using System.Security.Principal;
using DpcMonitor.Core.Services;

namespace DpcMonitor.Etw;

public sealed class WindowsPrivilegeService : IPrivilegeService
{
    public bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
