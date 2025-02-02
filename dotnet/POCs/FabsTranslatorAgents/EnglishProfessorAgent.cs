using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Threading.Tasks;

public class EnglishProfessorAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<EnglishProfessorAgent> _logger;

    public EnglishProfessorAgent(Kernel kernel, ILoggerFactory loggerFactory)
    {
        _kernel = kernel;
        _logger = loggerFactory.CreateLogger<EnglishProfessorAgent>();
    }

    public async Task<string> RefineEnglish(string text)
    {
        _logger.LogInformation("Refining English text.");

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.5,
            MaxTokens = 500,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var prompt = "Refine this English text for clarity:";

        // Execute the prompt with the settings
        var result = await _kernel.InvokePromptAsync(prompt, new(settings));

        return result.ToString();
    }
}