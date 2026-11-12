using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PiggyOcr.App.Services;
using PiggyOcr.Platform;
using Windows.System;

namespace PiggyOcr.App;

public sealed partial class SettingsWindow : Window
{
    private readonly App _app;
    private bool _loading = true;
    private bool _capturing;

    public SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        Title = "PiggyOcr · 设置";

        var config = app.Config;
        HotkeyBox.Text = config.Hotkey;
        LeftCombo.SelectedIndex = (int)config.Left;
        MiddleCombo.SelectedIndex = (int)config.Middle;
        RightCombo.SelectedIndex = (int)config.Right;
        DoubleCombo.SelectedIndex = (int)config.DoubleClick;
        AutoCopySwitch.IsOn = config.AutoCopy;
        AutoStartSwitch.IsOn = config.AutoStart;
        CheckUpdateSwitch.IsOn = config.CheckUpdate;
        TierCombo.SelectedIndex = config.Tier.ToLowerInvariant() switch
        {
            "tiny" => 1,
            "small" => 2,
            "medium" => 3,
            _ => 0,
        };

        var installed = app.InstalledModels();
        TierHint.Text = installed.Count == 0
            ? "未安装任何模型。"
            : "已安装：" + string.Join("、", installed.Select(m => m.Tier.ToString().ToLowerInvariant()));
        BackendText.Text = "当前后端：" + app.BackendText;
        VersionText.Text = $"v{App.CurrentVersion.ToString(3)}";

        Activated += (_, _) => BackendText.Text = "当前后端：" + _app.BackendText;

        var timer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => BackendText.Text = "当前后端：" + _app.BackendText;
        timer.Start();
        Closed += (_, _) => timer.Stop();

        _loading = false;
    }

    private void OnHotkeyFocus(object sender, RoutedEventArgs e)
    {
        _capturing = true;
        HotkeyBox.Text = "按下组合键…";
        HotkeyHint.Text = "按 Esc 取消。";
    }

    private void OnHotkeyLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_capturing) return;
        _capturing = false;
        HotkeyBox.Text = _app.Config.Hotkey;
    }

    private void OnHotkeyKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;

        if (e.Key == VirtualKey.Escape)
        {
            _capturing = false;
            HotkeyBox.Text = _app.Config.Hotkey;
            HotkeyHint.Text = "已取消。";
            return;
        }

        if (e.Key is VirtualKey.Shift or VirtualKey.Control or VirtualKey.Menu
            or VirtualKey.LeftWindows or VirtualKey.RightWindows)
            return;

        uint key = (uint)e.Key;
        uint modifiers = KeyboardState.CurrentModifiers();
        bool isFunctionKey = key is >= 0x70 and <= 0x87;

        if (modifiers == 0 && !isFunctionKey)
        {
            HotkeyHint.Text = "需要至少一个修饰键，或使用 F1–F24。";
            return;
        }

        string text = HotkeyParser.Format(modifiers, key);
        if (_app.ApplyHotkey(text))
        {
            HotkeyBox.Text = text;
            HotkeyHint.Text = "已生效。";
            _capturing = false;
        }
        else
        {
            HotkeyHint.Text = "这个组合键被占用了，换一个。";
        }
    }

    private void OnMouseActionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var config = _app.Config;
        config.Left = (MouseAction)Math.Clamp(LeftCombo.SelectedIndex, 0, 3);
        config.Middle = (MouseAction)Math.Clamp(MiddleCombo.SelectedIndex, 0, 3);
        config.Right = (MouseAction)Math.Clamp(RightCombo.SelectedIndex, 0, 3);
        config.DoubleClick = (MouseAction)Math.Clamp(DoubleCombo.SelectedIndex, 0, 3);
        _app.SaveConfig();
    }

    private void OnTierChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        string tier = TierCombo.SelectedIndex switch
        {
            1 => "tiny",
            2 => "small",
            3 => "medium",
            _ => "auto",
        };
        BackendText.Text = "正在切换模型…";
        _app.ApplyTier(tier);
    }

    private void OnAutoCopyToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _app.Config.AutoCopy = AutoCopySwitch.IsOn;
        _app.SaveConfig();
    }

    private void OnAutoStartToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _app.ApplyAutoStart(AutoStartSwitch.IsOn);
    }

    private void OnCheckUpdateToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _app.Config.CheckUpdate = CheckUpdateSwitch.IsOn;
        _app.SaveConfig();
    }

    private void OnCheckUpdate(object sender, RoutedEventArgs e) => _app.OpenReleases();

    private void OnOpenModels(object sender, RoutedEventArgs e) => _app.OpenModelsFolder();
}
