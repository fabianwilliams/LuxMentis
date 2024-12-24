using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Configuration;
using LLAMA3DOT370BTESTS.Plugins;
using Microsoft.SemanticKernel.ChatCompletion;

class Program
{
    private static IConfiguration _configuration;
    private static ILogger _logger;

    static async Task Main(string[] args)
    {
        // Initialize logging and configuration
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
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
        var modelId = "llama3.3:latest";
        var weatherApiKey = _configuration["WeatherAPI:weatherApiKey"];

        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint, httpClient: httpClient);

        var HostName = "AI Assistant";
        var HostInstructions = @"You are a helpful Assistant to answer their queries. 
        If the queries are related to getting the time or weather, Use the available plugin functions to get the answer.";

        var settings = new OpenAIPromptExecutionSettings()
        {
            Temperature = 0.0,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };
        var kernel = builder.Build();

        ChatCompletionAgent agent =
            new()
            {
                Instructions = HostInstructions,
                Name = HostName,
                Kernel = kernel,
                Arguments = new(settings),
            };

        KernelPlugin localTimePlugin = KernelPluginFactory.CreateFromType<LocalTimePlugin>();
        agent.Kernel.Plugins.Add(localTimePlugin);

        KernelPlugin weatherPlugin = KernelPluginFactory.CreateFromObject(new WeatherPlugin(weatherApiKey!));
        agent.Kernel.Plugins.Add(weatherPlugin);

        Console.WriteLine("Assistant: Hello, I am your Assistant. How may I help you?");

        AgentGroupChat chat = new();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("User: ");

            // Read user input once
            string userInput = Console.ReadLine() ?? "";

            // Exit if "quit"
            if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            // Pass the same input to the agent
            await InvokeAgentAsync(userInput);
        }

        // Local function to invoke agent and display the conversation messages.
        async Task InvokeAgentAsync(string question)
        {
            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, question));
            Console.ForegroundColor = ConsoleColor.Green;

            try
            {
                // Stream response as chunks
                await foreach (var chunk in agent.Kernel.InvokePromptStreamingAsync(question, new(settings)))
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
            
            Console.WriteLine();  // Ensure to print a new line after response completes
        }
    }
}