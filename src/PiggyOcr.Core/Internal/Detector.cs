using Microsoft.ML.OnnxRuntime;

namespace PiggyOcr.Core.Internal;

internal sealed class Detector
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly OcrOptions _options;

    public Detector(InferenceSession session, OcrOptions options)
    {
        _session = session;
        _inputName = session.InputMetadata.Keys.First();
        _outputName = session.OutputMetadata.Keys.First();
        _options = options;
    }

    public List<Box> Detect(ImageBuffer image)
    {
        int canvasW = _options.DetCanvasWidth;
        int canvasH = _options.DetCanvasHeight;

        float scale = Math.Min(canvasW / (float)image.Width, canvasH / (float)image.Height);
        scale = Math.Min(scale, _options.DetMaxUpscale);

        int scaledW = Math.Max(1, (int)MathF.Round(image.Width * scale));
        int scaledH = Math.Max(1, (int)MathF.Round(image.Height * scale));
        int padX = (canvasW - scaledW) / 2;
        int padY = (canvasH - scaledH) / 2;

        var resized = ImageOps.Resize(image, scaledW, scaledH);
        var canvas = new ImageBuffer(canvasW, canvasH);
        ImageOps.Blit(canvas, resized, padX, padY);

        var tensor = ImageOps.ToTensor(canvas, _options.DetMean, _options.DetStd);

        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });
        var map = results.First(v => v.Name == _outputName).AsTensor<float>();
        var probs = map.ToArray();
        int pw = map.Dimensions[3], ph = map.Dimensions[2];

        bool logits = false;
        for (int i = 0; i < probs.Length; i++)
        {
            if (probs[i] < 0f || probs[i] > 1f)
            {
                logits = true;
                break;
            }
        }
        if (logits)
        {
            for (int i = 0; i < probs.Length; i++)
                probs[i] = 1f / (1f + MathF.Exp(-probs[i]));
        }

        var boxes = DetPostProcess.Find(probs, pw, ph, _options);

        var result = new List<Box>(boxes.Count);
        foreach (var b in boxes)
        {
            int x = Math.Clamp((int)((b.X - padX) / scale), 0, image.Width - 1);
            int y = Math.Clamp((int)((b.Y - padY) / scale), 0, image.Height - 1);
            int w = Math.Clamp((int)MathF.Ceiling(b.W / scale), 1, image.Width - x);
            int h = Math.Clamp((int)MathF.Ceiling(b.H / scale), 1, image.Height - y);
            result.Add(new Box(x, y, w, h));
        }
        return result;
    }
}
