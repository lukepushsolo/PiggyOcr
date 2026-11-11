using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PiggyOcr.Core;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length < 2)
{
    Console.WriteLine("usage: OcrCli <image.png> <modelsRoot> [gpu|cpu] [tiny|small|medium]");
    return 1;
}

var imagePath = args[0];
var modelsRoot = args[1];
bool preferGpu = args.Length < 3 || !args[2].Equals("cpu", StringComparison.OrdinalIgnoreCase);
var tierName = args.Length >= 4 ? args[3].ToLowerInvariant() : null;

ModelFiles? model = tierName is null
    ? ModelSet.BestInstalled(modelsRoot)
    : ModelSet.Discover(modelsRoot).FirstOrDefault(m => m.Tier.ToString().Equals(tierName, StringComparison.OrdinalIgnoreCase));

if (model is null)
{
    Console.WriteLine($"no model found under {modelsRoot}");
    return 2;
}

var image = Load(imagePath);
Console.WriteLine($"image {image.Width}x{image.Height}, model {model.Tier}, gpu={preferGpu}");

var sw = Stopwatch.StartNew();
using var engine = new OcrEngine(model, preferGpu);
Console.WriteLine($"engine init {sw.ElapsedMilliseconds}ms, backend={engine.Backend}" +
                  (engine.FallbackReason is null ? "" : $", fallback={engine.FallbackReason}"));

sw.Restart();
engine.WarmUp();
Console.WriteLine($"warmup {sw.ElapsedMilliseconds}ms");

for (int i = 0; i < 2; i++)
{
    sw.Restart();
    var result = engine.Recognize(image);
    Console.WriteLine($"run {i + 1}: {sw.ElapsedMilliseconds}ms, lines={result.Lines.Count}");
    if (i == 0)
    {
        Console.WriteLine("---- text ----");
        Console.WriteLine(result.Text);
        Console.WriteLine("---- lines ----");
        foreach (var line in result.Lines)
            Console.WriteLine($"[{line.Box.X},{line.Box.Y} {line.Box.Width}x{line.Box.Height}] {line.Confidence:F2} {line.Text}");
    }
}

return 0;

static ImageBuffer Load(string path)
{
    using var raw = new Bitmap(path);
    using var bmp = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
        g.DrawImage(raw, 0, 0, raw.Width, raw.Height);

    var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
    var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var bytes = new byte[data.Stride * bmp.Height];
    Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
    bmp.UnlockBits(data);
    return new ImageBuffer(bmp.Width, bmp.Height, bytes);
}
