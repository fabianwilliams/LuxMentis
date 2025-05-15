// Disable specific warnings
#pragma warning disable SKEXP0001, SKEXP0040

using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using System.Threading.Tasks;
using System.IO;
using Microsoft.SemanticKernel.Plugins.OpenApi.Extensions;
using Microsoft.Identity.Client;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Diagnostics;
using OtelBetter;

namespace OtelBetter;
internal class Program
{
    private static IConfiguration? _configuration;
    private static ILogger<Program>? _logger;
    private static ActivitySource? _activitySource;
    private static BearerAuthenticationProviderWithCancellationToken? _bearerAuthenticationProviderWithCancellationToken;

    private static async Task Main(string[] args)
    {
        // Initialize configuration and logging
          var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _configuration = configurationBuilder.Build();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
        _logger = loggerFactory.CreateLogger<Program>();
        var bearerLogger = loggerFactory.CreateLogger<BearerAuthenticationProviderWithCancellationToken>();

        _bearerAuthenticationProviderWithCancellationToken = new BearerAuthenticationProviderWithCancellationToken(_configuration, bearerLogger);

        // Initialize OpenTelemetry
        var serviceName = "SemanticKernelConsoleApp";
        var serviceVersion = "1.0.0";

        _activitySource = new ActivitySource(serviceName);

        // Keep this global if you want to use it throughout the app
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri("http://localhost:18889"); // Aspire dashboard
            })
            .Build();


        // Initialize the kernel with OpenAI and Graph authentication
        var kernel = await InitializeKernelAsync();

        // Load plugins dynamically
        await LoadPluginsAsync(kernel);

        // Enter the prompt-response loop
        await PromptLoopAsync(kernel);
    }
    private static async Task<Kernel> InitializeKernelAsync()
    {
        using var activity = _activitySource?.StartActivity("InitializeKernel", ActivityKind.Internal);

        var apikey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apikey))
        {
            _logger?.LogError("OpenAI API key is not configured.");
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        var modelId = "gpt-4o";
        activity?.SetTag("openai.model", modelId);

        var kernelBuilder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: apikey);

        var kernel = kernelBuilder.Build();

        kernel.FunctionInvocationFilters.Add(new FunctionTracingFilter(_activitySource));
        //kernel.AutoFunctionInvocationFilters.Add(new ExpectedSchemaFunctionFilter()); 


        return kernel;
    }


    private static async Task LoadPluginsAsync(Kernel kernel)
    {
        const string PluginsDirectory = "Plugins/CopilotAgentPlugins";

        if (!Directory.Exists(PluginsDirectory))
        {
            _logger?.LogWarning($"Plugins directory not found: {PluginsDirectory}");
            return;
        }

        foreach (var pluginPath in Directory.GetDirectories(PluginsDirectory))
        {
            var pluginName = Path.GetFileName(pluginPath);
            var manifestFile = Directory.GetFiles(pluginPath, "*-apiplugin.json").FirstOrDefault();

            using var activity = _activitySource?.StartActivity("LoadPlugin", ActivityKind.Internal);
            activity?.SetTag("plugin.name", pluginName);
            activity?.SetTag("plugin.path", pluginPath);
            activity?.SetTag("plugin.manifestFile", manifestFile ?? "null");

            if (string.IsNullOrEmpty(manifestFile))
            {
                _logger?.LogWarning($"No manifest file found for plugin: {pluginName}. Ensure a file ending with '-apiplugin.json' exists in {pluginPath}.");
                activity?.SetTag("plugin.load.success", false);
                activity?.SetStatus(ActivityStatusCode.Error, "Manifest file missing");
                continue;
            }

            try
            {
                var copilotAgentPluginParameters = new CopilotAgentPluginParameters
                {
                    FunctionExecutionParameters = new()
                    {
                        {
                            "https://graph.microsoft.com/v1.0",
                            new OpenApiFunctionExecutionParameters(
                                authCallback: Program._bearerAuthenticationProviderWithCancellationToken.AuthenticateRequestAsync,
                                enableDynamicOperationPayload: false,
                                enablePayloadNamespacing: true)
                            {
                                ParameterFilter = s_restApiParameterFilter
                            }
                        },
                    },
                };

                var manifestPath = Path.GetFullPath(manifestFile);

                if (!File.Exists(manifestPath))
                {
                    _logger?.LogError($"Manifest file not found: {manifestPath}");
                    activity?.SetTag("plugin.load.success", false);
                    activity?.SetStatus(ActivityStatusCode.Error, "Manifest file path invalid");
                    continue;
                }

                _logger?.LogInformation($"Loading plugin '{pluginName}' from {manifestPath}...");

                await kernel.ImportPluginFromCopilotAgentPluginAsync(pluginName, manifestPath, copilotAgentPluginParameters);

                _logger?.LogInformation($"Plugin '{pluginName}' loaded successfully.");
                activity?.SetTag("plugin.load.success", true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to load plugin '{pluginName}' from {manifestFile}.");
                activity?.SetTag("plugin.load.success", false);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
        }
    }

    private static async Task PromptLoopAsync(Kernel kernel)
    {
        while (true)
        {
            Console.WriteLine("Enter your prompt (type 'quit' to exit):");
            var userInput = Console.ReadLine();

            if (string.Equals(userInput, "quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            if (!string.IsNullOrEmpty(userInput))
            {
                using var promptActivity = _activitySource?.StartActivity("InvokePrompt", ActivityKind.Server);
                promptActivity?.SetTag("user.prompt", userInput);
                promptActivity?.SetTag("timestamp", DateTime.UtcNow.ToString("o"));

                try
                {
                    var promptExecutionSettings = new PromptExecutionSettings
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                            options: new FunctionChoiceBehaviorOptions
                            {
                                AllowStrictSchemaAdherence = true
                            })
                    };

                    var result = await kernel.InvokePromptAsync(
                        userInput,
                        new KernelArguments(promptExecutionSettings)
                    ).ConfigureAwait(false);

                    var output = result?.ToString() ?? "[null]";
                    promptActivity?.SetTag("result.length", output.Length);
                    promptActivity?.SetStatus(ActivityStatusCode.Ok);

                    Console.WriteLine("Response:");
                    Console.WriteLine(output);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error invoking the plugin.");
                    promptActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    Console.WriteLine("An error occurred while invoking the plugin. Please check the logs for details.");
                }
            }
            else
            {
                _logger?.LogWarning("User input is null or empty.");
            }

            Console.WriteLine();
        }
    }

    #region MagicDoNotLookUnderTheHood
    private static readonly HashSet<string> s_fieldsToIgnore = new(
        [
            "@odata.type",
            "attachments",
            "bccRecipients",
            "bodyPreview",
            "categories",
            "ccRecipients",
            "conversationId",
            "conversationIndex",
            "extensions",
            "flag",
            "from",
            "hasAttachments",
            "id",
            "inferenceClassification",
            "internetMessageHeaders",
            "isDeliveryReceiptRequested",
            "isDraft",
            "isRead",
            "isReadReceiptRequested",
            "multiValueExtendedProperties",
            "parentFolderId",
            "receivedDateTime",
            "replyTo",
            "sender",
            "sentDateTime",
            "singleValueExtendedProperties",
            "uniqueBody",
            "webLink",
        ],
        StringComparer.OrdinalIgnoreCase
    );
    private const string RequiredPropertyName = "required";
    private const string PropertiesPropertyName = "properties";
    /// <summary>
    /// Trims the properties from the request body schema.
    /// Most models in strict mode enforce a limit on the properties.
    /// </summary>
    /// <param name="schema">Source schema</param>
    /// <returns>the trimmed schema for the request body</returns>
    private static KernelJsonSchema? TrimPropertiesFromRequestBody(KernelJsonSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        var originalSchema = JsonSerializer.Serialize(schema.RootElement);
        var node = JsonNode.Parse(originalSchema);
        if (node is not JsonObject jsonNode)
        {
            return schema;
        }

        TrimPropertiesFromJsonNode(jsonNode);

        return KernelJsonSchema.Parse(node.ToString());
    }
    private static void TrimPropertiesFromJsonNode(JsonNode jsonNode)
    {
        if (jsonNode is not JsonObject jsonObject)
        {
            return;
        }
        if (jsonObject.TryGetPropertyValue(RequiredPropertyName, out var requiredRawValue) && requiredRawValue is JsonArray requiredArray)
        {
            jsonNode[RequiredPropertyName] = new JsonArray(requiredArray.Where(x => x is not null).Select(x => x!.GetValue<string>()).Where(x => !s_fieldsToIgnore.Contains(x)).Select(x => JsonValue.Create(x)).ToArray());
        }
        if (jsonObject.TryGetPropertyValue(PropertiesPropertyName, out var propertiesRawValue) && propertiesRawValue is JsonObject propertiesObject)
        {
            var properties = propertiesObject.Where(x => s_fieldsToIgnore.Contains(x.Key)).Select(static x => x.Key).ToArray();
            foreach (var property in properties)
            {
                propertiesObject.Remove(property);
            }
        }
        foreach (var subProperty in jsonObject)
        {
            if (subProperty.Value is not null)
            {
                TrimPropertiesFromJsonNode(subProperty.Value);
            }
        }
    }
#pragma warning disable SKEXP0040
    private static readonly RestApiParameterFilter s_restApiParameterFilter = (RestApiParameterFilterContext context) =>
    {
#pragma warning restore SKEXP0040
        if ("me_sendMail".Equals(context.Operation.Id, StringComparison.OrdinalIgnoreCase) &&
            "payload".Equals(context.Parameter.Name, StringComparison.OrdinalIgnoreCase))
        {
            context.Parameter.Schema = TrimPropertiesFromRequestBody(context.Parameter.Schema);
            return context.Parameter;
        }
        return context.Parameter;
    };
    #endregion
}