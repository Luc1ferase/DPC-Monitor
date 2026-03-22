using DpcMonitor.Core.Models;

namespace DpcMonitor.Core.Services;

public sealed class DpcLatencyAggregator
{
    private readonly int _windowSize;
    private readonly Queue<DpcLatencyBucket> _completed = new();
    private readonly List<DpcLatencyBucket> _history = new();
    private DateTimeOffset? _currentBucketStart;
    private double _currentBucketMax;
    private double _sessionPeak;
    private double _sessionSum;
    private int _sessionCount;

    public DpcLatencyAggregator(int windowSize)
    {
        if (windowSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize));
        }

        _windowSize = windowSize;
    }

    public IReadOnlyList<DpcLatencyBucket> History => _history;

    public void Add(DpcLatencySample sample)
    {
        var bucketStart = new DateTimeOffset(
            sample.Timestamp.Year,
            sample.Timestamp.Month,
            sample.Timestamp.Day,
            sample.Timestamp.Hour,
            sample.Timestamp.Minute,
            sample.Timestamp.Second,
            sample.Timestamp.Offset);

        if (_currentBucketStart is null)
        {
            _currentBucketStart = bucketStart;
        }
        else if (bucketStart > _currentBucketStart.Value)
        {
            CompleteCurrentBucket();
            _currentBucketStart = bucketStart;
            _currentBucketMax = 0;
        }

        _currentBucketMax = Math.Max(_currentBucketMax, sample.DurationUs);
        _sessionPeak = Math.Max(_sessionPeak, sample.DurationUs);
        _sessionSum += sample.DurationUs;
        _sessionCount++;
    }

    public IReadOnlyList<DpcLatencyBucket> DrainCompletedBuckets()
    {
        var drained = _completed.ToArray();
        _completed.Clear();
        return drained;
    }

    private void CompleteCurrentBucket()
    {
        if (_currentBucketStart is null || _sessionCount == 0)
        {
            return;
        }

        var bucket = new DpcLatencyBucket(
            _currentBucketStart.Value,
            _currentBucketMax,
            _sessionPeak,
            _sessionSum / _sessionCount);

        _completed.Enqueue(bucket);
        _history.Add(bucket);

        while (_history.Count > _windowSize)
        {
            _history.RemoveAt(0);
        }
    }
}
