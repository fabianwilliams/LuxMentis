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
            //THIS WORKS and is tested
            string prompt = @"
            Hey, I need some help.
            Can you tell me what the last couple unread email is?
            Please summarize the details for me as concise as possible
            Whats important is who its from when it was sent subject and
            brief description of the ask";
            */

            /*
            //Check to see if multiple can be called starting with contacts
            string prompt = @"
            Hey, I need some help.
            I saw Fabs today thought I met him before
            can you give his information please
            and remind me do I have any meetings with him or emails from fabsgwill@gmail.com
            I forgot";
            */

            /*
            //Create a prompt that sends an email
            //This works
            string prompt = @"
            I saw Fabs today and got an email from fabsgwill@gmail.com
            Check for that email and look at my calendar and other emails
            and create a response on my behalf addressing any and all items in there
            keep it short, to the points only and send it now! 
            Thanks!’ 
            Confirm if the email was sent successfully.
            ";
            */

            //Create a prompt that sends an email based on whats in inbox and contacts list
            //This works
            string prompt = @"
            Do i have anyone in my Contacts name Keyser
            If so then look in my email inbox to see if I have any email from that email address
            Create a response to any email from that email address using the content from that email
            and create a response on my behalf addressing any and all items in there
            keep it short, to the points only and send it now! 
            ";
            

            // Logging the prompt before sending it to the LLM
            _logger.LogInformation($"Sending the following prompt to LLM:\n{prompt}");

            // Perform the chat completion using the prompt
            OpenAIPromptExecutionSettings settings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 300
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