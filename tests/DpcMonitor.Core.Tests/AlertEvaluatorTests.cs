using DpcMonitor.Core.Models;
using DpcMonitor.Core.Services;

namespace DpcMonitor.Core.Tests;

public sealed class AlertEvaluatorTests
{
    [Fact]
    public void Evaluate_TransitionsToAlerting_WhenCurrentUsExceedsThreshold()
    {
        var evaluator = new AlertEvaluator();
        var bucket = new DpcLatencyBucket(
            new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero),
            CurrentUs: 352,
            PeakUs: 352,
            AverageUs: 240);

        var result = evaluator.Evaluate(bucket, thresholdUs: 300, previousState: MonitorState.Running);

        Assert.Equal(MonitorState.Alerting, result.State);
        Assert.Contains("352", result.LogMessage);
    }

    [Fact]
    public void Evaluate_TransitionsBackToRunning_WhenCurrentUsDropsBelowThreshold()
    {
        var evaluator = new AlertEvaluator();
        var bucket = new DpcLatencyBucket(
            new DateTimeOffset(2026, 3, 22, 10, 0, 1, TimeSpan.Zero),
            CurrentUs: 180,
            PeakUs: 352,
            AverageUs: 260);

        var result = evaluator.Evaluate(bucket, thresholdUs: 300, previousState: MonitorState.Alerting);

        Assert.Equal(MonitorState.Running, result.State);
        Assert.Contains("recovered", result.LogMessage, StringComparison.OrdinalIgnoreCase);
    }
}
