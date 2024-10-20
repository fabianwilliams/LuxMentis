using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel;
using System.IO;

public class ToDoPlugin
{
    private const string FilePath = "data/todo.txt"; // Path to the ToDo file

    [KernelFunction, Description("Get all ToDo tasks")]
    public static string GetTasks()
    {
        string dir = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(dir, FilePath);
        string content = File.ReadAllText(fullPath);
        return content;
    }

    [KernelFunction, Description("Add a new ToDo task")]
    public static string AddTask(string person, string task, string details)
    {
        string dir = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(dir, FilePath);
        string content = File.ReadAllText(fullPath);
        var tasks = JsonNode.Parse(content).AsArray();

        var newTask = new JsonObject
        {
            ["person"] = person.ToUpper(),
            ["task"] = task.ToUpper(),
            ["task_details"] = details.ToUpper(),
            ["date"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["is_complete"] = false
        };

        tasks.Add(newTask);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true }));

        return $"Added task '{task.ToUpper()}' for {person.ToUpper()}";
    }
}