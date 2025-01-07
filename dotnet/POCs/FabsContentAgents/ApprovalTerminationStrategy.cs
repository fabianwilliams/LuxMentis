using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.Extensions.Logging;

public class ApprovalTerminationStrategy : TerminationStrategy
{
    private readonly int _maxIterations;
    private readonly List<string> _approvalKeywords;
    private int _currentIteration;
    private readonly ILogger<ApprovalTerminationStrategy> _logger;

    public ApprovalTerminationStrategy(
        ILogger<ApprovalTerminationStrategy> logger, 
        int maxIterations = 2, 
        List<string>? approvalKeywords = null)
    {
        _logger = logger;
        _maxIterations = maxIterations;
        _approvalKeywords = approvalKeywords ?? new List<string> { "approve", "approved", "complete" };
        _currentIteration = 0;
    }

    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent, 
        IReadOnlyList<ChatMessageContent> history, 
        CancellationToken cancellationToken)
    {
        _currentIteration++;

        var lastMessage = history.LastOrDefault()?.Content ?? string.Empty;
        bool containsApproval = _approvalKeywords.Any(keyword => 
            lastMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase)
        );

        bool iterationLimitReached = _currentIteration >= _maxIterations;

        if (containsApproval)
        {
            _logger.LogInformation("Termination triggered by approval keyword.");
            return Task.FromResult(true);
        }

        if (iterationLimitReached)
        {
            _logger.LogWarning("Termination triggered by iteration limit (2 cycles).");
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}