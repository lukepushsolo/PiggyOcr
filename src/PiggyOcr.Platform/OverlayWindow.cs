using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using PiggyOcr.Core;
using static PiggyOcr.Platform.NativeMethods;

namespace PiggyOcr.Platform;

public enum OverlayButton
{
    Left,
    Middle,
    Right,
    DoubleClick,
}

/// <summary>
/// Full-virtual-screen layered window showing a frozen frame, dimmed outside the selection,
/// with a magnifier lens. WinUI 3 cannot do per-pixel transparent overlays, so this is Win32.
/// </summary>
public sealed class OverlayWindow : IDisposable
{
    private const string ClassName = "PiggyOcrOverlay";
    private const uint CS_DBLCLKS = 0x0008;
    private const float DimFactor = 0.55f;
    private const int LensSize = 168;
    private const int LensZoom = 6;
    private const int LensSrc = LensSize / LensZoom;

    private static WndProc? _wndProc;
    private static LowLevelKeyboardProc? _hookProc;
    private static OverlayWindow? _current;
    private static bool _classRegistered;

    private readonly ImageBuffer _frozen;
    private readonly byte[] _dimmed;
    private readonly byte[] _lens = new byte[LensSize * LensSize * 4];
    private readonly int _vx;
    private readonly int _vy;
    private readonly int _vw;
    private readonly int _vh;

    private IntPtr _hwnd;
    private IntPtr _memDc;
    private IntPtr _dib;
    private IntPtr _oldBitmap;
    private IntPtr _bits;
    private IntPtr _hook;
    private readonly bool _rightCancels;
    private bool _selecting;
    private bool _closed;
    private OverlayButton _button;
    private POINT _start;
    private POINT _cursor;

    public event Action<Box, OverlayButton>? Selected;
    public event Action? Cancelled;

    public OverlayWindow(ImageBuffer frozen, int originX, int originY, bool rightCancels = true)
    {
        _frozen = frozen;
        _vx = originX;
        _vy = originY;
        _vw = frozen.Width;
        _vh = frozen.Height;
        _rightCancels = rightCancels;

        _dimmed = new byte[frozen.Bgra.Length];
        for (int i = 0; i < _dimmed.Length; i += 4)
        {
            _dimmed[i] = (byte)(frozen.Bgra[i] * DimFactor);
            _dimmed[i + 1] = (byte)(frozen.Bgra[i + 1] * DimFactor);
            _dimmed[i + 2] = (byte)(frozen.Bgra[i + 2] * DimFactor);
            _dimmed[i + 3] = 255;
        }

        _current = this;
        RegisterClass();
        CreateSurface();
    }

    public void Show()
    {
        IntPtr instance = GetModuleHandle(null);
        _hwnd = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
            ClassName, "", WS_POPUP,
            _vx, _vy, _vw, _vh,
            IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");

        InstallHook();
        ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
        GetCursorPos(out _cursor);
        _cursor.X -= _vx;
        _cursor.Y -= _vy;
        SetCursor(LoadCursor(IntPtr.Zero, (IntPtr)IDC_CROSS));
        Render();
    }

    public void Close()
    {
        if (_closed) return;
        _closed = true;

        UninstallHook();
        if (_hwnd != IntPtr.Zero)
        {
            IntPtr hwnd = _hwnd;
            _hwnd = IntPtr.Zero;
            DestroyWindow(hwnd);
        }

        if (_oldBitmap != IntPtr.Zero)
        {
            SelectObject(_memDc, _oldBitmap);
            _oldBitmap = IntPtr.Zero;
        }
        if (_dib != IntPtr.Zero)
        {
            DeleteObject(_dib);
            _dib = IntPtr.Zero;
        }
        if (_memDc != IntPtr.Zero)
        {
            DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
        }

        if (ReferenceEquals(_current, this)) _current = null;
    }

    public void Dispose() => Close();

    private void CreateSurface()
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        try
        {
            _memDc = CreateCompatibleDC(screenDc);
            var info = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = _vw,
                    biHeight = -_vh,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                },
                bmiColors = new byte[256 * 4],
            };
            _dib = CreateDIBSection(screenDc, ref info, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
            if (_dib == IntPtr.Zero)
                throw new InvalidOperationException($"CreateDIBSection failed: {Marshal.GetLastWin32Error()}");
            _oldBitmap = SelectObject(_memDc, _dib);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private void RegisterClass()
    {
        if (_classRegistered) return;

        _wndProc = WndProcImpl;
        IntPtr instance = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = CS_DBLCLKS,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = instance,
            hCursor = LoadCursor(IntPtr.Zero, (IntPtr)IDC_CROSS),
            lpszClassName = ClassName,
        };
        RegisterClassEx(ref wc);
        _classRegistered = true;
    }

    private void InstallHook()
    {
        if (_hook != IntPtr.Zero) return;
        _hookProc ??= HookProc;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
    }

    private void UninstallHook()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (info.vkCode == VK_ESCAPE)
            {
                OverlayWindow? current = _current;
                if (current is not null)
                {
                    current.Cancel();
                    return (IntPtr)1;
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        OverlayWindow? current = _current;
        if (current is not null && current._hwnd == hWnd)
            return current.HandleMessage(msg, wParam, lParam);
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private IntPtr HandleMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_MOUSEMOVE:
                _cursor = PointFromLParam(lParam);
                SetCursor(LoadCursor(IntPtr.Zero, (IntPtr)IDC_CROSS));
                Render();
                return IntPtr.Zero;

            case WM_LBUTTONDOWN:
                StartSelection(OverlayButton.Left, lParam);
                return IntPtr.Zero;

            case WM_MBUTTONDOWN:
                StartSelection(OverlayButton.Middle, lParam);
                return IntPtr.Zero;

            case WM_RBUTTONDOWN:
                if (_rightCancels) Cancel();
                else StartSelection(OverlayButton.Right, lParam);
                return IntPtr.Zero;

            case WM_LBUTTONUP:
                FinishSelection(OverlayButton.Left, lParam);
                return IntPtr.Zero;

            case WM_MBUTTONUP:
                FinishSelection(OverlayButton.Middle, lParam);
                return IntPtr.Zero;

            case WM_RBUTTONUP:
                if (_rightCancels) Cancel();
                else FinishSelection(OverlayButton.Right, lParam);
                return IntPtr.Zero;

            case WM_LBUTTONDBLCLK:
                _selecting = false;
                var dbl = Selected;
                Close();
                dbl?.Invoke(new Box(0, 0, _vw, _vh), OverlayButton.DoubleClick);
                return IntPtr.Zero;

            case WM_CLOSE:
                Cancel();
                return IntPtr.Zero;

            case WM_DESTROY:
                return IntPtr.Zero;
        }
        return DefWindowProc(_hwnd, msg, wParam, lParam);
    }

    private void StartSelection(OverlayButton button, IntPtr lParam)
    {
        _button = button;
        _start = PointFromLParam(lParam);
        _cursor = _start;
        _selecting = true;
        Render();
    }

    private void FinishSelection(OverlayButton button, IntPtr lParam)
    {
        if (!_selecting || _button != button) return;

        Box rect = SelectionRect(PointFromLParam(lParam));
        _selecting = false;
        if (rect.Width >= 4 && rect.Height >= 4)
        {
            var handler = Selected;
            Close();
            handler?.Invoke(rect, button);
        }
        else
        {
            Render();
        }
    }

    private void Cancel()
    {
        if (_closed) return;
        var handler = Cancelled;
        Close();
        handler?.Invoke();
    }

    private POINT PointFromLParam(IntPtr lParam)
    {
        int value = lParam.ToInt32();
        return new POINT { X = (short)(value & 0xFFFF), Y = (short)((value >> 16) & 0xFFFF) };
    }

    private Box SelectionRect(POINT end)
    {
        int x = Math.Min(_start.X, end.X);
        int y = Math.Min(_start.Y, end.Y);
        int w = Math.Abs(end.X - _start.X);
        int h = Math.Abs(end.Y - _start.Y);
        x = Math.Clamp(x, 0, _vw - 1);
        y = Math.Clamp(y, 0, _vh - 1);
        w = Math.Clamp(w, 0, _vw - x);
        h = Math.Clamp(h, 0, _vh - y);
        return new Box(x, y, w, h);
    }

    private void Render()
    {
        if (_bits == IntPtr.Zero) return;

        Marshal.Copy(_dimmed, 0, _bits, _dimmed.Length);

        Box selection = _selecting ? SelectionRect(_cursor) : default;
        if (selection.Width > 0 && selection.Height > 0)
            CopyRegionFromFrozen(selection);

        using (Graphics g = Graphics.FromHdc(_memDc))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            DrawCrosshair(g);
            if (selection.Width > 0 && selection.Height > 0)
                DrawSelection(g, selection);
            DrawMagnifier(g);
            DrawHint(g);
        }

        var dst = new POINT { X = _vx, Y = _vy };
        var size = new SIZE { Cx = _vw, Cy = _vh };
        var src = new POINT();
        var blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            SourceConstantAlpha = 255,
            AlphaFormat = 0,
        };
        UpdateLayeredWindow(_hwnd, IntPtr.Zero, ref dst, ref size, _memDc, ref src, 0, ref blend, ULW_ALPHA);
    }

    private void CopyRegionFromFrozen(Box box)
    {
        for (int row = 0; row < box.Height; row++)
        {
            int offset = ((box.Y + row) * _vw + box.X) * 4;
            Marshal.Copy(_frozen.Bgra, offset, IntPtr.Add(_bits, offset), box.Width * 4);
        }
    }

    private void DrawCrosshair(Graphics g)
    {
        using var pen = new Pen(Color.FromArgb(255, 200, 200, 200), 1f);
        g.DrawLine(pen, _cursor.X, 0, _cursor.X, _vh);
        g.DrawLine(pen, 0, _cursor.Y, _vw, _cursor.Y);
    }

    private void DrawSelection(Graphics g, Box box)
    {
        using var pen = new Pen(Color.DeepSkyBlue, 2f);
        g.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);

        string label = $"{box.Width} × {box.Height}";
        using var font = new Font("Microsoft YaHei UI", 9.5f);
        SizeF textSize = g.MeasureString(label, font);
        float x = box.X;
        float y = box.Bottom + 6;
        if (y + textSize.Height > _vh) y = box.Bottom - textSize.Height - 6;
        using var bg = new SolidBrush(Color.FromArgb(255, 20, 20, 20));
        g.FillRectangle(bg, x, y, textSize.Width + 12, textSize.Height + 6);
        g.DrawString(label, font, Brushes.White, x + 6, y + 3);
    }

    private void DrawMagnifier(Graphics g)
    {
        int lx = _cursor.X + 24;
        int ly = _cursor.Y + 24;
        if (lx + LensSize > _vw) lx = _cursor.X - 24 - LensSize;
        if (ly + LensSize > _vh) ly = _cursor.Y - 24 - LensSize;
        lx = Math.Clamp(lx, 0, Math.Max(0, _vw - LensSize));
        ly = Math.Clamp(ly, 0, Math.Max(0, _vh - LensSize));

        for (int dy = 0; dy < LensSize; dy++)
        {
            int sy = Math.Clamp(_cursor.Y - LensSrc / 2 + dy / LensZoom, 0, _vh - 1);
            int srcRow = sy * _vw;
            int dstRow = dy * LensSize;
            for (int dx = 0; dx < LensSize; dx++)
            {
                int sx = Math.Clamp(_cursor.X - LensSrc / 2 + dx / LensZoom, 0, _vw - 1);
                int si = (srcRow + sx) * 4;
                int di = (dstRow + dx) * 4;
                _lens[di] = _frozen.Bgra[si];
                _lens[di + 1] = _frozen.Bgra[si + 1];
                _lens[di + 2] = _frozen.Bgra[si + 2];
                _lens[di + 3] = 255;
            }
        }

        Marshal.Copy(_lens, 0, IntPtr.Add(_bits, (ly * _vw + lx) * 4), _lens.Length);

        using var outer = new Pen(Color.FromArgb(255, 20, 20, 20), 4f);
        using var inner = new Pen(Color.White, 2f);
        g.DrawRectangle(outer, lx - 1, ly - 1, LensSize + 2, LensSize + 2);
        g.DrawRectangle(inner, lx + 1, ly + 1, LensSize - 2, LensSize - 2);

        using var cross = new Pen(Color.FromArgb(255, 255, 80, 80), 1f);
        g.DrawLine(cross, lx + LensSize / 2, ly + 4, lx + LensSize / 2, ly + LensSize - 4);
        g.DrawLine(cross, lx + 4, ly + LensSize / 2, lx + LensSize - 4, ly + LensSize / 2);
    }

    private void DrawHint(Graphics g)
    {
        const string hint = "拖拽框选 · 右键取消 · 双击全屏";
        using var font = new Font("Microsoft YaHei UI", 10.5f);
        SizeF size = g.MeasureString(hint, font);
        float x = (_vw - size.Width) / 2f;
        float y = _vh - size.Height - 48;
        using var bg = new SolidBrush(Color.FromArgb(255, 22, 22, 22));
        g.FillRectangle(bg, x - 16, y - 7, size.Width + 32, size.Height + 14);
        using var border = new Pen(Color.FromArgb(255, 70, 70, 70), 1f);
        g.DrawRectangle(border, x - 16, y - 7, size.Width + 32, size.Height + 14);
        g.DrawString(hint, font, Brushes.White, x, y);
    }
}
