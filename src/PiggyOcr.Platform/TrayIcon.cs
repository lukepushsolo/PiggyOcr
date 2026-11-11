using System.Runtime.InteropServices;
using static PiggyOcr.Platform.NativeMethods;

namespace PiggyOcr.Platform;

/// <summary>Tray icon + global hotkey, both hosted on one hidden Win32 window.</summary>
public sealed class TrayIcon : IDisposable
{
    private const string ClassName = "PiggyOcrTray";

    private static WndProc? _wndProc;
    private static TrayIcon? _current;
    private static bool _classRegistered;

    private readonly uint _hotkeyModifiers;
    private readonly uint _hotkeyKey;
    private IntPtr _hwnd;
    private NOTIFYICONDATA _iconData;
    private bool _disposed;

    public bool HotkeyRegistered { get; private set; }
    public string? HotkeyError { get; private set; }

    public event Action? HotkeyPressed;
    public event Action? CaptureRequested;
    public event Action? SettingsRequested;
    public event Action? CheckUpdateRequested;
    public event Action? ExitRequested;

    public TrayIcon(uint modifiers = MOD_WIN | MOD_SHIFT | MOD_NOREPEAT, uint key = 'O')
    {
        _hotkeyModifiers = modifiers;
        _hotkeyKey = key;

        _current = this;
        RegisterClass();
        IntPtr instance = GetModuleHandle(null);
        _hwnd = CreateWindowEx(0, ClassName, "", WS_POPUP, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"tray window failed: {Marshal.GetLastWin32Error()}");

        IntPtr icon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
        _iconData = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP,
            uCallbackMessage = WM_APP + 1,
            hIcon = icon,
            szTip = "PiggyOcr · 截图取字",
            szInfo = "",
            szInfoTitle = "",
        };
        if (!Shell_NotifyIcon(NIM_ADD, ref _iconData))
            throw new InvalidOperationException("Shell_NotifyIcon failed");

        HotkeyRegistered = RegisterHotKey(_hwnd, 1, _hotkeyModifiers, _hotkeyKey);
        if (!HotkeyRegistered)
            HotkeyError = $"RegisterHotKey failed: {Marshal.GetLastWin32Error()}";
    }

    private void RegisterClass()
    {
        if (_classRegistered) return;
        _wndProc = WndProcImpl;
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = ClassName,
        };
        RegisterClassEx(ref wc);
        _classRegistered = true;
    }

    private static IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        TrayIcon? current = _current;
        if (current is null || current._hwnd != hWnd)
            return DefWindowProc(hWnd, msg, wParam, lParam);

        switch (msg)
        {
            case WM_HOTKEY:
                current.HotkeyPressed?.Invoke();
                return IntPtr.Zero;

            case WM_APP + 1:
                int mouseMessage = lParam.ToInt32() & 0xFFFF;
                if (mouseMessage == WM_LBUTTONUP)
                    current.CaptureRequested?.Invoke();
                else if (mouseMessage == WM_RBUTTONUP)
                    current.ShowMenu();
                return IntPtr.Zero;

            case WM_DESTROY:
                return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        IntPtr menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, 1, "截图识别");
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, MF_STRING, 2, "设置");
        AppendMenu(menu, MF_STRING, 3, "检查更新");
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, MF_STRING, 4, "退出");

        GetCursorPos(out POINT point);
        SetForegroundWindow(_hwnd);
        uint command = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY,
            point.X, point.Y, 0, _hwnd, IntPtr.Zero);
        PostMessage(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);

        switch (command)
        {
            case 1: CaptureRequested?.Invoke(); break;
            case 2: SettingsRequested?.Invoke(); break;
            case 3: CheckUpdateRequested?.Invoke(); break;
            case 4: ExitRequested?.Invoke(); break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _iconData);
            UnregisterHotKey(_hwnd, 1);
            IntPtr hwnd = _hwnd;
            _hwnd = IntPtr.Zero;
            DestroyWindow(hwnd);
        }
        if (ReferenceEquals(_current, this)) _current = null;
    }
}
