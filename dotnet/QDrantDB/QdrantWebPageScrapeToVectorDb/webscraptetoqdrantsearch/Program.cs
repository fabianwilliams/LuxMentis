using Qdrant.Client;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.AI;
using System.IO;
using System.Collections.Generic;
using Qdrant.Client.Grpc;

namespace WebPageEmbeddingToQdrant
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // URL of the web page you want to read
            var url = "https://en.wikipedia.org/wiki/Fringe_(TV_series)";

            // Create an instance of HttpClient
            using var httpClient = new HttpClient();

            try
            {
                // Fetch the web page content
                var htmlContent = await httpClient.GetStringAsync(url);

                // Parse and extract text from the HTML
                var textContent = ExtractTextFromHtml(htmlContent);

                // Generate embeddings from the extracted text and upsert them into Qdrant
                await UpsertEmbeddingsToQdrantAsync(textContent, url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        // Method to extract text from HTML using HtmlAgilityPack
        static string ExtractTextFromHtml(string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Remove script and style nodes
            htmlDoc.DocumentNode.Descendants()
                .Where(n => n.Name == "script" || n.Name == "style")
                .ToList()
                .ForEach(n => n.Remove());

            // Extract the inner text
            var text = htmlDoc.DocumentNode.InnerText;

            // Clean up the text
            var cleanText = HtmlEntity.DeEntitize(text);
            cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"\s+", " ").Trim();

            return cleanText;
        }

        // Method to generate embeddings and upsert them to Qdrant
        static async Task UpsertEmbeddingsToQdrantAsync(string text, string url)
        {
            // Create Qdrant client (no authentication, local server)
            var channel = QdrantChannel.ForAddress("http://localhost:6334");
            var grpcClient = new QdrantGrpcClient(channel);
            var client = new QdrantClient(grpcClient);

            // Ensure the collection exists
            await client.CreateCollectionAsync("fringetv_embeddings",
                new VectorParams { Size = 384, Distance = Distance.Cosine });

            // Generate embeddings using Ollama
            IEmbeddingGenerator<string, Embedding<float>> generator =
                new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"), "all-minilm:33m");

            // Split the text into chunks for embedding generation
            var chunks = SplitTextIntoChunks(text, maxChunkSize: 1000);
            var random = new Random();
            var points = new List<PointStruct>();

            foreach (var (chunk, index) in chunks.Select((value, index) => (value, index)))
            {
                var embeddings = await generator.GenerateAsync(chunk);

                foreach (var embedding in embeddings)
                {
                    var embeddingArray = embedding.Vector.ToArray();

                    // Create point with embedding vector and metadata
                    var point = new PointStruct
                    {
                        Id = (ulong)index + (ulong)random.Next(), // Ensure unique IDs
                        Vectors = embeddingArray,
                        Payload = {
                            ["url"] = url,
                            ["chunk"] = chunk.Substring(0, Math.Min(chunk.Length, 50)) + "...", // Metadata about the chunk
                            ["index"] = index
                        }
                    };
                    points.Add(point);
                }
            }

            // Upsert the embeddings into Qdrant
            var updateResult = await client.UpsertAsync("fringetv_embeddings", (IReadOnlyList<Qdrant.Client.Grpc.PointStruct>)points);
            Console.WriteLine($"Embeddings for {url} upserted to Qdrant.");
        }

        // Helper method to split text into manageable chunks
        static IEnumerable<string> SplitTextIntoChunks(string text, int maxChunkSize)
        {
            for (int i = 0; i < text.Length; i += maxChunkSize)
            {
                yield return text.Substring(i, Math.Min(maxChunkSize, text.Length - i));
            }
        }
    }
}