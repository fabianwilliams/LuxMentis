using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Threading.Tasks;
using System.IO;
using Microsoft.SemanticKernel.Connectors.OpenAI;

public class TypeScriptProgrammerArchitectAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<TypeScriptProgrammerArchitectAgent> _logger;

    public TypeScriptProgrammerArchitectAgent(Kernel kernel, ILoggerFactory loggerFactory)
    {
        _kernel = kernel;
        _logger = loggerFactory.CreateLogger<TypeScriptProgrammerArchitectAgent>();
    }

    public async Task<string> TranslateCSharpToTypeScript(string csharpFilePath)
    {
        _logger.LogInformation($"Translating C# file: {csharpFilePath}");

        // Read the C# code from the file
        var code = File.ReadAllText(csharpFilePath);

        // Define execution settings
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.7, // Adjust temperature for creativity
            MaxTokens = 1000,  // Set max tokens for response length
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions // Automatically use tools as needed
        };

        // Define the prompt
        var prompt = "Convert this C# code to TypeScript:";

        // Execute the prompt with the settings
        var result =  await _kernel.InvokePromptAsync(prompt, new(settings));

        return result.ToString();
    }
}