using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PiggyOcr.App.Services;
using PiggyOcr.Core;
using PiggyOcr.Platform;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace PiggyOcr.App;

public partial class App : Application
{
    private readonly ManualResetEventSlim _engineReady = new(false);
    private readonly object _engineLock = new();
    private readonly List<string> _history = new();

    private DispatcherQueue? _dispatcher;
    private TrayIcon? _tray;
    private OcrEngine? _engine;
    private ModelFiles? _model;
    private OverlayWindow? _overlay;
    private ResultWindow? _result;
    private SettingsWindow? _settings;
    private Window? _anchor;
    private Mutex? _mutex;
    private AppConfig _config = new();
    private string _modelsRoot = "";
    private bool _firstRunShown;

    public static App Instance => (App)Application.Current;

    public AppConfig Config => _config;
    public string ModelsRoot => _modelsRoot;
    public string BackendText => _engine is null ? "未加载" : $"{_engine.Backend} · {_engine.Model.Tier}";
    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);

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
        _config = ConfigStore.Load();
        _modelsRoot = ResolveModelsRoot();
        LogEnabled = _config.WriteLog;

        _anchor = new Window { Title = "PiggyOcr" };
        _anchor.AppWindow.IsShownInSwitchers = false;
        _anchor.AppWindow.Hide();

        try
        {
            AutoStart.Set(_config.AutoStart);

            if (!HotkeyParser.TryParse(_config.Hotkey, out uint mods, out uint key))
            {
                mods = 0x0008 | 0x0004;
                key = 'O';
            }

            _tray = new TrayIcon(mods | 0x4000, key);
            _tray.HotkeyPressed += OnHotkey;
            _tray.CaptureRequested += OnHotkey;
            _tray.ExitRequested += Shutdown;
            _tray.SettingsRequested += () => _dispatcher.TryEnqueue(ShowSettings);
            _tray.CheckUpdateRequested += () => _dispatcher.TryEnqueue(OpenReleases);
            _tray.NotificationClicked += () => _dispatcher.TryEnqueue(OpenReleases);

            if (!_tray.HotkeyRegistered)
                ShowInfoLater($"快捷键 {_config.Hotkey} 被占用，请在设置里换一个。");
        }
        catch (Exception ex)
        {
            Log("tray", ex);
        }

        Task.Run(InitializeEngine);
        Task.Run(CheckUpdateOnStartupAsync);

        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--settings", StringComparison.OrdinalIgnoreCase)))
            _dispatcher.TryEnqueue(ShowSettings);
    }

    private void OnHotkey()
    {
        _dispatcher?.TryEnqueue(BeginCapture);
    }

    private void InitializeEngine()
    {
        try
        {
            _model = ResolveModel(_config.Tier);
            if (_model is null)
            {
                _dispatcher?.TryEnqueue(ShowFirstRun);
                return;
            }

            var engine = new OcrEngine(_model, preferGpu: true);
            if (engine.FallbackReason is not null)
                Log("dml-fallback", new InvalidOperationException(engine.FallbackReason));
            engine.WarmUp();

            lock (_engineLock)
            {
                _engine?.Dispose();
                _engine = engine;
            }
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

    private ModelFiles? ResolveModel(string tier)
    {
        var models = ModelSet.Discover(_modelsRoot);
        if (models.Count == 0) return null;

        if (Enum.TryParse<ModelTier>(tier, ignoreCase: true, out ModelTier wanted))
        {
            var match = models.FirstOrDefault(m => m.Tier == wanted);
            if (match is not null) return match;
        }
        return models.OrderByDescending(m => m.Tier).First();
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
            bool rightCancels = _config.Right == MouseAction.Cancel;
            var overlay = new OverlayWindow(frozen, vx, vy, rightCancels);
            overlay.Selected += (rect, button) =>
            {
                _overlay = null;
                ProcessSelection(frozen, vx, vy, rect, button);
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

    private void ProcessSelection(ImageBuffer frozen, int vx, int vy, Box rect, OverlayButton button)
    {
        MouseAction action = button switch
        {
            OverlayButton.Left => _config.Left,
            OverlayButton.Middle => _config.Middle,
            OverlayButton.Right => _config.Right,
            OverlayButton.DoubleClick => _config.DoubleClick,
            _ => MouseAction.Edit,
        };

        if (action == MouseAction.Cancel) return;

        bool fullScreen = action == MouseAction.FullScreen;
        bool showEditor = action is MouseAction.Edit or MouseAction.FullScreen;
        bool copy = action == MouseAction.CopyClose || _config.AutoCopy;
        Log("action", null, $"button={button} action={action} copy={copy} editor={showEditor}");

        Box region = fullScreen ? new Box(0, 0, frozen.Width, frozen.Height) : rect;
        var crop = frozen.Crop(region.X, region.Y, region.Width, region.Height);

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

                _dispatcher?.TryEnqueue(() =>
                {
                    try
                    {
                        AddHistory(result.Text);
                        if (copy)
                        {
                            CopyText(result.Text);
                            Log("clipboard", null, "ok");
                        }

                        if (showEditor)
                        {
                            ShowResult(result.Text, vx + region.X, vy + region.Y);
                        }
                        else
                        {
                            _tray?.ShowNotification("PiggyOcr", "已复制到剪贴板");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("post", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Log("ocr", ex);
                _dispatcher?.TryEnqueue(() => ShowInfo("识别失败，详情见 PiggyOcr.log。"));
            }
        });
    }

    private void AddHistory(string text)
    {
        _history.Remove(text);
        _history.Insert(0, text);
        while (_history.Count > Math.Max(1, _config.HistoryLimit))
            _history.RemoveAt(_history.Count - 1);
    }

    private void ShowResult(string text, int screenX, int screenY)
    {
        _result?.Close();
        var window = new ResultWindow(text, _history);
        _result = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_result, window)) _result = null;
        };

        var area = DisplayArea.GetFromPoint(new PointInt32(screenX, screenY), DisplayAreaFallback.Primary);
        const int width = 540, height = 380;
        int x = Math.Clamp(screenX, area.WorkArea.X + 8, area.WorkArea.X + area.WorkArea.Width - width - 8);
        int y = Math.Clamp(screenY, area.WorkArea.Y + 8, area.WorkArea.Y + area.WorkArea.Height - height - 8);
        window.AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        SetWindowIcon(window);
        window.Activate();
    }

    private void ShowFirstRun()
    {
        if (_firstRunShown) return;
        _firstRunShown = true;

        Directory.CreateDirectory(_modelsRoot);

        var window = new Window { Title = "PiggyOcr · 首次运行" };
        var panel = new StackPanel { Padding = new Thickness(28), Spacing = 14, MaxWidth = 620 };
        panel.Children.Add(new TextBlock
        {
            Text = "未找到 OCR 模型",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "把任意一个模型包解压后的 tiny / small / medium 文件夹放到下面这个目录：",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = _modelsRoot,
            FontFamily = new FontFamily("Consolas"),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "tiny     6.0 MB   最快，不支持日文\nsmall   29.7 MB   平衡\nmedium  132.4 MB  最准",
            FontFamily = new FontFamily("Consolas"),
            Opacity = 0.85,
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var openFolder = new Button { Content = "打开模型目录" };
        openFolder.Click += (_, _) => OpenModelsFolder();
        var openDownloads = new Button { Content = "打开下载页", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        openDownloads.Click += (_, _) => OpenReleases();
        var close = new Button { Content = "知道了" };
        close.Click += (_, _) => window.Close();
        buttons.Children.Add(openFolder);
        buttons.Children.Add(openDownloads);
        buttons.Children.Add(close);
        panel.Children.Add(buttons);

        window.Content = panel;
        window.AppWindow.Resize(new SizeInt32(660, 420));
        SetWindowIcon(window);
        window.Activate();
    }

    private void ShowSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        _settings = new SettingsWindow(this);
        _settings.Closed += (_, _) => _settings = null;
        var area = DisplayArea.GetFromPoint(new PointInt32(0, 0), DisplayAreaFallback.Primary);
        int x = area.WorkArea.X + (area.WorkArea.Width - 620) / 2;
        int y = area.WorkArea.Y + (area.WorkArea.Height - 760) / 2;
        _settings.AppWindow.MoveAndResize(new RectInt32(Math.Max(area.WorkArea.X, x), Math.Max(area.WorkArea.Y, y), 620, 760));
        SetWindowIcon(_settings);
        _settings.Activate();
    }

    public void ShowInfo(string message)
    {
        var window = new Window { Title = "PiggyOcr" };
        var panel = new StackPanel { Padding = new Thickness(24), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = "知道了", HorizontalAlignment = HorizontalAlignment.Right };
        button.Click += (_, _) => window.Close();
        panel.Children.Add(button);
        window.Content = panel;
        window.AppWindow.Resize(new SizeInt32(440, 200));
        SetWindowIcon(window);
        window.Activate();
    }

    private static void SetWindowIcon(Window window)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "PiggyOcr.ico");
            if (File.Exists(path)) window.AppWindow.SetIcon(path);
        }
        catch
        {
            // the icon is cosmetic; never fail a window over it
        }
    }

    private void ShowInfoLater(string message)
    {
        _dispatcher?.TryEnqueue(() => ShowInfo(message));
    }

    public void OpenModelsFolder()
    {
        try
        {
            Directory.CreateDirectory(_modelsRoot);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_modelsRoot}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log("models-folder", ex);
        }
    }

    public void OpenReleases()
    {
        try
        {
            Process.Start(new ProcessStartInfo(UpdateService.ReleasesUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log("update", ex);
        }
    }

    public void SaveConfig()
    {
        ConfigStore.Save(_config);
    }

    public bool ApplyHotkey(string text)
    {
        if (!HotkeyParser.TryParse(text, out uint modifiers, out uint key)) return false;
        if (_tray is null) return false;

        bool ok = _tray.SetHotkey(modifiers | 0x4000, key);
        if (!ok) return false;

        _config.Hotkey = text;
        SaveConfig();
        return true;
    }

    public void ApplyAutoStart(bool enabled)
    {
        _config.AutoStart = enabled;
        AutoStart.Set(enabled);
        SaveConfig();
    }

    public void ApplyTier(string tier)
    {
        _config.Tier = tier;
        SaveConfig();

        Task.Run(() =>
        {
            try
            {
                var model = ResolveModel(tier);
                if (model is null) return;
                var engine = new OcrEngine(model, preferGpu: true);
                engine.WarmUp();
                lock (_engineLock)
                {
                    _engine?.Dispose();
                    _engine = engine;
                }
            }
            catch (Exception ex)
            {
                Log("tier", ex);
            }
        });
    }

    public IReadOnlyList<ModelFiles> InstalledModels() => ModelSet.Discover(_modelsRoot);

    private async Task CheckUpdateOnStartupAsync()
    {
        if (!_config.CheckUpdate) return;
        if (DateTimeOffset.Now - _config.LastUpdateCheck < TimeSpan.FromDays(7)) return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            string? tag = await UpdateService.GetLatestTagAsync();
            _config.LastUpdateCheck = DateTimeOffset.Now;
            SaveConfig();

            if (UpdateService.IsNewer(tag, CurrentVersion))
            {
                _dispatcher?.TryEnqueue(() =>
                    _tray?.ShowNotification("PiggyOcr 有新版本", $"{tag} 已发布，点击打开下载页"));
            }
        }
        catch (Exception ex)
        {
            Log("update-check", ex);
        }
    }

    private static void CopyText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private void Shutdown()
    {
        _overlay?.Dispose();
        _overlay = null;
        _result?.Close();
        _settings?.Close();
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

    private static readonly object LogLock = new();
    public static bool LogEnabled { get; set; } = true;

    private static void Log(string tag, Exception? ex, string? message = null)
    {
        if (!LogEnabled) return;

        try
        {
            lock (LogLock)
            {
                string path = Path.Combine(AppContext.BaseDirectory, "PiggyOcr.log");
                Rotate(path);
                string line = $"[{DateTime.Now:O}] {tag}: {message}{(ex is null ? "" : " " + ex)}";
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // logging must never take the app down
        }
    }

    private static void Rotate(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1_000_000) return;

        string third = path + ".3";
        if (File.Exists(third)) File.Delete(third);
        for (int i = 2; i >= 1; i--)
        {
            string from = path + "." + i;
            string to = path + "." + (i + 1);
            if (File.Exists(from)) File.Move(from, to, overwrite: true);
        }
        File.Move(path, path + ".1", overwrite: true);
    }
}
