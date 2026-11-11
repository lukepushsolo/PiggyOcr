namespace PiggyOcr.Core;

public sealed class OcrOptions
{
    /// <summary>Upscale short side to this before detection (PP-OCR limit_type=min).</summary>
    public int DetLimitSideLen { get; set; } = 736;
    public int DetMaxSide { get; set; } = 2400;
    public float DetThreshold { get; set; } = 0.3f;
    public float BoxThreshold { get; set; } = 0.5f;
    public float UnclipRatio { get; set; } = 1.6f;
    public int MinBoxSize { get; set; } = 3;

    public int RecHeight { get; set; } = 48;
    public int RecMaxWidth { get; set; } = 2048;
    public int RecPadMultiple { get; set; } = 32;
    public int CropPadding { get; set; } = 4;

    public float[] DetMean { get; set; } = { 0.5f, 0.5f, 0.5f };
    public float[] DetStd { get; set; } = { 0.5f, 0.5f, 0.5f };
    public float[] RecMean { get; set; } = { 0.5f, 0.5f, 0.5f };
    public float[] RecStd { get; set; } = { 0.5f, 0.5f, 0.5f };
}
