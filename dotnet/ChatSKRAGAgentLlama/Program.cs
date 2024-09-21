using Microsoft.SemanticKernel;
using System;
using System.Net;

var endpoint = new Uri("http://localhost:11434");
var modelId = "llama3.1:70b";

// Disable specific warnings
#pragma warning disable SKEXP0010
var kernelBuilder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint);
#pragma warning restore SKEXP0010

kernelBuilder.Plugins.AddFromType<GraphChangelog>();

var kernel = kernelBuilder.Build();

// Define the Semantic Kernel prompt which ACTS as a PERSONA
const string skPrompt = @"
GraphChangeLogBot can let you know recent changes in the Microsoft Graph ChangeLog.
It will provide you Title, Description, and published date of change for the most recent items, including links.

Here are the most recent items:
{{ $pluginFeed }}

{{ $history }}
User: {{ $userInput }}
GraphChangeLogBot:";

// Create a function from the Semantic Kernel prompt
var chatFunction = kernel.CreateFunctionFromPrompt(skPrompt);

// Initialize history and set up arguments
var history = "";
var arguments = new KernelArguments
{
    ["history"] = history,
    ["userInput"] = "",  // We'll capture user input here too
    ["pluginFeed"] = ""  // To display the pluginFeed properly
};

// Load the GraphChangelog plugin function
var getFeedFunction = kernel.Plugins.GetFunction("GraphChangelog", "get_formatted_graphlog_feed");

while (true)
{
    // Ask the user for input
    Console.Write("Tell me what areas on the Microsoft Graph you are looking for changes in? or type quit to exit: ");
    var userInput = Console.ReadLine();

    // Exit the loop if the user types "quit"
    if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye!");
        break;
    }

    // Update the user input in arguments
    arguments["userInput"] = userInput;

    // Invoke the plugin method to get the formatted feed (no filtering, just top 10 most recent)
    var pluginFeedResult = await kernel.InvokeAsync(getFeedFunction, arguments);
    
    // Extract the string result from FunctionResult using GetValue
    var pluginFeed = pluginFeedResult.GetValue<string>();

    // Ensure the pluginFeed contains proper links by checking for HTML encoding issues
    pluginFeed = WebUtility.HtmlDecode(pluginFeed);

    // Set the pluginFeed in the arguments to pass to the chatFunction
    arguments["pluginFeed"] = pluginFeed;

    // Start streaming the response from the chatFunction using the kernel
    Console.Write("GraphChangeLogBot: ");
    await foreach (var chunk in kernel.InvokeStreamingAsync(chatFunction, arguments))
    {
        // Print each chunk as it arrives
        Console.Write(chunk);
        history += chunk;
    }

    // Update the conversation history and include the plugin result
    history += "\nUser: Show me the most recent Graph changes\n";
    history += $"\nPlugin Output: {pluginFeed}\n";
    arguments["history"] = history;

    // Display the conversation history so far
    Console.WriteLine();  // Line break after the bot's response
    Console.WriteLine("Conversation history:");
    Console.WriteLine(history);
}