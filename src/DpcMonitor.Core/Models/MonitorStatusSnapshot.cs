namespace DpcMonitor.Core.Models;

public sealed record MonitorStatusSnapshot(
    MonitorState State,
    double CurrentUs,
    double PeakUs,
    double AverageUs,
    double ThresholdUs,
    string StatusMessage,
    IReadOnlyList<DpcLatencyBucket> Chart);
