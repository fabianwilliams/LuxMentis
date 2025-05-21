using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OtelBetter
{
    public static class ActivityHelper
    {
        public static IDisposable StartActivityWithLogging(
            ActivitySource source,
            string activityName,
            ILogger logger,
            ActivityKind kind = ActivityKind.Internal)
        {
            var activity = source.StartActivity(activityName, kind);

            if (activity == null)
            {
                logger.LogWarning("Could not start activity {ActivityName}", activityName);
                return DummyDisposable.Instance;
            }

            logger.LogInformation("▶️ Started {ActivityName} [TraceId: {TraceId}, SpanId: {SpanId}]",
                activityName,
                activity.TraceId,
                activity.SpanId);

            return new ActivityScope(activity, activityName, logger);
        }

        private class ActivityScope : IDisposable
        {
            private readonly Activity _activity;
            private readonly string _name;
            private readonly ILogger _logger;

            public ActivityScope(Activity activity, string name, ILogger logger)
            {
                _activity = activity;
                _name = name;
                _logger = logger;
            }

            public void Dispose()
            {
                _logger.LogInformation("⏹ Ended {ActivityName} [TraceId: {TraceId}, SpanId: {SpanId}]",
                    _name,
                    _activity.TraceId,
                    _activity.SpanId);
                _activity?.Dispose();
            }
        }

        private class DummyDisposable : IDisposable
        {
            public static DummyDisposable Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
