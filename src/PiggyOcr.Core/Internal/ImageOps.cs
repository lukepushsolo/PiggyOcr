using Microsoft.ML.OnnxRuntime.Tensors;

namespace PiggyOcr.Core.Internal;

internal static class ImageOps
{
    public static ImageBuffer Resize(ImageBuffer src, int dstW, int dstH)
    {
        var dst = new ImageBuffer(dstW, dstH);
        float sx = src.Width / (float)dstW;
        float sy = src.Height / (float)dstH;

        for (int y = 0; y < dstH; y++)
        {
            float fy = (y + 0.5f) * sy - 0.5f;
            int y0 = Math.Clamp((int)MathF.Floor(fy), 0, src.Height - 1);
            int y1 = Math.Clamp(y0 + 1, 0, src.Height - 1);
            float wy = Math.Clamp(fy - y0, 0f, 1f);
            int r0 = y0 * src.Stride;
            int r1 = y1 * src.Stride;
            int drow = y * dstW * 4;

            for (int x = 0; x < dstW; x++)
            {
                float fx = (x + 0.5f) * sx - 0.5f;
                int x0 = Math.Clamp((int)MathF.Floor(fx), 0, src.Width - 1);
                int x1 = Math.Clamp(x0 + 1, 0, src.Width - 1);
                float wx = Math.Clamp(fx - x0, 0f, 1f);

                int i00 = r0 + x0 * 4, i01 = r0 + x1 * 4;
                int i10 = r1 + x0 * 4, i11 = r1 + x1 * 4;
                int di = drow + x * 4;

                for (int c = 0; c < 4; c++)
                {
                    float top = src.Bgra[i00 + c] * (1 - wx) + src.Bgra[i01 + c] * wx;
                    float bottom = src.Bgra[i10 + c] * (1 - wx) + src.Bgra[i11 + c] * wx;
                    dst.Bgra[di + c] = (byte)Math.Clamp(top * (1 - wy) + bottom * wy, 0, 255);
                }
            }
        }
        return dst;
    }

    public static ImageBuffer Crop(ImageBuffer src, int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, src.Width - 1);
        y = Math.Clamp(y, 0, src.Height - 1);
        w = Math.Clamp(w, 1, src.Width - x);
        h = Math.Clamp(h, 1, src.Height - y);

        var dst = new ImageBuffer(w, h);
        for (int row = 0; row < h; row++)
            Buffer.BlockCopy(src.Bgra, (y + row) * src.Stride + x * 4, dst.Bgra, row * dst.Stride, w * 4);
        return dst;
    }

    /// <summary>BGRA buffer -> NCHW BGR float tensor.</summary>
    public static DenseTensor<float> ToTensor(ImageBuffer img, float[] mean, float[] std)
    {
        int w = img.Width, h = img.Height, plane = w * h;
        var tensor = new DenseTensor<float>(new float[3 * plane], new[] { 1, 3, h, w });
        var b = img.Bgra;
        int off = 0;
        for (int i = 0; i < b.Length; i += 4)
        {
            tensor.SetValue(off, (b[i] / 255f - mean[0]) / std[0]);
            tensor.SetValue(plane + off, (b[i + 1] / 255f - mean[1]) / std[1]);
            tensor.SetValue(2 * plane + off, (b[i + 2] / 255f - mean[2]) / std[2]);
            off++;
        }
        return tensor;
    }

    public static ImageBuffer PadRight(ImageBuffer src, int dstW)
    {
        if (dstW <= src.Width) return src;
        var dst = new ImageBuffer(dstW, src.Height);
        for (int row = 0; row < src.Height; row++)
            Buffer.BlockCopy(src.Bgra, row * src.Stride, dst.Bgra, row * dst.Stride, src.Stride);
        return dst;
    }
}
