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

        
        // Initialize kernel for local models
        var endpoint = new Uri("http://localhost:11434/v1");
        var modelId = "cogito:70b"; // using Cohere local model
        //var modelId = "llama3.3:latest"; // is faster gives better results and yes also does tool calling
        //var modelId = "llama3.3:70b"; //Will give slower responses but does DO tool calling come to find out
        //var modelId = "qwq:32b-preview-fp16"; //testing my latest Llama local model ..does not work fails at invocation of InovkePrompt..Async
        //var modelId = "deepseek-r1:70b"; //testing deepseek r1 ... alas DOES NOT support tool calling per errormsg
        //var modelId = "phi4:14b-fp16"; // also fails with 400 registry.ollama.ai/library/phi4:14b-fp16 does not support tools
        //var modelId = "phi3:14b";  // Will not work becausae there is 'stated' no Tool calling support
        //var modelId = "reflection:latest"; // as above

        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint, httpClient: httpClient);
        
        
        
        /*
        // Initialize Kernel using OpenAI models
        var apikey = _configuration["OpenAI:ApiKey"];
        var modelId = "gpt-4o";

        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: apikey, httpClient: httpClient);  
        
        */
        
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
        you have access to the Contacts Plugin. You are already Authenticated and have a Token. Use the Plugin and Give me informaiton about my contact Keyser Soze please.
         Do not ask me for anything else, just execute the plugin and give me the result.
        ";
        

        /*
        string prompt = @"
        What can you tell me about whats in my Calendar
        ";
        */

        /*
        string prompt = @"
        Add a new Event to my Calendar with Subject Meet Barry about Testing and with a Start at 9 am and a stop at 9:30
        ";
        */

        /*
        string prompt = @"
        Hey, I need some help.
        Can you tell me what the last 2 emails in my inbox only from Keyser?";
        */

        
        /*
        // Create the prompt asking about the latest email
        //THIS WORKS and is tested
        string prompt = @"
        Hey, I need some help.
        Can you tell me what the last email in my inbox only from KEYSER?
        Please summarize the details for me as concise as possible
        Send that summary as the body immideately to jahmekyanbwoy@gmail.com with
        an appropriate Subject";
        */

        /*
        //Check to see if multiple can be called starting with contacts
        //This prompt works on gpt-4o
        string prompt = @"
        Hey, I need some help.
        I saw Keyser Soze today thought I met him before
        can you give me his information please
        and remind me do I have any meetings with Keyser as well
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