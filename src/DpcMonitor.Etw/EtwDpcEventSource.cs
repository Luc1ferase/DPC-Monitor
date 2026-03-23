using DpcMonitor.Core.Models;
using DpcMonitor.Core.Services;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;

namespace DpcMonitor.Etw;

public sealed class EtwDpcEventSource : IDpcEventSource, IDisposable
{
    private readonly KernelTraceSessionFactory _sessionFactory;
    private readonly string _sessionName;
    private readonly object _gate = new();

    private TraceEventSession? _session;
    private ETWTraceEventSource? _source;
    private Task? _processingTask;
    private bool _started;

    public EtwDpcEventSource(KernelTraceSessionFactory? sessionFactory = null, string? sessionName = null)
    {
        _sessionFactory = sessionFactory ?? new KernelTraceSessionFactory();
        _sessionName = sessionName ?? $"DpcMonitorKernelSession-{Environment.ProcessId}";
    }

    public event EventHandler<DpcLatencySample>? SampleReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_started)
            {
                return Task.CompletedTask;
            }

            _session = _sessionFactory.Create(_sessionName);
            _session.EnableKernelProvider(
                KernelTraceEventParser.Keywords.DeferedProcedureCalls | KernelTraceEventParser.Keywords.Interrupt,
                KernelTraceEventParser.Keywords.None);

            _source = _session.Source;

            var parser = _source.Kernel;
            parser.PerfInfoDPC += data => PublishSample(data.TimeStamp, data.ElapsedTimeMSec * 1000.0);
            parser.PerfInfoThreadedDPC += data => PublishSample(data.TimeStamp, data.ElapsedTimeMSec * 1000.0);
            parser.PerfInfoTimerDPC += data => PublishSample(data.TimeStamp, data.ElapsedTimeMSec * 1000.0);
            parser.PerfInfoISR += data => PublishSample(data.TimeStamp, data.ElapsedTimeMSec * 1000.0);

            _processingTask = Task.Run(() =>
            {
                try
                {
                    _source.Process();
                }
                catch (Exception)
                {
                    // Surface ETW lifecycle failures through the app's coordinator on later integration steps.
                }
            }, cancellationToken);

            _started = true;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        TraceEventSession? session;
        ETWTraceEventSource? source;
        Task? processingTask;

        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            session = _session;
            source = _source;
            processingTask = _processingTask;

            _session = null;
            _source = null;
            _processingTask = null;
            _started = false;
        }

        session?.Stop();
        source?.Dispose();
        session?.Dispose();

        if (processingTask is not null)
        {
            await processingTask.WaitAsync(cancellationToken);
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private void PublishSample(DateTime timestamp, double durationUs)
    {
        if (durationUs <= 0)
        {
            return;
        }

        SampleReceived?.Invoke(this, new DpcLatencySample(new DateTimeOffset(timestamp), durationUs));
    }
}
