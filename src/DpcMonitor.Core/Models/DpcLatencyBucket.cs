namespace DpcMonitor.Core.Models;

public sealed record DpcLatencyBucket(
    DateTimeOffset BucketStart,
    double CurrentUs,
    double PeakUs,
    double AverageUs);
