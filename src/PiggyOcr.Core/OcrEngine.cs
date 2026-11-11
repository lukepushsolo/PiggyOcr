using Microsoft.ML.OnnxRuntime;
using PiggyOcr.Core.Internal;

namespace PiggyOcr.Core;

public sealed class OcrEngine : IDisposable
{
    private readonly InferenceSession _detSession;
    private readonly InferenceSession _recSession;
    private readonly Detector _detector;
    private readonly Recognizer _recognizer;
    private readonly object _gate = new();

    public ModelFiles Model { get; }
    public string Backend { get; }
    public string? FallbackReason { get; }
    public bool Disposed { get; private set; }

    public OcrEngine(ModelFiles model, bool preferGpu = true, int deviceId = 0, OcrOptions? options = null)
    {
        Model = model;
        var opts = options ?? new OcrOptions();

        if (preferGpu)
        {
            try
            {
                (_detSession, _recSession) = CreateSessions(model, dml: true, deviceId);
                Backend = "DirectML";
                FallbackReason = null;
            }
            catch (Exception ex)
            {
                FallbackReason = ex.Message;
                (_detSession, _recSession) = CreateSessions(model, dml: false, deviceId);
                Backend = "CPU";
            }
        }
        else
        {
            (_detSession, _recSession) = CreateSessions(model, dml: false, deviceId);
            Backend = "CPU";
            FallbackReason = null;
        }

        _detector = new Detector(_detSession, opts);
        _recognizer = new Recognizer(_recSession, model.Directory, opts);
    }

    public OcrResult Recognize(ImageBuffer image)
    {
        lock (_gate)
        {
            var boxes = _detector.Detect(image);
            var lines = new List<OcrLine>(boxes.Count);
            foreach (var box in boxes)
            {
                var line = _recognizer.Recognize(image, box);
                if (line is not null) lines.Add(line);
            }
            return TextLayout.Build(lines);
        }
    }

    /// <summary>Compiles DML kernels for the common shapes so the first real capture is fast.</summary>
    public void WarmUp()
    {
        lock (_gate)
        {
            var blank = new ImageBuffer(736, 736);
            _detector.Detect(blank);
            _recognizer.WarmUp();
        }
    }

    private static (InferenceSession Det, InferenceSession Rec) CreateSessions(ModelFiles model, bool dml, int deviceId)
    {
        using var detOptions = MakeOptions(dml, deviceId);
        using var recOptions = MakeOptions(dml, deviceId);

        InferenceSession? det = null;
        InferenceSession? rec = null;
        try
        {
            det = new InferenceSession(model.DetPath, detOptions);
            rec = new InferenceSession(model.RecPath, recOptions);
            return (det, rec);
        }
        catch
        {
            det?.Dispose();
            rec?.Dispose();
            throw;
        }
    }

    private static SessionOptions MakeOptions(bool dml, int deviceId)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };
        if (dml)
        {
            options.EnableMemoryPattern = false;
            options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            options.AppendExecutionProvider_DML(deviceId);
        }
        return options;
    }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
        _recSession.Dispose();
        _detSession.Dispose();
    }
}
