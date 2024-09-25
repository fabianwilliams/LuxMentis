using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;

#pragma warning disable SKEXP0050, SKEXP0001, SKEXP0010, SKEXP0003, SKEXP0011, SKEXP0052, SKEXP0055 // Experimental

internal class Program
{
    private static async Task Main(string[] args)
    {
        var endpoint = new Uri("http://localhost:11434");
        var modelId = "llama3.1:70b";
        string embeddingModelName = "nomic-embed-text";  // Embedding model

        // Create Kernel with OpenAI Chat Completion and Memory store
        var kernelBuilder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint);

        // Configure logging and memory store
        kernelBuilder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Trace));
        kernelBuilder.Services.AddVolatileVectorStore(); // Volatile in-memory vector store

        // Build the kernel with memory
        var kernel = kernelBuilder.Build();
        //Kernel kernel = kernelBuilder.WithMemoryStorage(new VolatileMemoryStore()).Build();


        // Example usage
        var logger = kernel.LoggerFactory.CreateLogger(typeof(Program));
        logger.LogInformation("Running the kernel to test Memory.");

        string collectionName = "GraphConnector";

        try
        {
            // Configure the semantic memory
            ISemanticTextMemory memory = new MemoryBuilder()
                .WithLoggerFactory(kernel.LoggerFactory)
                .WithOpenAITextEmbeddingGeneration(embeddingModelName, null)
                .Build();

            using (HttpClient client = new())
            {
                // Fetch and process the content
                string s = await client.GetStringAsync("https://devblogs.microsoft.com/microsoft365dev/oh-to-be-a-dev-at-the-microsoft-365-community-conference/");
                IList paragraphs = TextChunker.SplitPlainTextParagraphs(
                    TextChunker.SplitPlainTextLines(
                        WebUtility.HtmlDecode(Regex.Replace(s, @"<[^>]+>|&nbsp;", "")),
                        128),
                    1024);

                // Save paragraphs into memory
                for (int i = 0; i < paragraphs.Count; i++)
                {
                    await memory.SaveInformationAsync(collectionName, (string)paragraphs[i], $"paragraph{i}");
                }
            }

            // Create a new chat
            var ai = kernel.GetRequiredService<IChatCompletionService>();
            ChatHistory chat = new("You are an AI assistant that helps people find information.");
            StringBuilder builder = new();

            // Q&A loop
            while (true)
            {
                Console.Write("Question: ");
                string question = Console.ReadLine()!;

                builder.Clear();
                await foreach (var result in memory.SearchAsync(collectionName, question, limit: 3))
                    builder.AppendLine(result.Metadata.Text);
                
                int contextToRemove = -1;
                if (builder.Length != 0)
                {
                    builder.Insert(0, "Here's some additional information: ");
                    contextToRemove = chat.Count;
                    chat.AddUserMessage(builder.ToString());
                }

                chat.AddUserMessage(question);

                builder.Clear();
                await foreach (var message in ai.GetStreamingChatMessageContentsAsync(chat))
                {
                    Console.Write(message);
                    builder.Append(message.Content);
                }
                Console.WriteLine();
                chat.AddAssistantMessage(builder.ToString());

                if (contextToRemove >= 0) chat.RemoveAt(contextToRemove);
                Console.WriteLine();

                logger.LogInformation("Result: Pass");
            }

        }
        catch (Exception ex)
        {
            logger.LogError("Error: {Message}", ex.Message);
        }
    }
}
