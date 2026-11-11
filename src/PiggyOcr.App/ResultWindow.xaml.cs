using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace PiggyOcr.App;

public sealed partial class ResultWindow : Window
{
    private readonly string _original;

    public ResultWindow(string text)
    {
        InitializeComponent();
        Title = "PiggyOcr · 识别结果";

        _original = text;
        Text.Text = text;
        Count.Text = $"{text.Length} 字";

        Activated += OnActivated;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState is WindowActivationState.CodeActivated or WindowActivationState.PointerActivated)
        {
            Text.Focus(FocusState.Programmatic);
            Text.SelectionStart = Text.Text.Length;
        }
        else if (args.WindowActivationState == WindowActivationState.Deactivated && Text.Text == _original)
        {
            // policy C: unmodified -> click outside closes; edited -> stay open
            Close();
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        Count.Text = $"{Text.Text.Length} 字";
        CopyButton.Content = "复制";
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyText();

    private void OnCopyClose(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        CopyText();
        Close();
    }

    private void OnEscape(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnJoinClick(object sender, RoutedEventArgs e)
    {
        if (JoinLines.IsChecked != true) return;
        Text.Text = Text.Text.Replace("\r\n", " ").Replace('\n', ' ');
    }

    private void CopyText()
    {
        var package = new DataPackage();
        package.SetText(Text.Text);
        Clipboard.SetContent(package);
        CopyButton.Content = "已复制";
    }
}
