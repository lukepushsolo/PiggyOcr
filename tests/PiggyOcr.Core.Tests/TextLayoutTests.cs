using PiggyOcr.Core.Internal;
using Xunit;

namespace PiggyOcr.Core.Tests;

public class TextLayoutTests
{
    [Fact]
    public void Groups_rows_and_joins_latin_words()
    {
        var lines = new List<OcrLine>
        {
            new("World", new Box(60, 0, 50, 20), 1f),
            new("Hello", new Box(0, 0, 50, 20), 1f),
            new("下一行", new Box(0, 30, 50, 20), 1f),
        };

        var result = TextLayout.Build(lines);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("Hello World", result.Lines[0].Text);
        Assert.Equal("Hello World\n下一行", result.Text);
    }

    [Fact]
    public void Does_not_insert_space_between_cjk()
    {
        var lines = new List<OcrLine>
        {
            new("截图", new Box(0, 0, 40, 20), 1f),
            new("取字", new Box(40, 0, 40, 20), 1f),
        };

        var result = TextLayout.Build(lines);

        Assert.Equal("截图取字", result.Text);
    }
}
