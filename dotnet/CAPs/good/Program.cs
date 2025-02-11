using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Threading.Tasks;
internal class Program
{
    private static IConfiguration? _configuration;
    private static async Task Main(string[] args)
    {
        // Load configuration
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _configuration = configurationBuilder.Build();

        // Initialize Kernel using OpenAI models
        var apikey = _configuration["OpenAI:ApiKey"];
        var modelId = "gpt-4o";
        
        var kernelBuilder = Kernel.CreateBuilder();

        //TODO - Add Services in this case the OpenAIChatCompletionService
        kernelBuilder.AddOpenAIChatCompletion(modelId: modelId, apikey);

        //TODO - Add Plugins
        //kernelBuilder.AddPlugin<PluginType>(); but done in the "better" Project sample

        //Build the Kernel
        Kernel kernel = kernelBuilder.Build();

        //Get the Service you want to use in this case the ChatCompletionService
        var chatService = kernel.GetRequiredService<IChatCompletionService>(); //This is the service that will be used to send the messages to the LLM

        // Lets create a ChatHistory Object because this thing has no memory unless you keep adding to it
        ChatHistory chatMessages = new ChatHistory();

        //Lets New Up a Loop so we can continually have a conversation Use Console.Readline to a User Message object
        while (true)
        {
            Console.WriteLine("Enter your Prompt");
            var userInput = Console.ReadLine();

            // Exit the loop if the user types "quit"
            if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }
            else
            {
                chatMessages.AddUserMessage(userInput);
            }

            var completionMessage = chatService.GetStreamingChatMessageContentsAsync(chatMessages); //Used to send the message to the LLM
            var fullConversation = ""; //Used to hold the full message from the Stream

            await foreach(var content in completionMessage)
            {
                Console.Write(content.Content); //this sends out the response back as they come in 
                fullConversation += content.Content; //this loads the messages as they come in to our variable
            }

            chatMessages.AddAssistantMessage(fullConversation); //this stuffs it into the ChatHistory object
            Console.WriteLine(); //this puts us on a new line after the output
        }
    }
}


