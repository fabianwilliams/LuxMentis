using Microsoft.Extensions.AI;

IChatClient client = new OllamaChatClient(new Uri("http://localhost:11434/v1"), "llama3.3:70b");

var response = await client.CompleteAsync("What is Microsoft 365 Copilot? give me in no more than 2 sentences and no more than 5 bullet points");

Console.WriteLine(response.Message);
