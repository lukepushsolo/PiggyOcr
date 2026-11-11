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
}
