using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001

var endpoint = new Uri("http://localhost:11434");
var modelId = "llama3.1:70b";

var builder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint); //suppression of 0010

//Below adds a single music to the text file playlist using SK
var kernel = builder.Build();

kernel.ImportPluginFromType<MusicLibraryPlugin>();
kernel.ImportPluginFromType<MusicConcertPlugin>();
kernel.ImportPluginFromPromptDirectory("Prompts");

OpenAIPromptExecutionSettings settings = new()
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

string prompt = @"I live in Portland OR USA. Based on my recently 
    played songs and a list of upcoming concerts, which concert 
    do you recommend?";

var result = await kernel.InvokePromptAsync(prompt, new(settings));

Console.WriteLine(result);