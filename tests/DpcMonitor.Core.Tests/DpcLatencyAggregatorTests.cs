using DpcMonitor.Core.Models;
using DpcMonitor.Core.Services;

namespace DpcMonitor.Core.Tests;

public sealed class DpcLatencyAggregatorTests
{
    [Fact]
    public void CompleteBucket_UsesMaximumLatencyAsCurrentValue()
    {
        var aggregator = new DpcLatencyAggregator(windowSize: 3);
        aggregator.Add(new DpcLatencySample(new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero), 120));
        aggregator.Add(new DpcLatencySample(new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero).AddMilliseconds(400), 260));
        aggregator.Add(new DpcLatencySample(new DateTimeOffset(2026, 3, 22, 10, 0, 1, TimeSpan.Zero), 80));

        var completed = aggregator.DrainCompletedBuckets().Single();

        Assert.Equal(260, completed.CurrentUs);
        Assert.Equal(260, completed.PeakUs);
        Assert.Equal(190.0, completed.AverageUs);
    }

    [Fact]
    public void History_RetainsOnlyLatestCompletedBucketsWithinWindowSize()
    {
        var aggregator = new DpcLatencyAggregator(windowSize: 3);
        var start = new DateTimeOffset(2026, 3, 22, 10, 0, 0, TimeSpan.Zero);

        aggregator.Add(new DpcLatencySample(start.AddSeconds(0), 100));
        aggregator.Add(new DpcLatencySample(start.AddSeconds(1), 110));
        aggregator.Add(new DpcLatencySample(start.AddSeconds(2), 120));
        aggregator.Add(new DpcLatencySample(start.AddSeconds(3), 130));
        aggregator.Add(new DpcLatencySample(start.AddSeconds(4), 140));

        _ = aggregator.DrainCompletedBuckets();

        Assert.Equal(3, aggregator.History.Count);
        Assert.Equal(start.AddSeconds(1), aggregator.History[0].BucketStart);
        Assert.Equal(start.AddSeconds(2), aggregator.History[1].BucketStart);
        Assert.Equal(start.AddSeconds(3), aggregator.History[2].BucketStart);
    }
}
