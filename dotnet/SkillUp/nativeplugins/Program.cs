using Microsoft.SemanticKernel;

// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001

var endpoint = new Uri("http://localhost:11434");
var modelId = "llama3.1:70b";

var builder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint); //suppression of 0010

/*
//Below adds a single music to the text file playlist using SK
var kernel = builder.Build();
kernel.ImportPluginFromType<MusicLibraryPlugin>();

var result = await kernel.InvokeAsync(
    "MusicLibraryPlugin", 
    "AddToRecentlyPlayed", 
    new() {
        ["artist"] = "Tiara", 
        ["song"] = "Danse", 
        ["genre"] = "French pop, electropop, pop"
    }
);

Console.WriteLine(result);   
*/

//Below will get music that is availale to the user
//It combines loading a function inside a prompt
var kernel = builder.Build();
kernel.ImportPluginFromType<MusicLibraryPlugin>();

string prompt = @"This is a list of music available to the user:
    {{MusicLibraryPlugin.GetMusicLibrary}} 

    This is a list of music the user has recently played:
    {{MusicLibraryPlugin.GetRecentPlays}}

    Based on their recently played music, suggest a song from
    the list to play next";

var result = await kernel.InvokePromptAsync(prompt);
Console.WriteLine(result);