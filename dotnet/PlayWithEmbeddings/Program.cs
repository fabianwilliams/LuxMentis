using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.Numerics.Tensors;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Set up the API URL and the new model name
        string apiUrl = "http://localhost:11434/api/embeddings";
        string modelName = "nomic-embed-text";  // New model

        // Input text
        string input = "What is Jamaica?";

        // Example sentences
        string[] examples =
        {
            "Jamaica is an island nation in the Caribbean.",
            "Jamaica is known for its beautiful beaches and reggae music.",
            "Bob Marley is one of Jamaica's most famous musicians.",
            "The capital of Jamaica is Kingston.",
            "Montego Bay is a popular tourist destination in Jamaica.",
            "The Caribbean region has many islands, including Jamaica, the Bahamas, and Barbados.",
            "What are the best islands to visit in the Caribbean?",
            "Hawaii is not part of the Caribbean, but it is an island in the Pacific Ocean.",
            "Puerto Rico is an island in the Caribbean.",
            "Jamaica is known for its lush, green mountains and coffee plantations.",
        };

        // Generate embeddings for the input text using the new model
        var inputEmbedding = await GenerateEmbedding(apiUrl, modelName, input);
        var exampleEmbeddings = new List<float[]>();

        // Generate embeddings for each example
        foreach (var example in examples)
        {
            var embedding = await GenerateEmbedding(apiUrl, modelName, example);
            exampleEmbeddings.Add(embedding);
        }

        // Calculate cosine similarity between the input and each example
        float[] similarities = exampleEmbeddings
            .Select(e => TensorPrimitives.CosineSimilarity(new ReadOnlySpan<float>(e), new ReadOnlySpan<float>(inputEmbedding)))
            .ToArray();

        // Sort the examples by similarity (ascending by default)
        Array.Sort(similarities, examples);

        // Reverse both arrays to get descending order
        Array.Reverse(similarities);  
        Array.Reverse(examples); 

        // Print the similarity results
        Console.WriteLine("Similarity with examples:");
        for (int i = 0; i < similarities.Length; i++)
        {
            Console.WriteLine($"{similarities[i]:F6}   {examples[i]}");
        }

    }

    // Function to generate embedding using the API
    private static async Task<float[]> GenerateEmbedding(string apiUrl, string modelName, string prompt)
    {
        // Create JSON payload
        var jsonPayload = new
        {
            model = modelName,
            prompt = prompt
        };

        // Serialize the payload to JSON
        string jsonString = JsonSerializer.Serialize(jsonPayload);

        // Create HttpClient to send the request
        using HttpClient client = new HttpClient();

        // Send POST request to the local embedding service
        HttpContent content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync(apiUrl, content);

        // Ensure the request was successful
        response.EnsureSuccessStatusCode();

        // Read and parse the response
        string responseBody = await response.Content.ReadAsStringAsync();
        var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseBody);

        // Return the embedding array
        return embeddingResponse.embedding;
    }

    // Class to model the API response (assuming JSON structure contains an "embedding" array)
    public class EmbeddingResponse
    {
        public float[] embedding { get; set; }
    }
}
