using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    private static IConfiguration _configuration;
    private static ILoggerFactory _loggerFactory;
    private static ILogger<Program> _logger;

    static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _configuration = configurationBuilder.Build();

        // Initialize logger factory and logger
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _logger = _loggerFactory.CreateLogger<Program>();

        _logger.LogInformation("Application starting...");

        // Initialize the kernel
        var kernel = InitializeKernel();

        // Instantiate CoordinatorAgent
        var coordinator = new CoordinatorAgent(kernel, _loggerFactory);

        // Execute workflow
        await coordinator.ExecuteWorkflowAsync();
    }

    /// <summary>
    /// Initializes the Semantic Kernel with either OpenAI (hosted) or local model support
    /// </summary>
    static Kernel InitializeKernel()
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        //var useLocalModel = _configuration.GetValue<bool>("UseLocalModel");
        var useLocalModel = _configuration.GetValue<bool>("No");

        if (useLocalModel)
        {
            _logger.LogInformation("Initializing with Local LLM (Ollama/DeepSeek).");

            var endpoint = new Uri(_configuration["LocalModel:Endpoint"] ?? "http://localhost:11434/v1");
            var modelId = _configuration["LocalModel:ModelId"] ?? "deepseek-r1:70b";

            return Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint, httpClient: httpClient)
                .Build();
        }
        else
        {
            _logger.LogInformation("Initializing with OpenAI (Hosted GPT-4).");

            var apiKey = _configuration["OpenAI:ApiKey"];
            var modelId = _configuration["OpenAI:ModelId"] ?? "gpt-4o";

            return Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(modelId: modelId, apiKey: apiKey, httpClient: httpClient)
                .Build();
        }
    }
}