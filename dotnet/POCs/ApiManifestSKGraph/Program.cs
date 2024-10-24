// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001, SKEXP0040

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

namespace SemanticKernelApp
{
    class Program
    {
        private static IConfiguration _configuration;
        private static ILogger _logger;

        // Parameters for testing plugins (email and calendar)
        static readonly IEnumerable<(string PluginToTest, string FunctionToTest, string[] PluginsToLoad)> Parameters =
            new[] 
            { 
                ("MessagesPlugin", "meListMessages", new[] { "MessagesPlugin" })
            };

        static async Task Main(string[] args)
        {
            // Initialize logging and configuration
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug));
            _logger = loggerFactory.CreateLogger<Program>();

            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            _configuration = configurationBuilder.Build();

            // Logging the start of the program
            _logger.LogInformation("Starting Semantic Kernel Program");

            // Initialize the kernel
            var endpoint = new Uri("http://localhost:11434/v1");
            var modelId = "llama3.1:70b";

            var builder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint);

            var kernel = builder.Build();

            // Test Graph token acquisition
            var token = await GetGraphAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to acquire Graph token.");
                return;
            }

            _logger.LogInformation("Successfully acquired Graph token: {Token}", token.Substring(0, 20)); // Masked for safety

            // Load plugins (using OpenAPI for MessagesPlugin only)
            foreach (var (pluginToTest, functionToTest, pluginsToLoad) in Parameters)
            {
                // We're not using this anymore but kept for reference
                // await RunSampleAsync(kernel, pluginToTest, functionToTest, pluginsToLoad);
            }

            // Perform chat completion using the API manifest plugins
            await PerformChatCompletion(kernel);
        }

        static async Task RunSampleAsync(Kernel kernel, string pluginToTest, string functionToTest, string[] pluginsToLoad)
        {
            _logger.LogInformation($"Running test for {pluginToTest}.{functionToTest}");

            await AddApiManifestPluginsAsync(kernel, pluginsToLoad);

            var function = kernel.ImportPluginFromFunctions(pluginToTest, functionToTest);
            if (function == null)
            {
                _logger.LogError($"Function {pluginToTest}.{functionToTest} not found.");
                return;
            }

            _logger.LogInformation($"Successfully invoked {pluginToTest}.{functionToTest}");
        }

        static async Task AddApiManifestPluginsAsync(Kernel kernel, string[] pluginNames)
        {
            var token = await GetGraphAccessTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to acquire Graph token for plugin authentication.");
                return;
            }

            _logger.LogInformation("Loading plugins with Graph API token.");

            var authenticationProvider = new BearerAuthenticationProvider(() => Task.FromResult(token));

            foreach (var pluginName in pluginNames)
            {
                try
                {
                    _logger.LogInformation($"Loading plugin: {pluginName}");
                    
                    if (pluginName == "MessagesPlugin")
                    {
                        await kernel.ImportPluginFromOpenApiAsync(
                            pluginName: "MessagesPlugin",
                            filePath: "Plugins/ApiManifestPlugins/MessagesPlugin/apimanifest.json",
                            executionParameters: new OpenApiFunctionExecutionParameters
                            {
                                EnablePayloadNamespacing = true
                            }
                        );
                        _logger.LogInformation($"{pluginName} loaded successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to load plugin {pluginName}: {ex.Message}");
                }
            }
        }

        static async Task PerformChatCompletion(Kernel kernel)
        {
            // Create the prompt asking about the latest email
            string prompt = @"
            Hey, I need some help.
            Can you tell me what the latest unread email is?
            Please summarize the details for me.";

            // Logging the prompt before sending it to the LLM
            _logger.LogInformation($"Sending the following prompt to LLM:\n{prompt}");

            // Perform the chat completion using the prompt
            OpenAIPromptExecutionSettings settings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var functionResult = await kernel.InvokePromptAsync(prompt, new(settings));

            // Log or display the final result from the LLM
            _logger.LogInformation("LLM's response with the synthesized email:");

            if (functionResult != null)
            {
                // Output the result directly since "GetOutputsAsync()" was causing issues
                var resultText = functionResult.ToString();

                if (!string.IsNullOrEmpty(resultText))
                {
                    Console.WriteLine("Summary of Latest Email:");
                    Console.WriteLine(resultText);

                    // Check if the result contains any plugin-related information
                    if (resultText.Contains("MessagesPlugin"))
                    {
                        _logger.LogInformation("The MessagesPlugin was invoked successfully.");
                    }
                    else
                    {
                        _logger.LogWarning("MessagesPlugin was not invoked, check plugin loading or function invocation.");
                    }

                    // Additional proof: Log the raw result from LLM
                    _logger.LogInformation($"Raw LLM response: {resultText}");
                }
                else
                {
                    _logger.LogWarning("The result was null or empty. Check plugin invocation and execution.");
                }
            }
        }

        static async Task<string> GetGraphAccessTokenAsync()
        {
            try
            {
                _logger.LogInformation("Attempting to acquire Graph token...");

                var clientId = _configuration["AzureAd:ClientId"];
                var tenantId = _configuration["AzureAd:TenantId"];
                var clientSecret = _configuration["AzureAd:ClientSecret"];
                var scopes = new[] { "https://graph.microsoft.com/.default" };

                var app = ConfidentialClientApplicationBuilder.Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithTenantId(tenantId)
                    .Build();

                var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();

                _logger.LogInformation("Successfully acquired Graph token: {Token}", result.AccessToken.Substring(0, 20)); // Mask token
                return result.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error acquiring Graph token: {ex.Message}");
                return null;
            }
        }
    }

    public class BearerAuthenticationProvider
    {
        private readonly Func<Task<string>> _getToken;

        public BearerAuthenticationProvider(Func<Task<string>> getToken)
        {
            _getToken = getToken;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var token = await _getToken();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}