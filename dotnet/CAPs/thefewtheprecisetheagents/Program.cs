using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
//using Microsoft.SemanticKernel.Agents.History;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Plugins.OpenApi.Extensions;

internal class Program
{
    private static IConfiguration? _configuration;
    private static ILogger<Program>? _logger;
    private static BearerAuthenticationProviderWithCancellationToken? _bearerAuthenticationProviderWithCancellationToken;
    
    private static async Task Main(string[] args)
    {
        // Initialize configuration and logging
        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        _configuration = configurationBuilder.Build();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
        _logger = loggerFactory.CreateLogger<Program>();
        var bearerLogger = loggerFactory.CreateLogger<BearerAuthenticationProviderWithCancellationToken>();

        _bearerAuthenticationProviderWithCancellationToken = new BearerAuthenticationProviderWithCancellationToken(_configuration, bearerLogger);

        // Initialize the kernel
        var kernel = await InitializeKernelAsync();

        // Load plugins dynamically
        await LoadPluginsAsync(kernel);

        Console.WriteLine("Defining agents...");

        // Define Agent Names
        const string ChiefOfStaffName = "ChiefOfStaffAgent";
        const string ContactsName = "ContactsAgent";
        const string CalendarName = "CalendarAgent";
        const string EmailName = "EmailAgent";

        // Define Chief of Staff Agent
        ChatCompletionAgent chiefOfStaffAgent = new()
        {
            Name = ChiefOfStaffName,
            Instructions =
                """
                You are the Chief of Staff Agent, responsible for overseeing and orchestrating AI-powered interactions.
                Your goal is to ensure that user queries are **interpreted accurately** and **routed to the appropriate agent**.

                - When a user provides a prompt, you analyze its intent.
                - You assign the task to the appropriate agent (Contacts, Calendar, or Mail).
                - Once an agent refines the request, you review it to ensure it aligns with the user's original intent.
                - You engage in **back-and-forth iteration** with the specialized agents to ensure **accuracy** and **clarity**.
                - You confirm when an agent's refined request is **ready for execution**.

                **Rules:**
                - Always verify the agent's modifications against the original user prompt.
                - Ensure the final request aligns with **API plugin specifications**.
                - Continue iterations until you and the agent reach **agreement**.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
        };

        // Define Specialized Agents (Contacts, Calendar, Email)
        ChatCompletionAgent contactsAgent = new()
        {
            Name = ContactsName,
            Instructions =
                """
                You are the Contacts Agent, responsible for ensuring **contact-related queries** conform to the Contacts API specifications.
                Your role is to validate, refine, and optimize queries for retrieving contacts.
                """,
            Kernel = kernel,
        };

        ChatCompletionAgent calendarAgent = new()
        {
            Name = CalendarName,
            Instructions =
                """
                You are the Calendar Agent, ensuring **calendar-related queries** adhere to the Microsoft Graph Calendar API specifications.
                Your job is to validate and refine calendar queries before execution.
                """,
            Kernel = kernel,
        };

        ChatCompletionAgent emailAgent = new()
        {
            Name = EmailName,
            Instructions =
                """
                You are the Email Agent, ensuring **email-related queries** conform to the Microsoft Graph Mail API.
                Your role is to validate, refine, and optimize queries for sending or retrieving emails.
                """,
            Kernel = kernel,
        };

        // Define Selection Strategy (Which Agent Speaks Next?)
        KernelFunction selectionFunction =
            AgentGroupChat.CreatePromptFunctionForStrategy(
                $$$"""
                Examine the provided RESPONSE and choose the next participant.
                State only the name of the chosen participant without explanation.
                Never choose the participant named in the RESPONSE.

                Choose only from these participants:
                - {{{ContactsName}}}
                - {{{CalendarName}}}
                - {{{EmailName}}}
                - {{{ChiefOfStaffName}}}

                Always follow these rules when choosing the next participant:
                - If RESPONSE is user input, it is the Chief of Staff Agent's turn.
                - If RESPONSE is by the Chief of Staff Agent, analyze the request:
                    - If it involves **contacts**, choose {{{ContactsName}}}.
                    - If it involves **calendar**, choose {{{CalendarName}}}.
                    - If it involves **email**, choose {{{EmailName}}}.
                - If RESPONSE is by a specialized agent (Contacts, Calendar, or Email), it is the Chief of Staff Agent's turn.
                - If Chief of Staff determines the request is fully refined, execution will begin.

                RESPONSE:
                {{$lastmessage}}
                """,
                safeParameterNames: "lastmessage"
            );

        // Define Termination Strategy (When to Stop)
        const string TerminationToken = "yes";

        KernelFunction terminationFunction =
            AgentGroupChat.CreatePromptFunctionForStrategy(
                $$$"""
                Examine the RESPONSE and determine whether the content has been deemed satisfactory.
                If content is satisfactory, respond with a single word without explanation: {{{TerminationToken}}}.
                If specific suggestions are being provided, it is not satisfactory.
                If no correction is suggested, it is satisfactory.

                RESPONSE:
                {{$lastmessage}}
                """,
                safeParameterNames: "lastmessage"
            );

        // Limit chat history to recent message (Optimize Token Usage)
        ChatHistoryTruncationReducer historyReducer = new(1);

        // Define the Agent Group Chat
        AgentGroupChat chat =
            new(chiefOfStaffAgent, contactsAgent, calendarAgent, emailAgent)
            {
                ExecutionSettings = new AgentGroupChatSettings
                {
                    SelectionStrategy =
                        new KernelFunctionSelectionStrategy(selectionFunction, kernel)
                        {
                            // Always start with the Chief of Staff Agent
                            InitialAgent = chiefOfStaffAgent,
                            // Optimize token usage
                            HistoryReducer = historyReducer,
                            // Set prompt variable for tracking
                            HistoryVariableName = "lastmessage",
                            // Extract agent name from result
                            ResultParser = (result) => result.GetValue<string>() ?? chiefOfStaffAgent.Name
                        },
                    TerminationStrategy =
                        new KernelFunctionTerminationStrategy(terminationFunction, kernel)
                        {
                            // Evaluate only for Chief of Staff responses
                            Agents = [chiefOfStaffAgent],
                            // Optimize token usage
                            HistoryReducer = historyReducer,
                            // Set prompt variable for tracking
                            HistoryVariableName = "lastmessage",
                            // Limit total turns to avoid infinite loops
                            MaximumIterations = 12,
                            // Determines if the process should exit
                            ResultParser = (result) =>
                                result.GetValue<string>()?.Contains(TerminationToken, StringComparison.OrdinalIgnoreCase) ?? false
                        }
                }
            };

        await PromptLoopAsync(kernel, chat);
    }

    private static async Task<Kernel> InitializeKernelAsync()
    {
        var kernelBuilder = Kernel.CreateBuilder();

        // Initialize Kernel using OpenAI models
        var apikey = _configuration?["OpenAI:ApiKey"];
        var modelId = _configuration?["OpenAI:ModelId"] ?? "gpt-4o";
        if (string.IsNullOrEmpty(apikey))
        {
            _logger?.LogError("OpenAI API key is not configured.");
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        // Add OpenAI Chat Completion service
        kernelBuilder.AddOpenAIChatCompletion(modelId: modelId, apikey);

        return kernelBuilder.Build();
    }

        private static async Task LoadPluginsAsync(Kernel kernel)
    {
        const string PluginsDirectory = "Plugins/CopilotAgentPlugins";
        if (!Directory.Exists(PluginsDirectory))
        {
            _logger?.LogWarning($"Plugins directory not found: {PluginsDirectory}");
            return;
        }

        foreach (var pluginPath in Directory.GetDirectories(PluginsDirectory))
        {
            var pluginName = Path.GetFileName(pluginPath);
            var manifestFile = Directory.GetFiles(pluginPath, "*-apiplugin.json").FirstOrDefault();

            if (string.IsNullOrEmpty(manifestFile))
            {
                _logger?.LogWarning($"No manifest file found for plugin: {pluginName}. Ensure a file ending with '-apiplugin.json' exists in {pluginPath}.");
                continue;
            }

            try
            {
                var copilotAgentPluginParameters = new CopilotAgentPluginParameters
                {
                    FunctionExecutionParameters = new()
                    {
                        { "https://graph.microsoft.com/v1.0", new OpenApiFunctionExecutionParameters(authCallback: Program._bearerAuthenticationProviderWithCancellationToken.AuthenticateRequestAsync, enableDynamicOperationPayload: false, enablePayloadNamespacing: true) { ParameterFilter = s_restApiParameterFilter} }
                        
                    },
                };
                // Convert manifest path to absolute path
                var manifestPath = Path.GetFullPath(manifestFile);

                if (!File.Exists(manifestPath))
                {
                    _logger?.LogError($"Manifest file not found: {manifestPath}");
                    continue;
                }

                _logger?.LogInformation($"Loading plugin '{pluginName}' from {manifestPath}...");

                // Load the plugin using the correct method
                await kernel.ImportPluginFromCopilotAgentPluginAsync(pluginName, manifestPath, copilotAgentPluginParameters);
                _logger?.LogInformation($"Plugin '{pluginName}' loaded successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to load plugin '{pluginName}' from {manifestFile}.");
            }
        }
    }
    
    private static async Task PromptLoopAsync(Kernel kernel, AgentGroupChat chat)
    {
        Console.WriteLine("Ready!");

        bool isComplete = false;
        do
        {
            Console.WriteLine();
            Console.Write("> ");
            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }
            input = input.Trim();
            if (input.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
            {
                isComplete = true;
                break;
            }

            if (input.Equals("RESET", StringComparison.OrdinalIgnoreCase))
            {
                await chat.ResetAsync();
                Console.WriteLine("[Conversation has been reset]");
                continue;
            }

            // Add user input to the chat history
            chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));

            chat.IsComplete = false;

            try
            {
                await foreach (ChatMessageContent response in chat.InvokeAsync())
                {
                    Console.WriteLine();
                    Console.WriteLine($"{response.AuthorName.ToUpperInvariant()}:{Environment.NewLine}{response.Content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error in chat execution: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine(ex.InnerException.Message);
                }
            }
        } while (!isComplete);
    }

    #region MagicDoNotLookUnderTheHood
    private static readonly HashSet<string> s_fieldsToIgnore = new(
        [
            "@odata.type",
            "attachments",
            "bccRecipients",
            "bodyPreview",
            "categories",
            "ccRecipients",
            "conversationId",
            "conversationIndex",
            "extensions",
            "flag",
            "from",
            "hasAttachments",
            "id",
            "inferenceClassification",
            "internetMessageHeaders",
            "isDeliveryReceiptRequested",
            "isDraft",
            "isRead",
            "isReadReceiptRequested",
            "multiValueExtendedProperties",
            "parentFolderId",
            "receivedDateTime",
            "replyTo",
            "sender",
            "sentDateTime",
            "singleValueExtendedProperties",
            "uniqueBody",
            "webLink",
        ],
        StringComparer.OrdinalIgnoreCase
    );
    private const string RequiredPropertyName = "required";
    private const string PropertiesPropertyName = "properties";
    /// <summary>
    /// Trims the properties from the request body schema.
    /// Most models in strict mode enforce a limit on the properties.
    /// </summary>
    /// <param name="schema">Source schema</param>
    /// <returns>the trimmed schema for the request body</returns>
    private static KernelJsonSchema? TrimPropertiesFromRequestBody(KernelJsonSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        var originalSchema = JsonSerializer.Serialize(schema.RootElement);
        var node = JsonNode.Parse(originalSchema);
        if (node is not JsonObject jsonNode)
        {
            return schema;
        }

        TrimPropertiesFromJsonNode(jsonNode);

        return KernelJsonSchema.Parse(node.ToString());
    }
    private static void TrimPropertiesFromJsonNode(JsonNode jsonNode)
    {
        if (jsonNode is not JsonObject jsonObject)
        {
            return;
        }
        if (jsonObject.TryGetPropertyValue(RequiredPropertyName, out var requiredRawValue) && requiredRawValue is JsonArray requiredArray)
        {
            jsonNode[RequiredPropertyName] = new JsonArray(requiredArray.Where(x => x is not null).Select(x => x!.GetValue<string>()).Where(x => !s_fieldsToIgnore.Contains(x)).Select(x => JsonValue.Create(x)).ToArray());
        }
        if (jsonObject.TryGetPropertyValue(PropertiesPropertyName, out var propertiesRawValue) && propertiesRawValue is JsonObject propertiesObject)
        {
            var properties = propertiesObject.Where(x => s_fieldsToIgnore.Contains(x.Key)).Select(static x => x.Key).ToArray();
            foreach (var property in properties)
            {
                propertiesObject.Remove(property);
            }
        }
        foreach (var subProperty in jsonObject)
        {
            if (subProperty.Value is not null)
            {
                TrimPropertiesFromJsonNode(subProperty.Value);
            }
        }
    }
#pragma warning disable SKEXP0040
    private static readonly RestApiParameterFilter s_restApiParameterFilter = (RestApiParameterFilterContext context) =>
    {
#pragma warning restore SKEXP0040
        if ("me_sendMail".Equals(context.Operation.Id, StringComparison.OrdinalIgnoreCase) &&
            "payload".Equals(context.Parameter.Name, StringComparison.OrdinalIgnoreCase))
        {
            context.Parameter.Schema = TrimPropertiesFromRequestBody(context.Parameter.Schema);
            return context.Parameter;
        }
        return context.Parameter;
    };
    private sealed class ExpectedSchemaFunctionFilter : IAutoFunctionInvocationFilter
    {//TODO: this eventually needs to be added to all CAP or DA but we're still discussing where should those facilitators live
        public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
        {
            await next(context).ConfigureAwait(false);

            if (context.Result.ValueType == typeof(RestApiOperationResponse))
            {
                var openApiResponse = context.Result.GetValue<RestApiOperationResponse>();
                if (openApiResponse?.ExpectedSchema is not null)
                {
                    openApiResponse.ExpectedSchema = null;
                }
            }
        }
    }
    #endregion
}