using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Threading.Tasks;
using System.IO;

public class CoordinatorAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<CoordinatorAgent> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public CoordinatorAgent(Kernel kernel, ILoggerFactory loggerFactory)
    {
        _kernel = kernel;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CoordinatorAgent>();
    }

    public async Task ExecuteWorkflowAsync()
    {
        _logger.LogInformation("Starting translation workflow...");

        // Loop to ensure the user can provide input
        while (true)
        {
            Console.WriteLine("Enter text for translation (or file path for code translation):");
            var input = Console.ReadLine();

            // Check if input is valid
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Input cannot be empty. Please try again.");
                continue;
            }

            if (File.Exists(input)) // Assume it's a C# file
            {
                _logger.LogInformation($"Processing file: {input}");

                var tsAgent = new TypeScriptProgrammerArchitectAgent(_kernel, _loggerFactory);
                var result = await tsAgent.TranslateCSharpToTypeScript(input);

                Console.WriteLine($"Translated TypeScript Code:\n{result}");
            }
            else // Assume plain text translation
            {
                _logger.LogInformation("Processing text translation.");

                var engAgent = new EnglishProfessorAgent(_kernel, _loggerFactory);
                var frAgent = new FrenchProfessorAgent(_kernel, _loggerFactory);

                try
                {
                    var refinedEnglish = await engAgent.RefineEnglish(input);
                    var translatedFrench = await frAgent.TranslateToFrench(refinedEnglish);

                    Console.WriteLine($"Translated French:\n{translatedFrench}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during text translation workflow.");
                    Console.WriteLine("An error occurred. Please try again.");
                }
            }

            Console.WriteLine("Do you want to enter more input? (yes/no)");
            var continueInput = Console.ReadLine()?.Trim().ToLower();
            if (continueInput != "yes")
            {
                _logger.LogInformation("Ending translation workflow.");
                break;
            }
        }
    }
}