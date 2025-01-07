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
using System.Threading;
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
            ("MessagesPlugin", "meListMessages", new[] { "MessagesPlugin" }),
            ("ContactsPlugin", "me_ListContacts", new[] { "ContactsPlugin" })
        };

    static async Task Main(string[] args)
    {
        // Initialize logging and configuration
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<Program>();
        _logger = loggerFactory.CreateLogger<Program>();

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _configuration = configurationBuilder.Build();

        _logger.LogInformation("Starting Semantic Kernel Program");

        var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };       

        /*
        // Initialize kernel for local models
        var endpoint = new Uri("http://localhost:11434/v1");
        var modelId = "llama3.3:latest"; // is faster gives better results and yes also does tool calling
        //var modelId = "llama3.1:70b"; //Will give slower responses but does DO tool calling come to find out
        //var modelId = "phi3:14b";  // Will not work becausae there is 'stated' no Tool calling support
        //var modelId = "reflection:latest"; // as above

        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint, httpClient: httpClient);
        */


        
        // Initialize Kernel using OpenAI models
        var apikey = _configuration["OpenAI:ApiKey"];
        var modelId = "gpt-4o";

        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: apikey, httpClient: httpClient);  
        
        
        
        var kernel = builder.Build();

        // Load Plugins
        await AddApiManifestPluginsAsync(kernel, httpClient);

        // Perform chat completion using the plugins
        await PerformChatCompletion(kernel);
    }

    static async Task<bool> ImportPluginWithRetry(Kernel kernel, string pluginName, string filePath, HttpClient httpClient, int maxRetries = 3)
    {
        int retryCount = 0;
        while (retryCount < maxRetries)
        {
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

                await kernel.ImportPluginFromOpenApiAsync(
                    pluginName: pluginName,
                    filePath: filePath,
                    executionParameters: new OpenApiFunctionExecutionParameters
                    {
                        HttpClient = httpClient,
                        EnablePayloadNamespacing = true
                    },
                    cts.Token
                );
                return true;
            }
            catch (TaskCanceledException ex)
            {
                retryCount++;
                _logger.LogWarning($"Timeout loading {pluginName}, retrying {retryCount}/{maxRetries}...");

                if (retryCount >= maxRetries)
                {
                    _logger.LogError($"Failed to load {pluginName} after {maxRetries} attempts.");
                    throw;
                }
            }
        }
        return false;
    }

    static async Task AddApiManifestPluginsAsync(Kernel kernel, HttpClient httpClient)
    {
        var token = await GetGraphAccessTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("Failed to acquire Graph token.");
            return;
        }

        _logger.LogInformation("Successfully acquired Graph token: {Token}", token.Substring(0, 20));

        string pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Plugins", "OpenAPISpecs");
        var pluginFiles = Directory.GetFiles(pluginsDirectory, "*.json", SearchOption.AllDirectories).ToList();

        foreach (var filePath in pluginFiles)
        {
            var pluginName = Path.GetFileNameWithoutExtension(filePath);
 
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                await ImportPluginWithRetry(kernel, pluginName, filePath, httpClient);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to load plugin {pluginName}: {ex.Message}");
            }
        }

        _logger.LogInformation("All plugins loaded successfully.");
    }

    static async Task PerformChatCompletion(Kernel kernel)
    {
        
        string prompt = @"
        Give me informaiton about Keyser in a tabular format please
        ";
        

        /*
        string prompt = @"
        What can you tell me about whats in my Calendar
        ";
        */

        /*
        // Create the prompt asking about the latest email
        //THIS WORKS and is tested
        string prompt = @"
        Hey, I need some help.
        Can you tell me what the last 2 emails in my inbox only from Keyser?
        Please summarize the details for me as concise as possible
        Whats important is who the sender is and their email address along with when it was sent subject and
        brief description of the ask. Give me in readable markdown";
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

        /*
        //Create a prompt that sends an email based on whats in inbox and contacts list
        //This works
        string prompt = @"
        Do i have anyone in my Contacts name Keyser
        If so then look in my email inbox to see if I have any email from that email address
        Create a response to any email from that email address using the content from that email
        and create a response on my behalf addressing any and all items in there
        keep it short, to the points only and send it now! 
        ";
        */


        _logger.LogInformation($"Sending the following prompt to LLM:\n{prompt}");
        
        OpenAIPromptExecutionSettings settings = new()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 1000
        };

        try
        {
            
            // Stream the response as chunks arrive
            await foreach (var chunk in kernel.InvokePromptStreamingAsync(prompt, new(settings)))
            {
                Console.Write(chunk);
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError($"The LLM request was canceled due to timeout. Exception: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred during streaming: {ex.Message}");
        }
    }

    static async Task<string> GetGraphAccessTokenAsync()
    {
        try
        {
            _logger.LogInformation("Attempting to acquire a new Graph token using device code flow...");

            var clientId = _configuration["AzureAd:ClientId"];
            var tenantId = _configuration["AzureAd:TenantId"];
            var scopes = new[] { "Mail.Read" };

            var app = PublicClientApplicationBuilder.Create(clientId)
                                .WithTenantId(tenantId)
                                .Build();

            var accounts = await app.GetAccountsAsync();
            foreach (var account in accounts)
            {
                _logger.LogInformation($"Clearing cached token for account: {account.Username}");
                await app.RemoveAsync(account);
            }

            var result = await app.AcquireTokenWithDeviceCode(scopes, callback =>
            {
                Console.WriteLine(callback.Message);
                _logger.LogInformation("Device code login message: " + callback.Message);
                return Task.FromResult(0);
            }).ExecuteAsync();

            _logger.LogInformation("Successfully acquired a new Graph token: {Token}", result.AccessToken.Substring(0, 20));
            return result.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error acquiring Graph token: {ex.Message}");
            return null;
        }
    }
}