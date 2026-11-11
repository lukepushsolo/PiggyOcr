using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PiggyOcr.Core;
using PiggyOcr.Platform;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace PiggyOcr.App;

public partial class App : Application
{
    private readonly ManualResetEventSlim _engineReady = new(false);
    private readonly object _engineLock = new();

    private DispatcherQueue? _dispatcher;
    private TrayIcon? _tray;
    private OcrEngine? _engine;
    private ModelFiles? _model;
    private OverlayWindow? _overlay;
    private ResultWindow? _result;
    private Window? _anchor;
    private Mutex? _mutex;
    private string _modelsRoot = "";

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mutex = new Mutex(true, "PiggyOcr.SingleInstance", out bool created);
        if (!created)
        {
            Exit();
            return;
        }

        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _modelsRoot = ResolveModelsRoot();

        _anchor = new Window { Title = "PiggyOcr" };
        _anchor.AppWindow.IsShownInSwitchers = false;
        _anchor.AppWindow.Hide();

        try
        {
            _tray = new TrayIcon();
            _tray.HotkeyPressed += OnHotkey;
            _tray.CaptureRequested += OnHotkey;
            _tray.ExitRequested += Shutdown;
            _tray.SettingsRequested += () => _dispatcher.TryEnqueue(() => ShowInfo("设置页将在下一阶段接入。"));
            _tray.CheckUpdateRequested += () => _dispatcher.TryEnqueue(OpenReleasesPage);

            if (!_tray.HotkeyRegistered)
                Log("hotkey", new InvalidOperationException(_tray.HotkeyError ?? "unknown"));
        }
        catch (Exception ex)
        {
            Log("tray", ex);
        }

        Task.Run(InitializeEngine);
    }

    private void OnHotkey()
    {
        _dispatcher?.TryEnqueue(BeginCapture);
    }

    private void InitializeEngine()
    {
        try
        {
            _model = ModelSet.BestInstalled(_modelsRoot);
            if (_model is null)
                return;

            var engine = new OcrEngine(_model, preferGpu: true);
            if (engine.FallbackReason is not null)
                Log("dml-fallback", new InvalidOperationException(engine.FallbackReason));
            engine.WarmUp();

            lock (_engineLock)
                _engine = engine;
        }
        catch (Exception ex)
        {
            Log("engine", ex);
        }
        finally
        {
            _engineReady.Set();
        }
    }

    private OcrEngine? GetEngine()
    {
        _engineReady.Wait(TimeSpan.FromSeconds(30));
        lock (_engineLock) return _engine;
    }

    private void BeginCapture()
    {
        if (_overlay is not null) return;

        try
        {
            var frozen = ScreenCapture.CaptureVirtualScreen(out int vx, out int vy);
            var overlay = new OverlayWindow(frozen, vx, vy);
            overlay.Selected += rect =>
            {
                _overlay = null;
                ProcessSelection(frozen, vx, vy, rect);
            };
            overlay.Cancelled += () => _overlay = null;
            _overlay = overlay;
            overlay.Show();
        }
        catch (Exception ex)
        {
            _overlay = null;
            Log("capture", ex);
        }
    }

    private void ProcessSelection(ImageBuffer frozen, int vx, int vy, Box rect)
    {
        var crop = frozen.Crop(rect.X, rect.Y, rect.Width, rect.Height);

        Task.Run(() =>
        {
            try
            {
                var engine = GetEngine();
                if (engine is null)
                {
                    _dispatcher?.TryEnqueue(() => ShowInfo("没有找到模型。请把模型包解压到程序目录的 models 文件夹。"));
                    return;
                }

                var watch = Stopwatch.StartNew();
                var result = engine.Recognize(crop);
                watch.Stop();

                if (result.Text.Length == 0)
                {
                    _dispatcher?.TryEnqueue(() => ShowInfo("没有识别到文字。"));
                    return;
                }

                Log("ocr", null, $"backend={engine.Backend} tier={engine.Model.Tier} ms={watch.ElapsedMilliseconds} chars={result.Text.Length}");
                _dispatcher?.TryEnqueue(() => ShowResult(result.Text, vx + rect.X, vy + rect.Y));
            }
            catch (Exception ex)
            {
                Log("ocr", ex);
                _dispatcher?.TryEnqueue(() => ShowInfo("识别失败，详情见 PiggyOcr.log。"));
            }
        });
    }

    private void ShowResult(string text, int screenX, int screenY)
    {
        CopyText(text);

        _result?.Close();
        var window = new ResultWindow(text);
        _result = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_result, window)) _result = null;
        };

        var area = DisplayArea.GetFromPoint(new PointInt32(screenX, screenY), DisplayAreaFallback.Primary);
        const int width = 520, height = 360;
        int x = Math.Clamp(screenX, area.WorkArea.X + 8, area.WorkArea.X + area.WorkArea.Width - width - 8);
        int y = Math.Clamp(screenY, area.WorkArea.Y + 8, area.WorkArea.Y + area.WorkArea.Height - height - 8);
        window.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        window.Activate();
    }

    private void ShowInfo(string message)
    {
        var window = new Window { Title = "PiggyOcr" };
        var panel = new StackPanel { Padding = new Thickness(24), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = "知道了", HorizontalAlignment = HorizontalAlignment.Right };
        button.Click += (_, _) => window.Close();
        panel.Children.Add(button);
        window.Content = panel;
        window.AppWindow.Resize(new SizeInt32(420, 180));
        window.Activate();
    }

    private static void CopyText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private void OpenReleasesPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/lukepushsolo/PiggyOcr/releases")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log("update", ex);
        }
    }

    private void Shutdown()
    {
        _overlay?.Dispose();
        _overlay = null;
        _result?.Close();
        _tray?.Dispose();
        _tray = null;

        lock (_engineLock)
        {
            _engine?.Dispose();
            _engine = null;
        }

        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        _mutex = null;
        Exit();
    }

    private static string ResolveModelsRoot()
    {
        string local = Path.Combine(AppContext.BaseDirectory, "models");
        if (Directory.Exists(local)) return local;

        string? env = Environment.GetEnvironmentVariable("PIGGYOCR_MODELS");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env)) return env;

        return local;
    }

    private static void Log(string tag, Exception? ex, string? message = null)
    {
        try
        {
            string line = $"[{DateTime.Now:O}] {tag}: {message}{(ex is null ? "" : " " + ex)}";
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "PiggyOcr.log"), line + Environment.NewLine);
        }
        catch
        {
            // logging must never take the app down
        }
    }
}
