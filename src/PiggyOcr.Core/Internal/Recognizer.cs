using Microsoft.ML.OnnxRuntime;

namespace PiggyOcr.Core.Internal;

internal sealed class Recognizer
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly OcrOptions _options;
    private readonly string[] _dict;

    public Recognizer(InferenceSession session, string modelDirectory, OcrOptions options)
    {
        _session = session;
        _inputName = session.InputMetadata.Keys.First();
        _outputName = session.OutputMetadata.Keys.First();
        _options = options;
        _dict = LoadDict(session, modelDirectory);
    }

    public OcrLine? Recognize(ImageBuffer image, Box box)
    {
        int pad = _options.CropPadding;
        int x = Math.Max(0, box.X - pad);
        int y = Math.Max(0, box.Y - pad);
        int right = Math.Min(image.Width, box.Right + pad);
        int bottom = Math.Min(image.Height, box.Bottom + pad);
        int w = right - x, h = bottom - y;
        if (w < 2 || h < 2) return null;

        var crop = ImageOps.Crop(image, x, y, w, h);
        int contentWidth = Math.Clamp(
            (int)MathF.Round(w * _options.RecHeight / (float)h),
            16,
            _options.RecMaxWidth);

        var resized = ImageOps.Resize(crop, contentWidth, _options.RecHeight);
        int paddedWidth = RoundUp(contentWidth, _options.RecPadMultiple);
        if (paddedWidth != contentWidth)
            resized = ImageOps.PadRight(resized, paddedWidth);

        var tensor = ImageOps.ToTensor(resized, _options.RecMean, _options.RecStd);
        using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });
        var output = results.First(v => v.Name == _outputName).AsTensor<float>();
        var logits = output.ToArray();
        int steps = output.Dimensions[1];
        int classes = output.Dimensions[2];

        var (text, confidence) = CtcDecoder.Decode(logits, steps, classes, _dict);
        return text.Length == 0 ? null : new OcrLine(text, box, confidence);
    }

    public void WarmUp()
    {
        var blank = new ImageBuffer(_options.RecPadMultiple * 4, _options.RecHeight);
        var tensor = ImageOps.ToTensor(blank, _options.RecMean, _options.RecStd);
        using var _ = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });
    }

    private static string[] LoadDict(InferenceSession session, string modelDirectory)
    {
        if (session.ModelMetadata.CustomMetadataMap.TryGetValue("character", out var text)
            && !string.IsNullOrWhiteSpace(text))
            return text.Split('\n');

        var path = Path.Combine(modelDirectory, "dict.txt");
        if (File.Exists(path))
            return File.ReadAllLines(path);

        throw new InvalidOperationException("rec 模型缺少 character 元数据，也未找到 dict.txt");
    }

    private static int RoundUp(int value, int multiple) => (value + multiple - 1) / multiple * multiple;
}
