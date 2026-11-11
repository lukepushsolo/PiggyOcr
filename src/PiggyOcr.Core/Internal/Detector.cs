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
        int minSide = Math.Min(image.Width, image.Height);
        int maxSide = Math.Max(image.Width, image.Height);

        float scale = 1f;
        if (minSide < _options.DetLimitSideLen)
            scale = Math.Min(_options.DetLimitSideLen / (float)minSide, 4f);
        if (maxSide * scale > _options.DetMaxSide)
            scale = _options.DetMaxSide / (float)maxSide;

        int dw = RoundUp((int)MathF.Ceiling(image.Width * scale), 32);
        int dh = RoundUp((int)MathF.Ceiling(image.Height * scale), 32);

        var resized = ImageOps.Resize(image, dw, dh);
        var tensor = ImageOps.ToTensor(resized, _options.DetMean, _options.DetStd);

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
        float sx = image.Width / (float)pw;
        float sy = image.Height / (float)ph;

        var result = new List<Box>(boxes.Count);
        foreach (var b in boxes)
        {
            int x = Math.Clamp((int)(b.X * sx), 0, image.Width - 1);
            int y = Math.Clamp((int)(b.Y * sy), 0, image.Height - 1);
            int w = Math.Clamp((int)MathF.Ceiling(b.W * sx), 1, image.Width - x);
            int h = Math.Clamp((int)MathF.Ceiling(b.H * sy), 1, image.Height - y);
            result.Add(new Box(x, y, w, h));
        }
        return result;
    }

    private static int RoundUp(int value, int multiple) => (value + multiple - 1) / multiple * multiple;
}
