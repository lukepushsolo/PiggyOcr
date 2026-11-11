using PiggyOcr.Core.Internal;
using Xunit;

namespace PiggyOcr.Core.Tests;

public class CtcDecoderTests
{
    [Fact]
    public void Decodes_merges_repeats_and_drops_blank()
    {
        // classes: 0=blank, 1=A, 2=B, 3=C
        int steps = 5, classes = 4;
        var logits = new float[steps * classes];
        Set(0, 1); Set(1, 1); Set(2, 0); Set(3, 2); Set(4, 3);

        var (text, confidence) = CtcDecoder.Decode(logits, steps, classes, new[] { "A", "B", "C" });

        Assert.Equal("ABC", text);
        Assert.True(confidence > 0);

        void Set(int frame, int cls) => logits[frame * classes + cls] = 1f;
    }

    [Fact]
    public void Empty_when_all_blank()
    {
        var logits = new float[4 * 3];
        for (int t = 0; t < 4; t++) logits[t * 3] = 1f;

        var (text, confidence) = CtcDecoder.Decode(logits, 4, 3, new[] { "A", "B" });

        Assert.Equal("", text);
        Assert.Equal(0, confidence);
    }
}
