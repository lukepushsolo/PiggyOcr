namespace PiggyOcr.Core;

public sealed class OcrOptions
{
    /// <summary>
    /// Detection always runs on one fixed canvas (letterboxed). DirectML recompiles its graph
    /// per input shape, so a variable-size det input costs ~300ms on every first capture.
    /// </summary>
    public int DetCanvasWidth { get; set; } = 1920;
    public int DetCanvasHeight { get; set; } = 1088;
    public float DetMaxUpscale { get; set; } = 4f;
    public float DetThreshold { get; set; } = 0.3f;
    public float BoxThreshold { get; set; } = 0.5f;
    public float UnclipRatio { get; set; } = 1.6f;
    public int MinBoxSize { get; set; } = 3;

    public int RecHeight { get; set; } = 48;
    public int RecMaxWidth { get; set; } = 2048;
    public int[] RecWidthBuckets { get; set; } = { 320, 640, 960, 1280, 1600, 2048 };
    public int CropPadding { get; set; } = 4;

    public float[] DetMean { get; set; } = { 0.5f, 0.5f, 0.5f };
    public float[] DetStd { get; set; } = { 0.5f, 0.5f, 0.5f };
    public float[] RecMean { get; set; } = { 0.5f, 0.5f, 0.5f };
    public float[] RecStd { get; set; } = { 0.5f, 0.5f, 0.5f };
}
