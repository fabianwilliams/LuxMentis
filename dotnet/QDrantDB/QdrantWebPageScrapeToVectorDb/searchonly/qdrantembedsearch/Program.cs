using Qdrant.Client;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Qdrant.Client.Grpc;
using System.Collections.Generic;

namespace WebPageEmbeddingSearch
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // The term you want to search for (using partial match)
            var searchTerm = "Walternate";  // Adjusted for partial matches

            // Generate embedding for the search term
            var searchEmbedding = await GenerateEmbeddingForQueryAsync(searchTerm);

            // Search Qdrant for similar embeddings (with pagination and filters)
            await SearchQdrantWithTop5PartialMatchesAsync(searchEmbedding, searchTerm);
        }

        // Method to generate an embedding for the query term using Ollama
        static async Task<float[]> GenerateEmbeddingForQueryAsync(string query)
        {
            IEmbeddingGenerator<string, Embedding<float>> generator =
                //new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"), "all-minilm:33m");
                //new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"),"mxbai-embed-large:latest");
                new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"),"rjmalagon/gte-qwen2-1.5b-instruct-embed-f16:latest"); //Needed for 1536 dimensions which is what Semantic Kernel needs


            var embedding = await generator.GenerateAsync(query);

            // Print the embedding vector for debugging
            Console.WriteLine($"Generated embedding for '{query}':");
            Console.WriteLine(string.Join(", ", embedding.First().Vector));  // Display the embedding vector

            return embedding.First().Vector.ToArray(); // Return the embedding vector
        }

        // Method to search Qdrant for top 5 results with partial matches
        static async Task SearchQdrantWithTop5PartialMatchesAsync(float[] embedding, string searchTerm)
        {
            // Connect to Qdrant (local instance)
            var channel = QdrantChannel.ForAddress("http://localhost:6334");
            var grpcClient = new QdrantGrpcClient(channel);
            var client = new QdrantClient(grpcClient);

            int limit = 20;  // Fetch more results to filter later
            int offset = 0;  // Start from the beginning
            var allResults = new List<(float Score, string Chunk, string Url, bool Found)>();  // Store all results

            // Loop to retrieve results with pagination (keep going until no more results are found)
            while (true)
            {
                // Search Qdrant for the closest points to the search term embedding
                var results = await client.SearchAsync(
                    "fringetv_embeddings_1536",  // The collection where embeddings are stored alternat between 384 and 1024 and 1536dimensions
                    embedding,              // The query embedding vector
                    limit: (ulong)limit,           // Limit for pagination
                    offset: (ulong)offset          // Offset to paginate
                );

                // Check if any results were returned
                if (results == null || !results.Any())
                {
                    break;  // Break loop if no more results are found
                }

                foreach (var result in results)
                {
                    if (result.Payload.ContainsKey("chunk"))
                    {
                        var chunk = result.Payload["chunk"].ToString();
                        var url = result.Payload.ContainsKey("url") ? result.Payload["url"].ToString() : "N/A";

                        // Check for a partial match with the search term (case insensitive)
                        bool found = chunk.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
                        allResults.Add((result.Score, chunk, url, found));  // Add result to list (mark whether found or not)
                    }
                }

                // Increase the offset by the limit for the next iteration
                offset += limit;
            }

            // Sort results by score and prioritize those with matches
            var sortedResults = allResults
                .OrderByDescending(r => r.Found)      // Prioritize found results
                .ThenByDescending(r => r.Score)       // Sort by score
                .Take(10)                              // Limit to top 10
                .ToList();

            // Output the top 5 results
            if (sortedResults.Any())
            {
                Console.WriteLine($"Top 5 search results for '{searchTerm}' (partial matches allowed):");

                foreach (var result in sortedResults)
                {
                    Console.WriteLine($"Score: {result.Score}");
                    Console.WriteLine($"Chunk: {result.Chunk.ToUpper()}");
                    Console.WriteLine($"URL: {result.Url}");
                    if (result.Found)
                    {
                        Console.WriteLine($"*** Search term '{searchTerm}' found in this chunk! ***");
                    }
                    Console.WriteLine();
                }
            }
            else
            {
                // If no results found at all
                Console.WriteLine($"No relevant results found for '{searchTerm}'.");
            }
        }
    }
}