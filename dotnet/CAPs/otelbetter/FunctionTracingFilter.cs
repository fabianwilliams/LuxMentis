using Microsoft.SemanticKernel;
using OpenTelemetry.Trace;
using System.Diagnostics;

public class FunctionTracingFilter : IFunctionInvocationFilter
{
    private readonly ActivitySource _activitySource;

    public FunctionTracingFilter(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        using var activity = _activitySource.StartActivity("SKFunction", ActivityKind.Internal);
        // ... same code
    }
}
