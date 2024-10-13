using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Qdrant.Client;
using Qdrant.Client.Grpc;

class Program
{
    static async Task Main(string[] args)
    {
        while (true)
        {
            Console.WriteLine("Ask a question (or type 'exit' to quit):");
            string searchQuery = Console.ReadLine();

            if (searchQuery.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            // Step 1: Generate embedding for your search query
            var searchEmbedding = await GenerateEmbeddingForQueryAsync(searchQuery);

            // Step 2: Perform semantic search in Qdrant
            var searchResults = await PerformQdrantVectorSearchAsync(searchEmbedding);

            // Step 3: If results are found, use Chat Completion to generate a summary
            if (searchResults.Any())
            {
                Console.WriteLine("Search results found:");
                foreach (var result in searchResults)
                {
                    Console.WriteLine($"Score: {result.Score}");
                    Console.WriteLine($"Chunk: {result.Payload["chunk"]}");
                    Console.WriteLine($"URL: {result.Payload["url"]}");
                }

                // Use the results to generate a structured summary via chat completion
                string summary = await GenerateSummaryFromResultsWithSystemPromptAsync(searchResults);
                Console.WriteLine("\nChat Completion Summary:");
                Console.WriteLine(summary);
            }
            else
            {
                Console.WriteLine("No matching results found.");
            }
        }
    }

    // Step 1: Method to generate an embedding for the search query using Ollama
    static async Task<float[]> GenerateEmbeddingForQueryAsync(string query)
    {
        IEmbeddingGenerator<string, Embedding<float>> generator =
            new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"), "rjmalagon/gte-qwen2-1.5b-instruct-embed-f16:latest");

        var embedding = await generator.GenerateAsync(query);
        return embedding.First().Vector.ToArray(); // Return the embedding vector
    }

    // Step 2: Perform semantic search using Qdrant
    static async Task<List<ScoredPoint>> PerformQdrantVectorSearchAsync(float[] searchEmbedding)
    {
        var channel = QdrantChannel.ForAddress("http://localhost:6334");
        var grpcClient = new QdrantGrpcClient(channel);
        var client = new QdrantClient(grpcClient);

        var searchResults = await client.SearchAsync(
            "fringetv_embeddings_1536",  // Your Qdrant collection name
            searchEmbedding,
            limit: 5  // Limit to top 5 results
        );

        return searchResults.ToList();
    }

    // Step 3: Generate a structured summary via chat completion with a system prompt
    private static async Task<string> GenerateSummaryFromResultsWithSystemPromptAsync(List<ScoredPoint> searchResults)
    {
        IChatClient client = new OllamaChatClient(new Uri("http://localhost:11434/"), "llama3.1:70b");

        // Provide a system prompt to instruct the model on how to summarize the results
        string systemPrompt = @"
            You are a helpful and conversational assistant.
            Summarize the content in a friendly and engaging tone.
            Avoid referencing the source material or actors by their real names.
            Focus on the characters' roles and their relevance to the topic.
            Keep the summary concise, under 100 words, and easy to understand for someone unfamiliar with the show.
        ";

        // Collect the text chunks from the search results
        string inputContent = string.Join("\n\n", searchResults.Select(r => r.Payload["chunk"].ToString()));

        // Add the system prompt to guide the completion
        string prompt = $"{systemPrompt}\nHere is the content to summarize:\n{inputContent}";

        var response = await client.CompleteAsync(prompt);
        return response.Message.ToString();
    }
}