using System.Runtime.InteropServices;
using PiggyOcr.Core;
using static PiggyOcr.Platform.NativeMethods;

namespace PiggyOcr.Platform;

public static class ScreenCapture
{
    /// <summary>Freezes the whole virtual desktop into BGRA. Coordinates are physical pixels.</summary>
    public static ImageBuffer CaptureVirtualScreen(out int originX, out int originY)
    {
        originX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        originY = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException($"invalid virtual screen {width}x{height}");

        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr bitmap = CreateCompatibleBitmap(screenDc, width, height);
        IntPtr oldBitmap = SelectObject(memDc, bitmap);

        try
        {
            if (!BitBlt(memDc, 0, 0, width, height, screenDc, originX, originY, SRCCOPY | CAPTUREBLT))
                throw new InvalidOperationException($"BitBlt failed: {Marshal.GetLastWin32Error()}");

            var info = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                },
                bmiColors = new byte[256 * 4],
            };

            var bytes = new byte[width * height * 4];
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                int lines = GetDIBits(memDc, bitmap, 0, (uint)height, handle.AddrOfPinnedObject(), ref info, DIB_RGB_COLORS);
                if (lines == 0)
                    throw new InvalidOperationException($"GetDIBits failed: {Marshal.GetLastWin32Error()}");
            }
            finally
            {
                handle.Free();
            }

            return new ImageBuffer(width, height, bytes);
        }
        finally
        {
            SelectObject(memDc, oldBitmap);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
