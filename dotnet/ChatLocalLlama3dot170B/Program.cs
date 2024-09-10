using Microsoft.SemanticKernel;
using System;

var endpoint = new Uri("http://localhost:11434");
var modelId = "llama3.1:70b";

// Disable specific warnings
#pragma warning disable SKEXP0010
var kernelBuilder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint);
#pragma warning restore SKEXP0010

var kernel = kernelBuilder.Build();

// Define the Semantic Kernel prompt
const string skPrompt = @"
JamicanFoodBot can give you recommendations on Jamaican Food cuisine and recipes.
It can give explicit instructions on how to cook these dishes. It will provide you ingredients, measurements, and cook time.

{{ $history }}
User: {{ $userInput }}
JamicanFoodBot:";

// Create a function from the Semantic Kernel prompt
var chatFunction = kernel.CreateFunctionFromPrompt(skPrompt);

// Initialize history and set up arguments
var history = "";
var arguments = new KernelArguments
{
    ["history"] = history
};

while (true)
{
    // Ask the user for input
    Console.Write("Tell me what kind of food you are in the mood for: ");
    var userInput = Console.ReadLine();

    // Exit the loop if the user types "quit"
    if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye!");
        break;
    }

    // Update the arguments with the user's input
    arguments["userInput"] = userInput;

    // Start streaming the response from the chatFunction using the kernel
    Console.Write("JamaicanFoodBot: ");
    await foreach (var chunk in kernel.InvokeStreamingAsync(chatFunction, arguments))
    {
        // Print each chunk as it arrives
        Console.Write(chunk);
        history += chunk;
    }

    // Update the conversation history
    history += $"\nUser: {userInput}\n";
    arguments["history"] = history;

    // Display the conversation history so far
    Console.WriteLine();  // Line break after the bot's response
    Console.WriteLine("Conversation history:");
    Console.WriteLine(history);
}