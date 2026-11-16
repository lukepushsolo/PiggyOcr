using PiggyOcr.Core;
using PiggyOcr.Core.Internal;
using Xunit;

namespace PiggyOcr.Core.Tests;

public class PunctuationNormalizerTests
{
    [Theory]
    [InlineData("说明(内容对了但错字、漏行)", "说明（内容对了但错字、漏行）")]
    [InlineData("括号（圆括号)", "括号（圆括号）")]
    [InlineData("重复（(内容)", "重复（内容）")]
    [InlineData("启动(模型加载、DML初始化)", "启动（模型加载、DML初始化）")]
    [InlineData("中文, English", "中文， English")]
    [InlineData("中文:说明", "中文：说明")]
    [InlineData("代码:if(x==1)", "代码：if(x==1)")]
    [InlineData("中文;", "中文；")]
    [InlineData("中文?", "中文？")]
    [InlineData("中文!", "中文！")]
    [InlineData("中文.", "中文。")]
    [InlineData("他说\"你好\"", "他说“你好”")]
    [InlineData("已经（全角）", "已经（全角）")]
    [InlineData("if (x == 1) { return \"ok\"; }", "if (x == 1) { return \"ok\"; }")]
    [InlineData("foo(\"参数\")", "foo(\"参数\")")]
    [InlineData("调用foo(\"参数\")，然后返回。", "调用foo(\"参数\")，然后返回。")]
    [InlineData("return \"ok\";", "return \"ok\";")]
    [InlineData("std::cout", "std::cout")]
    [InlineData("https://example.com/a", "https://example.com/a")]
    [InlineData("1.5", "1.5")]
    [InlineData("文件.txt", "文件.txt")]
    [InlineData("C:\\Users\\name", "C:\\Users\\name")]
    [InlineData("说明(未完", "说明(未完")]
    [InlineData("This is (fine).", "This is (fine).")]
    public void Normalizes_pairs_without_touching_code(string input, string expected)
    {
        Assert.Equal(expected, PunctuationNormalizer.NormalizeText(input));
    }

    [Fact]
    public void Normalization_is_idempotent()
    {
        string[] samples =
        {
            "说明(内容对了但错字、漏行)",
            "括号（圆括号)",
            "重复（(内容)",
            "调用foo(\"参数\")，然后返回。",
            "if (x == 1) { return \"ok\"; }",
        };

        foreach (string sample in samples)
        {
            string once = PunctuationNormalizer.NormalizeText(sample);
            Assert.Equal(once, PunctuationNormalizer.NormalizeText(once));
        }
    }

    [Fact]
    public void Normalize_preserves_boxes_and_confidence()
    {
        var first = new OcrLine("说明(内容)", new Box(1, 2, 30, 12), 0.91f);
        var second = new OcrLine("代码:if(x==1)", new Box(1, 20, 40, 12), 0.87f);
        var result = new OcrResult(new[] { first, second }, "说明(内容)\n代码:if(x==1)");

        var normalized = PunctuationNormalizer.Normalize(result);

        Assert.Equal(new OcrLine("说明（内容）", first.Box, first.Confidence), normalized.Lines[0]);
        Assert.Equal(new OcrLine("代码：if(x==1)", second.Box, second.Confidence), normalized.Lines[1]);
        Assert.Equal("说明（内容）\n代码：if(x==1)", normalized.Text);
    }
}
