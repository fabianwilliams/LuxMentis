using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;

// Disable specific warnings
#pragma warning disable SKEXP0010, SKEXP0050, SKEXP0001

var endpoint = new Uri("http://localhost:11434");
var modelId = "llama3.2:1b";

// Build the kernel and import plugins
var builder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: modelId, apiKey: null, endpoint: endpoint);

var kernel = builder.Build();

// Import plugins for emails, calendar, and todo tasks
kernel.ImportPluginFromType<EmailPlugin>();
kernel.ImportPluginFromType<CalendarPlugin>();
kernel.ImportPluginFromType<ToDoPlugin>();

// Load data from plugins
string importantEmails = EmailPlugin.GetUnreadImportantEmails();  // Get unread important emails
string incompleteTasks = ToDoPlugin.GetTasks();  // Get incomplete tasks
string blockedTime = CalendarPlugin.GetEvents();  // Get blocked time for tomorrow

// Create the prompt with loaded data
string prompt = $@"
Hey, I'm about to end my day and I know I haven't finished everything.
Let's set up the next day. For the people I work with the most, let's check to see if I have any important unread emails. 
Let's create a ToDo task for those and block some time in my calendar to address them.

Unread important emails:
{importantEmails}

Incomplete tasks:
{incompleteTasks}

Blocked time for tomorrow:
{blockedTime}

After performing these actions, please provide a detailed summary of the following:
1. Emails that were marked as read
2. ToDo tasks that were created
3. Calendar events that were blocked.
";

// Set up OpenAI prompt execution settings for auto-invoking kernel functions
OpenAIPromptExecutionSettings settings = new()
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

var result = await kernel.InvokePromptAsync(prompt, new(settings));
Console.WriteLine("Summary of Actions Taken:");
Console.WriteLine(result);