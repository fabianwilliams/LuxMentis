using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.SemanticKernel;
using System.IO;
using System.Linq;

public class EmailPlugin
{
    private const string FilePath = "data/email.txt"; // Path to the email file

    [KernelFunction, Description("Get all emails")]
    public static string GetEmails()
    {
        string dir = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(dir, FilePath);
        string content = File.ReadAllText(fullPath);
        return content;
    }

    [KernelFunction, Description("Get a list of important unread emails")]
    public static string GetUnreadImportantEmails()
    {
        string dir = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(dir, FilePath);
        string content = File.ReadAllText(fullPath);

        var emails = JsonNode.Parse(content).AsArray();
        var unreadImportantEmails = emails
            .Where(email => email["is_important"].GetValue<bool>() && !email["is_read"].GetValue<bool>());

        return JsonSerializer.Serialize(unreadImportantEmails);
    }

    [KernelFunction, Description("Mark an email as read")]
    public static string MarkEmailAsRead(string person, string subject)
    {
        string dir = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(dir, FilePath);
        string content = File.ReadAllText(fullPath);
        var emails = JsonNode.Parse(content).AsArray();

        foreach (var email in emails)
        {
            if (email["person"].GetValue<string>().ToUpper() == person.ToUpper() &&
                email["email_subject"].GetValue<string>().ToUpper() == subject.ToUpper())
            {
                email["is_read"] = true;
            }
        }

        File.WriteAllText(fullPath, JsonSerializer.Serialize(emails, new JsonSerializerOptions { WriteIndented = true }));
        return $"Marked '{subject.ToUpper()}' from {person.ToUpper()} as read.";
    }
}