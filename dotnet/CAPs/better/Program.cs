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

internal class Program
{
    private static IConfiguration? _configuration;
    private static ILogger<Program>? _logger;
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

        // Initialize the kernel with OpenAI and Graph authentication
        var kernel = await InitializeKernelAsync();

        // Load plugins dynamically
        await LoadPluginsAsync(kernel);

        // Enter the prompt-response loop
        await PromptLoopAsync(kernel);
    }
    private static async Task<Kernel> InitializeKernelAsync()
    {
        var kernelBuilder = Kernel.CreateBuilder();

        // Initialize Kernel using OpenAI models
        var apikey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apikey))
        {
            _logger?.LogError("OpenAI API key is not configured.");
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }
        var modelId = "gpt-4o";

        // Add OpenAI Chat Completion service
        kernelBuilder.AddOpenAIChatCompletion(modelId: modelId, apikey);

        return kernelBuilder.Build();
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

            if (string.IsNullOrEmpty(manifestFile))
            {
                _logger?.LogWarning($"No manifest file found for plugin: {pluginName}. Ensure a file ending with '-apiplugin.json' exists in {pluginPath}.");
                continue;
            }

            try
            {
                var copilotAgentPluginParameters = new CopilotAgentPluginParameters
                {
                    FunctionExecutionParameters = new()
                    {
                        { "https://graph.microsoft.com/v1.0", new OpenApiFunctionExecutionParameters(authCallback: Program._bearerAuthenticationProviderWithCancellationToken.AuthenticateRequestAsync, enableDynamicOperationPayload: false, enablePayloadNamespacing: true) { ParameterFilter = s_restApiParameterFilter} }
                        
                    },
                };
                // Convert manifest path to absolute path
                var manifestPath = Path.GetFullPath(manifestFile);

                if (!File.Exists(manifestPath))
                {
                    _logger?.LogError($"Manifest file not found: {manifestPath}");
                    continue;
                }

                _logger?.LogInformation($"Loading plugin '{pluginName}' from {manifestPath}...");

                // Load the plugin using the correct method
                await kernel.ImportPluginFromCopilotAgentPluginAsync(pluginName, manifestPath, copilotAgentPluginParameters);
                _logger?.LogInformation($"Plugin '{pluginName}' loaded successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to load plugin '{pluginName}' from {manifestFile}.");
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
                try
                {
                    // Configure the PromptExecutionSettings with loaded plugins
                    var promptExecutionSettings = new PromptExecutionSettings
                    {
                        //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new List<KernelFunction>())
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                        options: new FunctionChoiceBehaviorOptions
                        {
                            AllowStrictSchemaAdherence = true
                        })
                    };

                    var result = await kernel.InvokePromptAsync(userInput, new KernelArguments(promptExecutionSettings)).ConfigureAwait(false);

                    // Display the result in the console
                    Console.WriteLine("Response:");
                    Console.WriteLine(result.ToString());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error invoking the plugin.");
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
    private sealed class ExpectedSchemaFunctionFilter : IAutoFunctionInvocationFilter
    {//TODO: this eventually needs to be added to all CAP or DA but we're still discussing where should those facilitators live
        public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
        {
            await next(context).ConfigureAwait(false);

            if (context.Result.ValueType == typeof(RestApiOperationResponse))
            {
                var openApiResponse = context.Result.GetValue<RestApiOperationResponse>();
                if (openApiResponse?.ExpectedSchema is not null)
                {
                    openApiResponse.ExpectedSchema = null;
                }
            }
        }
    }
    #endregion
}