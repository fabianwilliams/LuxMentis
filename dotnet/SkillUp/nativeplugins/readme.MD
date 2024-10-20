## Use Functions in Prompts

The templating language of the **Semantic Kernel SDK** allows you to create dynamic prompts. The language supports three key features:

1. Using variables.
2. Calling external functions.
3. Passing arguments to functions.

To embed expressions in your prompts, the templating language uses curly brackets `{{...}}`, and variables are denoted by a dollar sign `$`. The functions you call must be part of the plugins you load into the kernel. 

For example, if you want to call a function within a prompt, you can use the following syntax:

```plaintext
{{plugin.functionName $argument}}
```

You must ensure that the plugin containing the function is loaded into the kernel before you call the function in your prompt. Nesting functions within prompts can help you reduce the number of tokens used in a prompt, or provide additional context to the model for improved results.

## Automatically do Function Calling

The Semantic Kernel SDK allows developers to integrate results from large language models (LLMs) with semantic prompts and native function plugins. While you can manually invoke functions and prompts, the SDK also supports automatic function calling. Automatic function calling can coordinate which plugins and functions to invoke based on the user's request. This feature can help you build adaptive applications that respond intelligently to dynamic user input.

## Automatic Function Calling in the Semantic Kernel SDK

The **Semantic Kernel SDK** supports a powerful automatic function calling feature. Enabling automatic function calling allows the kernel to automatically select which functions and prompts to use on some provided input. This feature can save you time from manually invoking functions and prompts, making your applications smarter.

### Requirements

To use automatic function calling, you must use an OpenAI model that supports it. Currently, these models include **GPT-3.5-turbo** and **GPT-4** models that are version **0613** or newer.

### Enabling Automatic Function Calling

To enable auto function calling, you must set the `ToolCallBehavior` property of the `OpenAIPromptExecutionSettings` object to `AutoInvokeKernelFunctions`.

### Example: Using Ingredient Plugins

Suppose you have some ingredient plugins that support the following functions:

- `GetIngredients`: Gets a list of the user's available ingredients.
- `GetRecipe`: Gets a list of ingredients for a given recipe.
- `GetMissingIngredients`: Gets a list of ingredients that are missing from the user's kitchen for a given recipe.

You can use the `AutoInvokeKernelFunctions` behavior to automatically run a function based on a prompt. 

Here is an example:

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var builder = Kernel.CreateBuilder();
//choose one or the other
var builder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint); //suppression of 0010

// or if using non local version
builder.AddAzureOpenAIChatCompletion(
    "your-deployment-name",
    "your-endpoint",
    "your-api-key",
    "deployment-model");

kernel.ImportPluginFromType<IngredientsPlugin>();
kernel.ImportPluginFromPromptDirectory("Prompts/IngredientPrompts");

// Set the ToolCallBehavior property
OpenAIPromptExecutionSettings settings = new()
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

string prompt = @"What ingredients am I missing from my current list of ingredients 
    to make a recipe for aloo jeera?";

// Use the settings to automatically invoke plugins based on the prompt
var result = await kernel.InvokePromptAsync(prompt, new(settings));

Console.WriteLine(result);
```

The semantic kernel automatically detects the appropriate plugin function to use and passes in the correct parameters. You can try modifying the location to see how the recommendation changes. You can also try changing the prompt to tell the LLM to suggest a song from the library.

The AutoInvokeKernelFunctions setting allows the semantic kernel to automatically call functions and prompts that are added to your kernel. This tool can empower you to create dynamic, robust applications using less code.