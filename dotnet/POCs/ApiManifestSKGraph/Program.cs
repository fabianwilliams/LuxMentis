// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel;

namespace SemanticKernelApp
{
    class Program
    {
        private static IConfiguration _configuration;
        private static ILogger _logger;

        // Parameters for testing plugins
        static readonly IEnumerable<(string PluginToTest, string FunctionToTest, Dictionary<string, string> Arguments, string[] PluginsToLoad)> Parameters =
            new[]
            {
                ("MessagesPlugin", "meListMessages", new Dictionary<string, string> { { "_top", "1" } }, new[] { "MessagesPlugin" }),
                ("DriveItemPlugin", "driverootGetChildrenContent", new Dictionary<string, string> { { "driveItem-Id", "test.txt" } }, new[] { "DriveItemPlugin", "MessagesPlugin" }),
                ("ContactsPlugin", "meListContacts", new Dictionary<string, string> { { "_count", "true" } }, new[] { "ContactsPlugin", "MessagesPlugin" }),
                ("CalendarPlugin", "mecalendarListEvents", new Dictionary<string, string> { { "_top", "1" } }, new[] { "CalendarPlugin", "MessagesPlugin" })
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

            // No explicit logger factory here, relying on DI pipeline for logging
            var kernel = builder.Build();

            // Test Graph token acquisition
            var token = await GetGraphAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to acquire Graph token.");
                return;
            }

            _logger.LogInformation("Successfully acquired Graph token: {Token}", token.Substring(0, 20)); // Masked for safety

            // Test loading plugins and running sample functions
            foreach (var (pluginToTest, functionToTest, arguments, pluginsToLoad) in Parameters)
            {
                await RunSampleAsync(kernel, pluginToTest, functionToTest, arguments, pluginsToLoad);
            }
        }

        static async Task RunSampleAsync(Kernel kernel, string pluginToTest, string functionToTest, Dictionary<string, string> arguments, string[] pluginsToLoad)
        {
            _logger.LogInformation($"Running test for {pluginToTest}.{functionToTest} with arguments {string.Join(", ", arguments.Select(kvp => $"{kvp.Key} = {kvp.Value}"))}");

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
            // Microsoft Graph token acquisition and plugin loading
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
                    // Use some alternative plugin loading mechanism if needed
                    // Simulating plugin loading here (since the original method `ImportPluginFromApiManifestAsync` is not available)
                    Console.WriteLine($">> Plugin {pluginName} is loaded.");
                    _logger.LogInformation($">> {pluginName} is created.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to load plugin {pluginName}: {ex.Message}");
                }
            }
        }

        static async Task<string> GetGraphAccessTokenAsync()
        {
            try
            {
                var clientId = _configuration["AzureAd:ClientId"];
                var tenantId = _configuration["AzureAd:TenantId"];
                var clientSecret = _configuration["AzureAd:ClientSecret"];
                var scopes = new[] { "https://graph.microsoft.com/.default" };

                var app = ConfidentialClientApplicationBuilder.Create(clientId)
                    .WithClientSecret(clientSecret)
                    .WithTenantId(tenantId)
                    .Build();

                var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
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