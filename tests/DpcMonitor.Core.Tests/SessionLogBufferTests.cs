using DpcMonitor.Core.Services;

namespace DpcMonitor.Core.Tests;

public sealed class SessionLogBufferTests
{
    [Fact]
    public void Append_SuppressesConsecutiveDuplicateMessages()
    {
        var buffer = new SessionLogBuffer();

        buffer.Append("DPC latency 352 us exceeded threshold");
        buffer.Append("DPC latency 352 us exceeded threshold");
        buffer.Append("DPC latency 410 us exceeded threshold");

        Assert.Equal(2, buffer.Entries.Count);
        Assert.Equal("DPC latency 352 us exceeded threshold", buffer.Entries[0]);
        Assert.Equal("DPC latency 410 us exceeded threshold", buffer.Entries[1]);
    }
}
