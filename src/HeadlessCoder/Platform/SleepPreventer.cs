using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HeadlessCoder.Platform;

/// <summary>
/// Cross-platform "keep the machine awake" guard. Dispose to release the lock.
/// </summary>
public sealed class SleepPreventer : IDisposable
{
    private Process? _helper;
    private bool _windowsActive;
    private bool _disposed;

    private SleepPreventer() { }

    /// <summary>
    /// Starts preventing system sleep on the current OS. Returns a handle that
    /// releases the lock when disposed. Never throws.
    /// </summary>
    public static SleepPreventer Start()
    {
        var preventer = new SleepPreventer();
        try
        {
            if (OperatingSystem.IsWindows())
                preventer.StartWindows();
            else if (OperatingSystem.IsMacOS())
                preventer.StartProcess("caffeinate", "-imsu");
            else if (OperatingSystem.IsLinux())
                preventer.StartLinux();
        }
        catch
        {
            // Best-effort: if we cannot prevent sleep, keep running anyway.
        }
        return preventer;
    }

    /// <summary>Human readable description of how sleep is being prevented.</summary>
    public string Status { get; private set; } = "not active";

    private void StartWindows()
    {
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_AWAYMODE_REQUIRED = 0x00000040;

        uint result = SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED);
        if (result == 0)
            result = SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);

        _windowsActive = result != 0;
        Status = _windowsActive ? "SetThreadExecutionState (system required)" : "unavailable";
    }

    private void StartLinux()
    {
        // Prefer systemd-inhibit; fall back to gnome/dbus is out of scope.
        StartProcess("systemd-inhibit",
            "--what=idle:sleep:handle-lid-switch --who=HeadlessCoder --why=Serving --mode=block sleep infinity");
    }

    private void StartProcess(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        _helper = Process.Start(psi);
        Status = _helper is { HasExited: false }
            ? $"{fileName} (pid {_helper.Id})"
            : $"{fileName} unavailable";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_windowsActive && OperatingSystem.IsWindows())
        {
            const uint ES_CONTINUOUS = 0x80000000;
            SetThreadExecutionState(ES_CONTINUOUS);
        }

        try
        {
            if (_helper is { HasExited: false })
            {
                _helper.Kill(entireProcessTree: true);
                _helper.WaitForExit(2000);
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            _helper?.Dispose();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);
}
