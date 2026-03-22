namespace DpcMonitor.Core.Services;

public sealed class SessionLogBuffer
{
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries;

    public void Append(string message)
    {
        if (_entries.Count > 0 && string.Equals(_entries[^1], message, StringComparison.Ordinal))
        {
            return;
        }

        _entries.Add(message);
    }
}
