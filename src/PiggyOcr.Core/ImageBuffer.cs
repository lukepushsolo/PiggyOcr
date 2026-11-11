namespace PiggyOcr.Core;

/// <summary>BGRA8888 image, stride = Width * 4. Platform layer decodes screenshots into this.</summary>
public sealed class ImageBuffer
{
    public int Width { get; }
    public int Height { get; }
    public int Stride => Width * 4;
    public byte[] Bgra { get; }

    public ImageBuffer(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        Bgra = new byte[width * height * 4];
    }

    public ImageBuffer(int width, int height, byte[] bgra)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (bgra.Length != width * height * 4)
            throw new ArgumentException($"expected {width * height * 4} bytes, got {bgra.Length}", nameof(bgra));
        Width = width;
        Height = height;
        Bgra = bgra;
    }

    public ImageBuffer Crop(int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        w = Math.Clamp(w, 1, Width - x);
        h = Math.Clamp(h, 1, Height - y);

        var dst = new ImageBuffer(w, h);
        for (int row = 0; row < h; row++)
            Buffer.BlockCopy(Bgra, (y + row) * Stride + x * 4, dst.Bgra, row * dst.Stride, w * 4);
        return dst;
    }
}
