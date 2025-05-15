using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace OtelBetter;
public sealed class ExpectedSchemaFunctionFilter : IAutoFunctionInvocationFilter
{
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
