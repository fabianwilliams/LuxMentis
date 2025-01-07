using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;

class Program
{
    private static IConfiguration _configuration;
    private static ILoggerFactory _loggerFactory;
    private static ILogger<Program> _logger;

    static async Task Main(string[] args)
    {
        // Load configuration
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _configuration = configurationBuilder.Build();

        if (args.Length == 0)
        {
            Console.WriteLine("No project repo name provided.");
            return;
        }

        string projectRepoName = args[0];
        Console.WriteLine($"Generating content for: {projectRepoName}");

        // Initialize logger factory and logger
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _logger = _loggerFactory.CreateLogger<Program>();

        // Initialize the kernel
        var kernel = InitializeKernel();

        // Instantiate CoordinatorAgent
        var coordinator = new CoordinatorAgent(kernel, _loggerFactory);

        // Execute workflow
        await coordinator.ExecuteWorkflowAsync(projectRepoName);
    }

    // Example kernel initialization (modify as needed for your local LLM setup)
    static Kernel InitializeKernel()
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        
        /*
        //Using a LOCAL Model 
        var endpoint = new Uri("http://localhost:11434/v1");
        var modelId = "llama3.3:latest";

        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint, httpClient: httpClient);

        */

        
        // Initialize Kernel using OpenAI models
        var apikey = _configuration["OpenAI:ApiKey"];
        var modelId = "gpt-4o";

        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: apikey, httpClient: httpClient);  

        


        return builder.Build();
    }
}