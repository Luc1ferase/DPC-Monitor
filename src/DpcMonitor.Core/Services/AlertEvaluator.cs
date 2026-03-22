using DpcMonitor.Core.Models;

namespace DpcMonitor.Core.Services;

public sealed class AlertEvaluator
{
    public AlertEvaluationResult Evaluate(DpcLatencyBucket bucket, double thresholdUs, MonitorState previousState)
    {
        if (bucket.CurrentUs > thresholdUs)
        {
            return new AlertEvaluationResult(
                MonitorState.Alerting,
                $"DPC latency {bucket.CurrentUs:0.##} us exceeded threshold {thresholdUs:0.##} us");
        }

        if (previousState == MonitorState.Alerting)
        {
            return new AlertEvaluationResult(
                MonitorState.Running,
                $"DPC latency recovered to {bucket.CurrentUs:0.##} us");
        }

        return new AlertEvaluationResult(MonitorState.Running, null);
    }
}
