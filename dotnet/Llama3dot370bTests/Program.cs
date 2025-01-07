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
        var modelId = "llama3.3:latest"; // is faster gives better results and yes also does tool calling
        //var modelId = "llama3.1:70b"; //Will give slower responses but does DO tool calling come to find out
        //var modelId = "phi3:14b";  // Will not work becausae there is 'stated' no Tool calling support
        //var modelId = "reflection:latest"; // as above
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

        async Task InvokeAgentAsync(string question)
        {
            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, question));
            Console.ForegroundColor = ConsoleColor.Green;

            try
            {
                // Step 1: Initial Plugin Invocation (Non-Streaming)
                var initialResponse = await agent.Kernel.InvokePromptAsync(
                    question,
                    new KernelArguments(settings)
                );

                // Print the result immediately from the tool/plugin
                Console.Write(initialResponse);

                // Step 2: Stream additional assistant commentary with explicit context
                string continuationPrompt = $"{initialResponse}\nPlease continue elaborating without repeating previous plugin calls. Be brief in response";

                await foreach (var chunk in agent.Kernel.InvokePromptStreamingAsync(
                    continuationPrompt,
                    new KernelArguments()  // Avoid additional plugin triggers
                ))
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

            Console.WriteLine();
        }
    }
}