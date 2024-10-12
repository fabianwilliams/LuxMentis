using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.AI;

namespace WebPageEmbedding
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // URL of the web page you want to read
            var url = "https://aiforoverfiftyplus.com/about.html";

            // Create an instance of HttpClient
            using var httpClient = new HttpClient();

            try
            {
                // Fetch the web page content
                var htmlContent = await httpClient.GetStringAsync(url);

                // Parse and extract text from the HTML
                var textContent = ExtractTextFromHtml(htmlContent);

                // Generate embeddings from the extracted text
                await GenerateEmbeddingsAsync(textContent);
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
        static async Task GenerateEmbeddingsAsync(string text)
        {
            IEmbeddingGenerator<string, Embedding<float>> generator =
                new OllamaEmbeddingGenerator(new Uri("http://localhost:11434/"), "all-minilm:33m");

            // For large texts, you may want to split the text into smaller chunks
            var chunks = SplitTextIntoChunks(text, maxChunkSize: 1000);

            // Define the path to the file where you want to save embeddings
            string filePath = "embeddings_output.txt";

            // Overwrite the file by opening it in write mode
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                foreach (var chunk in chunks)
                {
                    var embeddings = await generator.GenerateAsync(chunk);

                    // Output the embeddings
                    foreach (var embedding in embeddings)
                    {
                        var embeddingArray = embedding.Vector.ToArray();
                        foreach (var value in embeddingArray)
                        {
                            // Write each float value to the file
                            await writer.WriteLineAsync(value.ToString());
                        }
                    }
                }
            }

            Console.WriteLine($"Embeddings saved to {filePath}");
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