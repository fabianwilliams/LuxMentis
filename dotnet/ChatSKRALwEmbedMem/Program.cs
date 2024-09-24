using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Services;
using OpenAI.Assistants;
using OpenAI.Chat;
using System;
using System.Net;
using System.Text;



internal class Program
{
    private static async Task Main(string[] args)
    {
        var endpoint = new Uri("http://localhost:11434");
        var modelId = "llama3.1:70b";

        // Disable specific warnings
#pragma warning disable SKEXP0010
        var kernelBuilder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint);
#pragma warning restore SKEXP0010

        kernelBuilder.Plugins.AddFromType<GraphChangelog>();

        var kernel = kernelBuilder.Build();

        // Import the DateTimeHelpers plugin if needed
        kernel.ImportPluginFromFunctions("DateTimeHelpers",
        new[]
        {
            kernel.CreateFunctionFromMethod(() => $"{DateTime.Now:r}", "Now", "Gets the current date and time. It is provided in Local Time Zone")
        });

        // Create an inline function using a prompt that includes DateTimeHelpers
        KernelFunction qa = kernel.CreateFunctionFromPrompt(@"
            The current date and time is {{ datetimehelpers.now }}.
            {{ $input }}
        ");

        // Create a new chat
        IChatCompletionService ai = kernel.GetRequiredService<IChatCompletionService>();
        ChatHistory chat = new("You are an AI assistant that helps people find information.");
        
        StringBuilder builder = new();

        // Q&A loop
        while (true)
        {
            Console.Write("Question: ");
            var userInput = Console.ReadLine(); // Read user input once and reuse it

            // Exit the loop if the user types "quit"
            if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            // Invoke the KernelFunction dynamically with the user's input and DateTime
            // Create a KernelArguments object with the input
            var kernelArguments = new KernelArguments();
            kernelArguments.Add("input", userInput);  // Pass user input as "input"

            // Invoke the kernel function
            var promptResult = await kernel.InvokeAsync(qa, kernelArguments);

            // Add the dynamically generated prompt to the chat history
            chat.AddUserMessage(promptResult.ToString());

            // Clear the string builder before receiving new responses
            builder.Clear();

            // Stream the assistant's response
            await foreach (StreamingChatMessageContent message in ai.GetStreamingChatMessageContentsAsync(chat))
            {
                Console.Write(message); // Display streaming content
                builder.Append(message.Content); // Append streamed content to builder
            }

            // Add the assistant's message to the chat history
            chat.AddAssistantMessage(builder.ToString());
            Console.WriteLine(); // Add spacing
        }
    }
}