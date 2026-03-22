using Microsoft.Diagnostics.Tracing.Session;

namespace DpcMonitor.Etw;

public sealed class KernelTraceSessionFactory
{
    public TraceEventSession Create(string sessionName)
    {
        var session = new TraceEventSession(sessionName, TraceEventSessionOptions.Create);
        session.StopOnDispose = true;
        return session;
    }
}
