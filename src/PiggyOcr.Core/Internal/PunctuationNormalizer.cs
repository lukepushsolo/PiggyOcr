using System.Text;

namespace PiggyOcr.Core.Internal;

/// <summary>
/// The recognizer is inconsistent about full-width punctuation: the same line can
/// contain "（... )" or "（(" depending on the model. Fix whole pairs in Chinese
/// prose, but leave code-like text alone.
/// </summary>
internal static class PunctuationNormalizer
{
    public static OcrResult Normalize(OcrResult result)
    {
        if (result.Lines.Count == 0) return result;

        var lines = new OcrLine[result.Lines.Count];
        var text = new StringBuilder(result.Text.Length);
        for (int i = 0; i < result.Lines.Count; i++)
        {
            OcrLine line = result.Lines[i];
            string normalized = NormalizeText(line.Text);
            lines[i] = line with { Text = normalized };
            if (i > 0) text.Append('\n');
            text.Append(normalized);
        }

        return new OcrResult(lines, text.ToString());
    }

    internal static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = CollapseMixedBrackets(text);
        char[] chars = text.ToCharArray();

        NormalizeRoundBrackets(chars);
        NormalizeDoubleQuotes(chars);
        NormalizeStandalonePunctuation(chars);

        return new string(chars);
    }

    private enum PairStyle
    {
        Keep,
        Ascii,
        Full,
    }

    private static string CollapseMixedBrackets(string text)
    {
        while (true)
        {
            string next = text
                .Replace("（(", "（")
                .Replace("(（", "（")
                .Replace(")）", "）")
                .Replace("）)", "）");
            if (next == text) return next;
            text = next;
        }
    }

    private static void NormalizeRoundBrackets(char[] chars)
    {
        var stack = new Stack<int>();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is '(' or '（')
            {
                stack.Push(i);
                continue;
            }

            if (chars[i] is not (')' or '）') || stack.Count == 0) continue;

            int open = stack.Pop();
            PairStyle style = BracketStyle(chars, open, i);
            if (style == PairStyle.Keep) continue;

            bool full = style == PairStyle.Full;
            chars[open] = full ? '（' : '(';
            chars[i] = full ? '）' : ')';
        }
    }

    private static PairStyle BracketStyle(char[] chars, int open, int close)
    {
        char before = open > 0 ? chars[open - 1] : '\0';
        char after = close + 1 < chars.Length ? chars[close + 1] : '\0';
        string content = new(chars, open + 1, close - open - 1);

        if (IsAsciiIdentifier(before) || IsAsciiIdentifier(after) || HasCodeMarker(content))
            return PairStyle.Ascii;

        if (!ContainsCjk(content)) return PairStyle.Ascii;

        if (before == '\0' || IsCjk(before) || char.IsWhiteSpace(before) || IsChinesePunctuation(before))
            return PairStyle.Full;

        return PairStyle.Keep;
    }

    private static void NormalizeDoubleQuotes(char[] chars)
    {
        int open = -1;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c is '“' or '"')
            {
                if (open < 0)
                {
                    open = i;
                    continue;
                }

                ApplyQuoteStyle(chars, open, i);
                open = -1;
            }
            else if (c == '”' && open >= 0)
            {
                ApplyQuoteStyle(chars, open, i);
                open = -1;
            }
        }
    }

    private static void ApplyQuoteStyle(char[] chars, int open, int close)
    {
        char before = open > 0 ? chars[open - 1] : '\0';
        char after = close + 1 < chars.Length ? chars[close + 1] : '\0';
        string content = new(chars, open + 1, close - open - 1);

        bool code = IsAsciiIdentifier(before) || IsAsciiIdentifier(after)
            || HasCodeMarker(content)
            || before is '=' or '(' or '[' or '{' or ',' or ':' or ';';

        bool full = !code && ContainsCjk(content)
            && (before == '\0' || IsCjk(before) || char.IsWhiteSpace(before) || IsChinesePunctuation(before))
            && !(after != '\0' && IsAsciiIdentifier(after));

        chars[open] = full ? '“' : '"';
        chars[close] = full ? '”' : '"';
    }

    private static void NormalizeStandalonePunctuation(char[] chars)
    {
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            char prev = i > 0 ? chars[i - 1] : '\0';
            char next = i + 1 < chars.Length ? chars[i + 1] : '\0';

            switch (c)
            {
                case ',' when IsCjk(prev) && (next == '\0' || IsCjk(next) || char.IsWhiteSpace(next)):
                    chars[i] = '，';
                    break;
                case '.' when IsCjk(prev) && (next == '\0' || IsCjk(next) || char.IsWhiteSpace(next)):
                    chars[i] = '。';
                    break;
                case ':' when IsCjk(prev) && next != ':' && next != '/' && next != '\\':
                    chars[i] = '：';
                    break;
                case ';' when IsCjk(prev) && (next == '\0' || IsCjk(next) || char.IsWhiteSpace(next)):
                    chars[i] = '；';
                    break;
                case '?' when IsCjk(prev):
                    chars[i] = '？';
                    break;
                case '!' when IsCjk(prev):
                    chars[i] = '！';
                    break;
            }
        }
    }

    private static bool ContainsCjk(string value)
    {
        foreach (char c in value)
        {
            if (IsCjk(c)) return true;
        }

        return false;
    }

    private static bool IsCjk(char c) =>
        c is >= '㐀' and <= '䶿'
            or >= '一' and <= '鿿'
            or >= '豈' and <= '﫿'
            or >= '＀' and <= '￯'
            or >= '　' and <= '〿';

    private static bool IsChinesePunctuation(char c) =>
        c is >= '　' and <= '〿' or >= '＀' and <= '￯';

    private static bool IsAsciiIdentifier(char c) =>
        char.IsAsciiLetterOrDigit(c) || c == '_';

    private static bool HasCodeMarker(string value)
    {
        string[] markers = { "==", "=>", "!=", "<=", ">=", "&&", "||", "::", "//", "\\" };
        foreach (string marker in markers)
        {
            if (value.Contains(marker, StringComparison.Ordinal)) return true;
        }

        return value.IndexOfAny(['{', '}', '[', ']', ';', '`']) >= 0;
    }
}
