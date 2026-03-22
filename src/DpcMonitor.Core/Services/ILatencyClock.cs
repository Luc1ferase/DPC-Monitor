namespace DpcMonitor.Core.Services;

public interface ILatencyClock
{
    DateTimeOffset UtcNow { get; }
}
