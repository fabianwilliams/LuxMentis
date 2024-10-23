// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins;

namespace SemanticKernelApp
{
    class Program
    {
        private static IConfiguration _configuration;

        static readonly IEnumerable<(string PluginToTest, string FunctionToTest, Dictionary<string, string> Arguments, string[] PluginsToLoad)> Parameters =
            new[]
            {
                ("MessagesPlugin", "meListMessages", new Dictionary<string, string> { { "_top", "1" } }, new[] { "MessagesPlugin" }),
                ("DriveItemPlugin", "driverootGetChildrenContent", new Dictionary<string, string> { { "driveItem-Id", "test.txt" } }, new[] { "DriveItemPlugin", "MessagesPlugin" }),
                ("ContactsPlugin", "meListContacts", new Dictionary<string, string> { { "_count", "true" } }, new[] { "ContactsPlugin", "MessagesPlugin" }),
                ("CalendarPlugin", "mecalendarListEvents", new Dictionary<string, string> { { "_top", "1" } }, new[] { "CalendarPlugin", "MessagesPlugin" }),
                // Multiple API dependencies
                ("AstronomyPlugin", "meListMessages", new Dictionary<string, string> { { "_top", "1" } }, new[] { "AstronomyPlugin" }),
                ("AstronomyPlugin", "apod", new Dictionary<string, string> { { "_date", "2022-02-02" } }, new[] { "AstronomyPlugin" })
            };

        static async Task Main(string[] args)
        {
            var endpoint = new Uri("http://localhost:11434/v1");
            var modelId = "llama3.1:70b";
            
            // Build configuration
            var configurationBuilderbuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = configurationBuilderbuilder.Build();

            // Initialize the kernel
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            //var kernel = Kernel.CreateBuilder.WithLoggerFactory(loggerFactory).Build();
            // Build the kernel and import plugins
            // Build the kernel and import plugins
            var builder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint);

            builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));
            var kernel = builder.Build();
            //var kernel = (IKernel)builder.Build();

            // Loop over the parameters and run the sample
            foreach (var (pluginToTest, functionToTest, arguments, pluginsToLoad) in Parameters)
            {
                await RunSampleAsync(kernel, pluginToTest, functionToTest, arguments, pluginsToLoad);
            }
        }

        static void WriteSampleHeadingToConsole(string pluginToTest, string functionToTest, Dictionary<string, string> arguments, string[] pluginsToLoad)
        {
            Console.WriteLine();
            Console.WriteLine("======== [ApiManifest Plugins Sample] ========");
            Console.WriteLine($"======== Loading Plugins: {string.Join(", ", pluginsToLoad)} ========");
            Console.WriteLine($"======== Calling Plugin Function: {pluginToTest}.{functionToTest} with parameters {string.Join(", ", arguments.Select(kvp => $"{kvp.Key} = {kvp.Value}"))} ========");
            Console.WriteLine();
        }

        static async Task RunSampleAsync(Kernel kernel, string pluginToTest, string functionToTest, Dictionary<string, string> arguments, string[] pluginsToLoad)
        {
            WriteSampleHeadingToConsole(pluginToTest, functionToTest, arguments, pluginsToLoad);
            await AddApiManifestPluginsAsync(kernel, pluginsToLoad);

            // Get the function
            //var oldfunction = kernel.Skills.GetFunction(pluginToTest, functionToTest);
            var function = kernel.ImportPluginFromFunctions(pluginToTest, functionToTest);

            if (function == null)
            {
                Console.WriteLine($"Function {pluginToTest}.{functionToTest} not found.");
                return;
            }

            // Create context and set variables
            var context = kernel.CreateNewContext();
            foreach (var kvp in arguments)
            {
                context.Variables.Set(kvp.Key, kvp.Value);
            }

            // Invoke the function
            var result = await function.InvokeAsync(context);

            Console.WriteLine("--------------------");
            Console.WriteLine($"\nResult:\n{result.Result}\n");
            Console.WriteLine("--------------------");
        }

        static async Task AddApiManifestPluginsAsync(Kernel kernel, string[] pluginNames)
        {
            // Microsoft Graph authentication
            var token = await GetGraphAccessTokenAsync();

            var authenticationProvider = new BearerAuthenticationProvider(() => Task.FromResult(token));

            // Microsoft Graph API execution parameters
            var graphExecutionParameters = new OpenApiExecutionParameters
            {
                AuthCallback = authenticationProvider.AuthenticateRequestAsync,
                ServerUrlOverride = new Uri("https://graph.microsoft.com/v1.0")
            };

            // NASA API execution parameters
            var nasaExecutionParameters = new OpenApiExecutionParameters
            {
                AuthCallback = async (request, cancellationToken) =>
                {
                    var uri = request.RequestUri ?? throw new InvalidOperationException("The request URI is null.");
                    var query = QueryHelpers.ParseQuery(uri.Query);

                    var queryDict = query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
                    queryDict["api_key"] = "DEMO_KEY";

                    var newUri = QueryHelpers.AddQueryString(uri.GetLeftPart(UriPartial.Path), queryDict);

                    request.RequestUri = new Uri(newUri);
                }
            };

            var executionParameters = new Dictionary<string, OpenApiExecutionParameters>
            {
                { "microsoft.graph", graphExecutionParameters },
                { "nasa", nasaExecutionParameters }
            };

            foreach (var pluginName in pluginNames)
            {
                try
                {
                    var plugin = await kernel.ImportOpenApiPluginAsync(
                        pluginName,
                        $"Plugins/ApiManifestPlugins/{pluginName}/apimanifest.json",
                        executionParameters).ConfigureAwait(false);
                    Console.WriteLine($">> {pluginName} plugin is loaded.");
                }
                catch (Exception ex)
                {
                    kernel.LoggerFactory.CreateLogger("Plugin Creation").LogError(ex, "Plugin creation failed. Message: {0}", ex.Message);
                    throw new Exception($"Plugin creation failed for {pluginName}", ex);
                }
            }
        }

        static async Task<string> GetGraphAccessTokenAsync()
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
    }

    public class BearerAuthenticationProvider
    {
        private readonly Func<Task<string>> _getToken;

        public BearerAuthenticationProvider(Func<Task<string>> getToken)
        {
            _getToken = getToken;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            var token = await _getToken();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}