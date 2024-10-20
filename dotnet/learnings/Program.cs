using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Disable specific warnings
        #pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001

        var endpoint = new Uri("http://localhost:11434");
        var modelId = "llama3.1:70b";

        var builder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint); //suppression of 0010

        /*
        // Example 1: Just a regular inline prompt function to the kernel
        var kernel = builder.Build();
        var result = await kernel.InvokePromptAsync("Give me a list of breakfast foods with eggs and cheese");
        Console.WriteLine(result);
        */
        

        /*
        // This needs the core plugin using statement and calls on the dll with build in functions
        // See the readme for all of the available ones
        builder.Plugins.AddFromType<TimePlugin>(); //suppression of SKEXP0050 above added
        var kernel = builder.Build();
        var currentDay = await kernel.InvokeAsync("TimePlugin", "DayOfWeek");
        Console.WriteLine(currentDay);
        */

        /*
        //The below didnt work with an issue with the versions of prompt execution settings
        //The below one here summarizes using another build in function
        builder.Plugins.AddFromType<ConversationSummaryPlugin>();
        OpenAIPromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Required() }; //SK0001 pragma needed to fix this
        var kernel = builder.Build();

        string input = @"I'm a vegan in search of new recipes. I love spicy food! 
        Can you give me a list of breakfast recipes that are vegan friendly?";

        var result = await kernel.InvokeAsync("ConversationSummaryPlugin", "GetConversationActionItems", new() {{ "input", input }}); // Key dif here is passing in input

        Console.WriteLine(result);
        */

        /*
        // The below is a good example of an agent but it has no personality
        var kernel = builder.Build();
        string language = "French";
        //string prompt = @$"Create a list of helpful phrases and words in ${language} a traveler would find useful.";
        string prompt = @$"Create a list of helpful phrases and 
            words in ${language} a traveler would find useful.

            Group phrases by category. Display the phrases in 
            the following format: Hello - Ciao [chow]";
        var result = await kernel.InvokePromptAsync(prompt);
        Console.WriteLine(result);
        */

        /*
        //Lets give the agent some personality
        var kernel = builder.Build();

        string language = "German";
        string history = @"I'm traveling with my kids and one of them has a peanut allergy.";

        // Assign a persona to the prompt
        string prompt = @$"
            You are a travel assistant. You are helpful, creative, and very friendly. 
            Consider the traveler's background:
            ${history}

            Create a list of helpful phrases and words in ${language} a traveler would find useful.

            Group phrases by category. Include common direction words. 
            Display the phrases in the following format: 
            Hello - Ciao [chow]

            Begin with: 'Here are some phrases in ${language} you may find helpful:' 
            and end with: 'I hope this helps you on your trip!'";

        var result = await kernel.InvokePromptAsync(prompt);
        Console.WriteLine(result);   
        */

        /*
        //Now lets use roles in the prompt     
        var kernel = builder.Build();

        string input = @"I'm planning an anniversary trip with my family. We like eating out, train rides, 
        and beaches. Our travel budget is $10000";

        string prompt = @$"
            The following is a conversation with an AI travel assistant. 
            The assistant is helpful, creative, and very friendly.

            <message role=""user"">Can you give me some travel destination suggestions?</message>

            <message role=""assistant"">Of course! Do you have a budget or any specific 
            activities in mind?</message>

            <message role=""user"">${input}</message>";

        var result = await kernel.InvokePromptAsync(prompt);
        Console.WriteLine(result);  
        */

        var kernel = builder.Build();

        kernel.ImportPluginFromType<ConversationSummaryPlugin>();
        var prompts = kernel.ImportPluginFromPromptDirectory("Prompts/TravelPlugins");

        ChatHistory history = [];
        
        string input = @"I'm planning a holiday trip with my spouse and 2 girls age 10 and 17. 
        We like eating out, sight seeing, and beaches. Our travel budget is $10000 and we like 
        countries in Europe or the Carribean.";

        var result = await kernel.InvokeAsync<string>(prompts["GetDestination"],
            new() {{ "input", input }});

        result = await kernel.InvokeAsync<string>(prompts["SuggestDestinations"], new() {{ "input", input }});

        Console.WriteLine(result);
        history.AddUserMessage(input);
        history.AddAssistantMessage(result);

        Console.WriteLine("Where would you like to go?");
        input = Console.ReadLine();

        result = await kernel.InvokeAsync<string>(prompts["SuggestActivities"],
            new() {
                { "history", history },
                { "destination", input },
            }
        );
        Console.WriteLine(result);

    }
}