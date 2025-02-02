using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class ImageProcessor
{
    private static readonly Uri Endpoint = new Uri("http://localhost:11434/api/chat");
    private const string ModelId = "llama3.2-vision:90b";
    private const string InputDirectory = "images"; // Directory where images are stored
    private const string OutputDirectory = "imageresponse"; // Directory for output responses

    public async Task ProcessImageAsync(string imageName)
    {
        // Construct full paths for the input image and output file
        string imagePath = Path.Combine(InputDirectory, imageName);
        string responseOutputPath = Path.Combine(OutputDirectory, $"{Path.GetFileNameWithoutExtension(imageName)}_final_response.txt");

        // Validate that the image file exists
        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"Error: The file {imagePath} does not exist.");
            return;
        }

        try
        {
            // Read the image file and encode it as Base64
            string base64Image;
            using (var imageStream = File.OpenRead(imagePath))
            {
                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream);
                base64Image = Convert.ToBase64String(memoryStream.ToArray());
            }

            // Create the JSON payload
            var payload = new
            {
                model = ModelId,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = "Give me a summary and break down in detail of what is in this image. Use narrative and bullet points?",
                        images = new[] { base64Image }
                    }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

            // Configure HttpClient with a longer timeout
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // Increase the timeout to 5 minutes
            };

            var requestContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(Endpoint, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"HTTP Error: {response.StatusCode}");
                Console.WriteLine("Response body:");
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return;
            }

            // Process chunked response
            Console.WriteLine("Processing chunked response...");
            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(responseStream);

            var finalContent = new StringBuilder();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    try
                    {
                        var chunk = JsonSerializer.Deserialize<JsonElement>(line);
                        if (chunk.TryGetProperty("message", out var message) &&
                            message.TryGetProperty("content", out var chunkContentJson))
                        {
                            string chunkContent = chunkContentJson.GetString();
                            if (!string.IsNullOrEmpty(chunkContent))
                            {
                                finalContent.Append(chunkContent);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Failed to parse chunk: {ex.Message}");
                        Console.WriteLine($"Chunk content: {line}");
                    }
                }
            }

            string finalResponse = finalContent.ToString();
            Console.WriteLine("Final Response:");
            Console.WriteLine(finalResponse);

            // Write the final response to a file
            Directory.CreateDirectory(OutputDirectory); // Ensure the output directory exists
            await File.WriteAllTextAsync(responseOutputPath, finalResponse);
            Console.WriteLine($"Final response written to: {responseOutputPath}");
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine("Error: The request timed out.");
            Console.WriteLine($"Details: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }
}