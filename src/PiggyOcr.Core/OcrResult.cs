namespace PiggyOcr.Core;

public readonly record struct Box(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int CenterY => Y + Height / 2;
}

public sealed record OcrLine(string Text, Box Box, float Confidence);

public sealed record OcrResult(IReadOnlyList<OcrLine> Lines, string Text);
