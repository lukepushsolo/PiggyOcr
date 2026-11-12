using System.Runtime.InteropServices;

namespace PiggyOcr.Platform;

public static class WindowActivation
{
    private const uint SW_RESTORE = 9;

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, uint cmd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool doAttach);

    /// <summary>
    /// WinUI's Window.Activate() loses to the foreground lock when the app was
    /// started from a global hotkey. This retries with the raw HWND and falls back
    /// to attaching to the current foreground thread.
    /// </summary>
    public static bool ForceForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        ShowWindow(hwnd, SW_RESTORE);
        if (SetForegroundWindow(hwnd)) return true;

        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
        uint currentThread = GetCurrentThreadId();
        if (foregroundThread == currentThread) return SetForegroundWindow(hwnd);

        bool attached = AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            return SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }
}
