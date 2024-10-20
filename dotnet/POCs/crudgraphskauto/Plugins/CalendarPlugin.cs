using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel;
using System.IO;

public class CalendarPlugin
{
    private const string FilePath = "data/calendar.txt"; // Path to the calendar file

    [KernelFunction, Description("Get all calendar events")]
    public static string GetEvents()
    {
        string dir = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(dir, FilePath);
        string content = File.ReadAllText(fullPath);
        return content;
    }

    [KernelFunction, Description("Add a new calendar event")]
    public static string AddEvent(string person, string title, string details, string date, string timeBlocked)
    {
        string dir = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(dir, FilePath);
        string content = File.ReadAllText(fullPath);
        var events = JsonNode.Parse(content).AsArray();

        var newEvent = new JsonObject
        {
            ["person"] = person.ToUpper(),
            ["event_title"] = title.ToUpper(),
            ["event_details"] = details.ToUpper(),
            ["date"] = date,
            ["time_blocked"] = timeBlocked.ToUpper()
        };

        events.Add(newEvent);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true }));

        return $"Added event '{title.ToUpper()}' for {person.ToUpper()} on {date}";
    }
}