// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001, SKEXP0040

using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using System;
using System.Collections.Generic;
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

            // Load plugins using HttpClient and token
            await AddApiManifestPluginsAsync(kernel, new[] { "MessagesPlugin" });

            // Perform the test with the loaded plugin
            await TestMessagesPlugin(kernel);
        }

        static async Task AddApiManifestPluginsAsync(Kernel kernel, string[] pluginNames)
        {
            // Get the Microsoft Graph Access Token
            var token = await GetGraphAccessTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to acquire Graph token.");
                return;
            }

            _logger.LogInformation("Successfully acquired Graph token: {Token}", token.Substring(0, 20)); // Masked for safety

            // Write token to text file for scope verification
            File.WriteAllText("token.txt", token);
            _logger.LogInformation("Token written to token.txt for scope verification.");

            foreach (var pluginName in pluginNames)
            {
                try
                {
                    _logger.LogInformation($"Loading plugin: {pluginName}");

                    if (pluginName == "MessagesPlugin")
                    {
                        // Create HttpClient with the Bearer token
                        var httpClient = new HttpClient();
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                        // Load the OpenAPI plugin using the HttpClient
                        await kernel.ImportPluginFromOpenApiAsync(
                            pluginName: "MessagesPlugin",
                            filePath: "Plugins/ApiManifestPlugins/MessagesPlugin/apimanifest.json",
                            executionParameters: new OpenApiFunctionExecutionParameters
                            {
                                HttpClient = httpClient, // Set up the HttpClient
                                EnablePayloadNamespacing = true // Enables namespacing of parameters to avoid conflicts
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

        static async Task TestMessagesPlugin(Kernel kernel)
        {
            try
            {
                // Prepare parameters for the plugin
                var parameters = new Dictionary<string, string>
                {
                    { "$top", "5" }, // Limit the number of results
                    { "$select", "id,receivedDateTime,subject,from,bodyPreview" } // Specify fields to return
                };

                /*
                // Invoke the plugin function directly
                var result = await kernel.ExecuteFunctionAsync(
                    pluginName: "MessagesPlugin",
                    functionName: "readEmails",  // Ensure this matches your manifest's operationId
                    parameters: parameters
                );
                */

                var token = await GetGraphAccessTokenAsync();

                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var result = await kernel.ImportPluginFromOpenApiAsync(
                            pluginName: "MessagesPlugin",
                            filePath: "Plugins/ApiManifestPlugins/MessagesPlugin/apimanifest.json",
                            executionParameters: new OpenApiFunctionExecutionParameters
                            {
                                HttpClient = httpClient,
                                EnablePayloadNamespacing = true
                            }
                        );

                // Log the result from Microsoft Graph API
                if (result != null)
                {
                    Console.WriteLine($"Result from MessagesPlugin: {result}");
                    _logger.LogInformation($"Result from MessagesPlugin: {result}");
                }
                else
                {
                    Console.WriteLine("No result from MessagesPlugin.");
                    _logger.LogWarning("No result from MessagesPlugin.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in TestMessagesPlugin: {ex.Message}");
            }
        }

        static async Task<string> GetGraphAccessTokenAsync()
        {
            try
            {
                _logger.LogInformation("Attempting to acquire a Graph token using device code flow...");

                var clientId = _configuration["AzureAd:ClientId"];
                var tenantId = _configuration["AzureAd:TenantId"];
                var scopes = new[] { "Mail.Read" }; // Adjust scopes if necessary

                var app = PublicClientApplicationBuilder.Create(clientId)
                                .WithTenantId(tenantId)
                                .Build();

                var result = await app.AcquireTokenWithDeviceCode(scopes, callback =>
                {
                    Console.WriteLine(callback.Message); // Display device code login message
                    _logger.LogInformation("Device code login message: " + callback.Message); // Log the device code message
                    return Task.FromResult(0);
                }).ExecuteAsync();

                // Log and return the token
                _logger.LogInformation("Successfully acquired Graph token: {Token}", result.AccessToken.Substring(0, 20));

                // Ensure that token is immediately usable
                if (!string.IsNullOrEmpty(result.AccessToken))
                {
                    _logger.LogInformation("Token is valid and ready for use.");
                }
                else
                {
                    _logger.LogError("Failed to acquire a valid token.");
                }

                return result.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error acquiring Graph token: {ex.Message}");
                return null;
            }
        }
    }
}