namespace DpcMonitor.Core.Models;

public sealed record DpcLatencySample(DateTimeOffset Timestamp, double DurationUs);
