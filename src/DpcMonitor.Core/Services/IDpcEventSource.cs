using DpcMonitor.Core.Models;

namespace DpcMonitor.Core.Services;

public interface IDpcEventSource
{
    event EventHandler<DpcLatencySample>? SampleReceived;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
