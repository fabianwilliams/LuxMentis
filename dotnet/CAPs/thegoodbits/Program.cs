using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

internal class Program
{
    private static IConfiguration? _configuration;

    private static async Task Main(string[] args)
    {
        //Load Config
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _configuration = configurationBuilder.Build();

        //Initial Kernel
        var apiKey = _configuration["OpenAI:ApiKey"];
        var modelId = "gpt-4o";

        var kernelBuilder = Kernel.CreateBuilder();

        kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);

        var kernel = kernelBuilder.Build();

        //Load Skills
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory chatMessages = new ChatHistory();    

        while (true)
        {
            Console.WriteLine("Enter your message (or 'exit' to quit):");
            string userMessage = Console.ReadLine() ?? string.Empty;

            if (userMessage.ToLower() == "exit")
                break;

            chatMessages.AddUserMessage(userMessage);

            var result = chatService.GetStreamingChatMessageContentsAsync(chatMessages);
            var fullConversation = "";

            await foreach (var message in result)
            {
                fullConversation += message;
                Console.Write(message);
            }
            chatMessages.AddAssistantMessage(fullConversation);
            //Console.WriteLine($"Assistant: {fullConversation}");
            Console.WriteLine();
        }
    }
}