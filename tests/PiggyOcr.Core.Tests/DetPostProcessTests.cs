using PiggyOcr.Core.Internal;
using Xunit;

namespace PiggyOcr.Core.Tests;

public class DetPostProcessTests
{
    [Fact]
    public void Finds_a_single_block()
    {
        const int w = 40, h = 40;
        var probs = new float[w * h];
        for (int y = 10; y < 20; y++)
            for (int x = 5; x < 25; x++)
                probs[y * w + x] = 0.9f;

        var boxes = DetPostProcess.Find(probs, w, h, new OcrOptions());

        var box = Assert.Single(boxes);
        Assert.True(box.X < 5);
        Assert.True(box.Y < 10);
        Assert.True(box.W > 20);
        Assert.True(box.H > 10);
        Assert.InRange(box.Score, 0.85f, 0.95f);
    }

    [Fact]
    public void Ignores_noise_below_threshold()
    {
        var probs = new float[20 * 20];
        probs[50] = 0.9f;

        var boxes = DetPostProcess.Find(probs, 20, 20, new OcrOptions());

        Assert.Empty(boxes);
    }
}
