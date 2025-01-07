using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.SemanticKernel.ChatCompletion;

public class CoordinatorAgent
{
    private readonly Kernel _kernel;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ChatCompletionAgent _youtubeAgent;
    private readonly ChatCompletionAgent _blogAgent;
    private readonly ChatCompletionAgent _socialAgent;
    private readonly ChatCompletionAgent _technicalReviewer;
    private readonly ChatCompletionAgent _copyEditReviewer;

    public CoordinatorAgent(Kernel kernel, ILoggerFactory loggerFactory)
    {
        _kernel = kernel;
        _loggerFactory = loggerFactory;

        // Initialize agents with the logger factory
        _youtubeAgent = CreateYouTubeAgent();
        _blogAgent = CreateBlogAgent();
        _socialAgent = CreateSocialMediaAgent();
        _technicalReviewer = CreateTechnicalReviewer();
        _copyEditReviewer = CreateCopyEditReviewer();
    }

    public async Task ExecuteWorkflowAsync(string projectRepoName)
    {
        var logger = _loggerFactory.CreateLogger<CoordinatorAgent>();

        string transcriptPath = $"/Users/fabswill/Repos/fabianstaticbravo/data/transcripts/{projectRepoName}/{projectRepoName}.docx";
        if (!File.Exists(transcriptPath))
        {
            logger.LogError("Transcript not found: {0}", transcriptPath);
            return;
        }

        string transcript = ExtractTextFromWord(transcriptPath);

        // Step 1: Generate YouTube Content
        string title = await GenerateYouTubeTitle(transcript);
        string description = await GenerateYouTubeDescription(transcript);
        string keywords = await GenerateYouTubeKeywords(transcript);

        logger.LogInformation("Generated YouTube Title: {0}", title);
        logger.LogInformation("Generated YouTube Description: {0}", description);
        
        SaveYouTubeDraft(projectRepoName, title, description, keywords);

        // Step 2: Draft Blog Post with Transcript
        string initialDraft = await DraftBlogPost(projectRepoName, transcript);

        logger.LogInformation("Initial Blog Draft Created.");

        // Step 3: Run Review Cycle (With 2 Iterations)
        var reviewChat = new AgentGroupChat(_technicalReviewer, _copyEditReviewer)
        {
            LoggerFactory = _loggerFactory,
            ExecutionSettings = new()
            {
                TerminationStrategy = new ApprovalTerminationStrategy(
                    _loggerFactory.CreateLogger<ApprovalTerminationStrategy>(), 
                    maxIterations: 2
                )
            }
        };

        reviewChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, initialDraft));

        await foreach (var response in reviewChat.InvokeAsync())
        {
            logger.LogInformation("Review Cycle Response: {0}", response.Content);
        }

        SaveBlogPost(projectRepoName, initialDraft);

        // Step 4: Generate Social Media Post with Transcript
        string socialPost = await GenerateSocialMediaPost(projectRepoName, transcript);
        SaveSocialDraft(projectRepoName, socialPost);

        logger.LogInformation("Workflow completed successfully for {0}", projectRepoName);
    }

    // YouTube Content Generation
    private async Task<string> GenerateYouTubeTitle(string transcript) =>
        (await _youtubeAgent.Kernel.InvokePromptAsync($"Generate a YouTube title for this:\n{transcript}"))
        .GetValue<string>() ?? "Default Title";

    private async Task<string> GenerateYouTubeDescription(string transcript) =>
        (await _youtubeAgent.Kernel.InvokePromptAsync($"Summarize for YouTube description:\n{transcript}"))
        .GetValue<string>() ?? "Default Description";

    private async Task<string> GenerateYouTubeKeywords(string transcript) =>
        (await _youtubeAgent.Kernel.InvokePromptAsync($"Extract SEO keywords from this:\n{transcript}"))
        .GetValue<string>() ?? "Default Keywords";

    // Blog Draft Generation
    private async Task<string> DraftBlogPost(string projectRepoName, string transcript) =>
        (await _blogAgent.Kernel.InvokePromptAsync($"""
            Draft a detailed blog post based on this transcript:
            {transcript}
            
            Ensure the blog post references the YouTube video titled '{projectRepoName}' and provides a summary of the key takeaways.
            Include relevant keywords to enhance SEO.
        """)).GetValue<string>() ?? "Blog draft not available.";

    // Social Media Post Generation
    private async Task<string> GenerateSocialMediaPost(string projectRepoName, string transcript) =>
        (await _socialAgent.Kernel.InvokePromptAsync($"""
            Draft a concise and engaging social media post to promote the YouTube video for {projectRepoName}.
            The post should highlight key takeaways from this transcript:
            {transcript}
            
            Use a tone suitable for platforms like Twitter or LinkedIn.
            Include relevant hashtags and a link to the video.
        """)).GetValue<string>() ?? "Default Social Media Post";

    // Review Cycle
    private async Task RunReviewCycle(string initialDraft, string projectRepoName)
    {
        var logger = _loggerFactory.CreateLogger<CoordinatorAgent>();
        var reviewChat = new AgentGroupChat(_technicalReviewer, _copyEditReviewer)
        {
            LoggerFactory = _loggerFactory,
            ExecutionSettings = new()
            {
                TerminationStrategy = new ApprovalTerminationStrategy(
                    _loggerFactory.CreateLogger<ApprovalTerminationStrategy>(), 
                    maxIterations: 2
                )
            }
        };

        reviewChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, initialDraft));

        await foreach (var response in reviewChat.InvokeAsync())
        {
            logger.LogInformation(response.Content);
        }

        SaveBlogPost(projectRepoName, initialDraft);
    }

    // Agent Creation
    private ChatCompletionAgent CreateYouTubeAgent() => new()
    {
        Instructions = "Generate engaging YouTube titles, descriptions, and keywords.",
        Name = "YouTubeAgent",
        Kernel = _kernel,
        LoggerFactory = _loggerFactory
    };

    private ChatCompletionAgent CreateBlogAgent() => new()
    {
        Instructions = "Generate blog posts based on transcripts, linking to YouTube for engagement.",
        Name = "BlogAgent",
        Kernel = _kernel,
        LoggerFactory = _loggerFactory
    };

    private ChatCompletionAgent CreateSocialMediaAgent() => new()
    {
        Instructions = "Draft short, engaging social media posts driving traffic to YouTube.",
        Name = "SocialAgent",
        Kernel = _kernel,
        LoggerFactory = _loggerFactory
    };

    private ChatCompletionAgent CreateTechnicalReviewer() => new()
    {
        Instructions = "Review for technical accuracy.",
        Name = "TechnicalReviewer",
        Kernel = _kernel,
        LoggerFactory = _loggerFactory
    };

    private ChatCompletionAgent CreateCopyEditReviewer() => new()
    {
        Instructions = "Review and improve grammar, tone, and style.",
        Name = "CopyEditReviewer",
        Kernel = _kernel,
        LoggerFactory = _loggerFactory
    };

    // Utility Methods
    private string ExtractTextFromWord(string filePath)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        return doc.MainDocumentPart?.Document.Body.InnerText ?? "Transcript not available.";
    }

    // Save Methods (Added to Fix Errors)
    private void SaveYouTubeDraft(string projectRepoName, string title, string description, string keywords)
    {
        string path = $"/Users/fabswill/Repos/fabianstaticbravo/youtube_drafts/";
        Directory.CreateDirectory(path);
        File.WriteAllText($"{path}{projectRepoName}_title.txt", title);
        File.WriteAllText($"{path}{projectRepoName}_desc.txt", description);
        File.WriteAllText($"{path}{projectRepoName}_keywords.txt", keywords);
    }

    private void SaveBlogPost(string projectRepoName, string content)
    {
        string filePath = $"/Users/fabswill/Repos/fabianstaticbravo/content/blog/generated_{projectRepoName}_{DateTime.Now:yyyyMMdd}.md";
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
        File.WriteAllText(filePath, content);
    }

    private void SaveSocialDraft(string projectRepoName, string content)
    {
        string filePath = $"/Users/fabswill/Repos/fabianstaticbravo/social_drafts/twitter_{projectRepoName}.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? string.Empty);
        File.WriteAllText(filePath, content);
    }
}