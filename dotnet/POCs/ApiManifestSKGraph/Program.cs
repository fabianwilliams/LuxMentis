// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001, SKEXP0040


using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using System;
using System.Collections.Generic;
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

        // Parameters for testing plugins (email only)
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
            /*
            var endpoint = new Uri("http://localhost:11434/v1");
            var modelId = "llama3.1:70b";
            */
            var apikey = _configuration["OpenAI:ApiKey"];
            var modelId = "gpt-4o";

            /*
            var builder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint);

            var kernel = builder.Build();
            */
           var builder = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: modelId, apiKey: apikey);

            var kernel = builder.Build();

            // Load plugins (using OpenAPI for MessagesPlugin only)
            await AddApiManifestPluginsAsync(kernel, new[] { "MessagesPlugin" });

            // Perform chat completion using the API manifest plugins
            await PerformChatCompletion(kernel);
        }

        static async Task AddApiManifestPluginsAsync(Kernel kernel, string[] pluginNames)
        {
            var token = await GetGraphAccessTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to acquire Graph token.");
                return;
            }

            _logger.LogInformation("Successfully acquired Graph token: {Token}", token.Substring(0, 20)); // Masked for safety

            // Write token to text file
            /*
            // Yeah dont do that 
            File.WriteAllText("token.txt", token);
            _logger.LogInformation("Token written to token.txt for scope verification.");
            */

            _logger.LogInformation("Loading plugins with Graph API token.");

            // Adjusted path to the plugins directory
            string pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Plugins", "OpenAPISpecs");


            // Get all .json and .yml files under the plugins directory
            var pluginFiles = Directory.GetFiles(pluginsDirectory, "*.*", SearchOption.AllDirectories)
                                    .Where(file => file.EndsWith(".json"))
                                    .ToList();

            foreach (var filePath in pluginFiles)
            {
                var pluginName = Path.GetFileNameWithoutExtension(filePath);

                try
                {
                    _logger.LogInformation($"Loading plugin: {pluginName}");

                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    await kernel.ImportPluginFromOpenApiAsync(
                        pluginName: pluginName,
                        filePath: filePath,
                        executionParameters: new OpenApiFunctionExecutionParameters
                        {
                            HttpClient = httpClient,
                            EnablePayloadNamespacing = true
                        }
                    );

                    _logger.LogInformation($"{pluginName} loaded successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to load plugin {pluginName}: {ex.Message}");
                }
            }
        }

        static async Task PerformChatCompletion(Kernel kernel)
        {
            /*
            // Create the prompt asking about the latest email
            string prompt = @"
            Hey, I need some help.
            Send an email to fabian@adotob.com letting him know
            to get the proposal for Vincent ready in the next 2 days? 
            with a Subject of Lets Meet Soon. Author and Send the email";
            */

            /*
            // Create the prompt asking about Contacts
            string prompt = @"
            Hey, I need some help.
            I need to send an email to someone and their name is Adis
            Can you give me their details please?";
            */
            

            /*
            // Create the prompt asking about Calendars
            //wont work due to filters and other items in the OpenAPI Spec
            string prompt = @"
            Hey, I need some help.
            How many meetings do i have tomorrow?";
            */

            /*
            // Create the prompt asking about the latest email
            //THIS WORKS and is tested
            string prompt = @"
            Hey, I need some help.
            Can you tell me what the last couple unread email is?
            Please summarize the details for me as concise as possible
            Whats important is who its from when it was sent subject and
            brief description of the ask";
            */

            
            //Create a prompt that sends an email
            string prompt = @"
            Compose and send an email to keysersoze9416@gmail.com with the 
            subject ‘Project Milestone Hit’ and body ‘Hi Keyser, We had a few successful test. 
            We’re on track for the deadline. Let me know if you have any questions. 
            Thanks!’ 
            Confirm if the email was sent successfully.
            ";
            

            // Logging the prompt before sending it to the LLM
            _logger.LogInformation($"Sending the following prompt to LLM:\n{prompt}");

            // Perform the chat completion using the prompt
            OpenAIPromptExecutionSettings settings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 2000
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
                _logger.LogInformation("Attempting to acquire a new Graph token using device code flow...");

                var clientId = _configuration["AzureAd:ClientId"];
                var tenantId = _configuration["AzureAd:TenantId"];
                var scopes = new[] { "Mail.Read" }; // Adjust scopes if necessary

                // Create the public client application with a token cache that we will clear
                var app = PublicClientApplicationBuilder.Create(clientId)
                                    .WithTenantId(tenantId)
                                    .Build();

                // Clear the token cache to ensure a fresh token request
                var accounts = await app.GetAccountsAsync();
                foreach (var account in accounts)
                {
                    _logger.LogInformation($"Clearing cached token for account: {account.Username}");
                    await app.RemoveAsync(account); // Clear cache for this account
                }

                // Acquire token with device code flow
                var result = await app.AcquireTokenWithDeviceCode(scopes, callback =>
                {
                    // Display the device code message for login
                    Console.WriteLine(callback.Message);
                    _logger.LogInformation("Device code login message: " + callback.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync();

                // Log and return the token
                _logger.LogInformation("Successfully acquired a new Graph token: {Token}", result.AccessToken.Substring(0, 20));

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