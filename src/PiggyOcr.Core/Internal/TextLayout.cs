using System.Text;

namespace PiggyOcr.Core.Internal;

internal static class TextLayout
{
    public static OcrResult Build(List<OcrLine> lines)
    {
        var sorted = lines.OrderBy(l => l.Box.CenterY).ThenBy(l => l.Box.X).ToList();
        var rows = new List<List<OcrLine>>();

        foreach (var line in sorted)
        {
            var row = rows.Count > 0 ? rows[^1] : null;
            if (row is null || !SameRow(row, line.Box))
                rows.Add(new List<OcrLine> { line });
            else
                row.Add(line);
        }

        var outLines = new List<OcrLine>(rows.Count);
        var sb = new StringBuilder();

        foreach (var row in rows)
        {
            var ordered = row.OrderBy(l => l.Box.X).ToList();
            string text = Join(ordered.Select(l => l.Text));
            if (text.Length == 0) continue;

            outLines.Add(new OcrLine(text, Union(ordered), ordered.Average(l => l.Confidence)));
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(text);
        }

        return new OcrResult(outLines, sb.ToString());
    }

    private static bool SameRow(List<OcrLine> row, Box b)
    {
        int top = row.Min(l => l.Box.Y);
        int bottom = row.Max(l => l.Box.Bottom);
        int inter = Math.Min(bottom, b.Bottom) - Math.Max(top, b.Y);
        int minHeight = Math.Min(bottom - top, b.Height);
        return inter > minHeight * 0.5;
    }

    private static Box Union(List<OcrLine> row)
    {
        int left = row.Min(l => l.Box.X);
        int top = row.Min(l => l.Box.Y);
        int right = row.Max(l => l.Box.Right);
        int bottom = row.Max(l => l.Box.Bottom);
        return new Box(left, top, right - left, bottom - top);
    }

    private static string Join(IEnumerable<string> parts)
    {
        var sb = new StringBuilder();
        foreach (string part in parts)
        {
            if (part.Length == 0) continue;
            if (sb.Length > 0 && char.IsAsciiLetterOrDigit(sb[^1]) && char.IsAsciiLetterOrDigit(part[0]))
                sb.Append(' ');
            sb.Append(part);
        }
        return sb.ToString();
    }
}
