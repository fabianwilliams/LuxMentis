using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;

// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001, SKEXP0060

var endpoint = new Uri("http://localhost:11434");
var modelId = "llama3.1:70b";

var builder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint); //suppression of 0010

var kernel = builder.Build();

/*
//Below test fires the currency converter
kernel.ImportPluginFromType<CurrencyConverter>();
kernel.ImportPluginFromType<ConversationSummaryPlugin>();
var prompts = kernel.ImportPluginFromPromptDirectory("Prompts");

var result = await kernel.InvokeAsync("CurrencyConverter", "ConvertAmount", 
    new() {
        {"targetCurrencyCode", "USD"}, 
        {"amount", "25000"}, 
        {"baseCurrencyCode", "JMD"}
    }
);

Console.WriteLine(result);
*/

/*
//Elevate this so we can just call a function to determine what the user wants to convert
//In this the prompt will call the Plugin
kernel.ImportPluginFromType<CurrencyConverter>();
var prompts = kernel.ImportPluginFromPromptDirectory("Prompts");

var result = await kernel.InvokeAsync(prompts["GetTargetCurrencies"],
    new() {
        {"input", "How many Jamaican Dollars is 140,000 Mexican Peso worth?"}
    }
);

Console.WriteLine(result);
*/

kernel.ImportPluginFromType<CurrencyConverter>();
kernel.ImportPluginFromType<ConversationSummaryPlugin>();
var prompts = kernel.ImportPluginFromPromptDirectory("Prompts");

// Note: ChatHistory isn't working correctly as of SemanticKernel v 1.4.0
StringBuilder chatHistory = new();

OpenAIPromptExecutionSettings settings = new()
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

string input;

do {
    Console.WriteLine("What would you like to do?");
    input = Console.ReadLine()!;

    var intent = await kernel.InvokeAsync<string>(
        prompts["GetIntent"], 
        new() {{ "input",  input }}
    );

    switch (intent) {
        case "ConvertCurrency": 
            var currencyText = await kernel.InvokeAsync<string>(
                prompts["GetTargetCurrencies"], 
                new() {{ "input",  input }}
            );
            
            var currencyInfo = currencyText!.Split("|");
            var result = await kernel.InvokeAsync("CurrencyConverter", 
                "ConvertAmount", 
                new() {
                    {"targetCurrencyCode", currencyInfo[0]}, 
                    {"baseCurrencyCode", currencyInfo[1]},
                    {"amount", currencyInfo[2]}, 
                }
            );
            Console.WriteLine(result);
            break;
        case "SuggestDestinations":
            chatHistory.AppendLine("User:" + input);
            var recommendations = await kernel.InvokePromptAsync(input!);
            Console.WriteLine(recommendations);
            break;
        case "SuggestActivities":

            var chatSummary = await kernel.InvokeAsync(
                "ConversationSummaryPlugin", 
                "SummarizeConversation", 
                new() {{ "input", chatHistory.ToString() }});

            var activities = await kernel.InvokePromptAsync(
                input!,
                new () {
                    {"input", input},
                    {"history", chatSummary},
                    {"ToolCallBehavior", ToolCallBehavior.AutoInvokeKernelFunctions}
            });

            chatHistory.AppendLine("User:" + input);
            chatHistory.AppendLine("Assistant:" + activities.ToString());

            Console.WriteLine(activities);
            break;
        case "HelpfulPhrases":
        case "Translate":
            var autoInvokeResult = await kernel.InvokePromptAsync(input, new(settings));
            Console.WriteLine(autoInvokeResult);
            break;
        default:
            Console.WriteLine("Sure, I can help with that.");
            var otherIntentResult = await kernel.InvokePromptAsync(input);
            Console.WriteLine(otherIntentResult);
            break;
    }
} 
while (!string.IsNullOrWhiteSpace(input));
