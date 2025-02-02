using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Threading.Tasks;

public class FrenchProfessorAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<FrenchProfessorAgent> _logger;

    public FrenchProfessorAgent(Kernel kernel, ILoggerFactory loggerFactory)
    {
        _kernel = kernel;
        _logger = loggerFactory.CreateLogger<FrenchProfessorAgent>();
    }

    public async Task<string> TranslateToFrench(string englishText)
    {
        _logger.LogInformation("Translating English to French.");

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.6,
            MaxTokens = 1000,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var prompt = "Translate this English text to French:";

        // Execute the prompt with the settings
        var result = await _kernel.InvokePromptAsync(prompt, new(settings));

        return result.ToString();
    }
}