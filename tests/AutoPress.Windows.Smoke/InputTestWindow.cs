using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AutoPress.Windows.Smoke;

internal sealed class InputTestWindow : Window
{
    private readonly TextBlock key = new() { Text = "—", FontSize = 70, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 12) };
    private readonly TextBlock count = new() { Text = "0 keystrokes received", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 24, 0, 0) };
    private int received;

    public InputTestWindow()
    {
        SetResourceReference(StyleProperty, typeof(Window));
        Title = "AutoPress · Input Monitor";
        Width = 490; Height = 370; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(30) };
        panel.Children.Add(new TextBlock { Text = "Input Monitor", FontSize = 24, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "This window displays received input without performing actions.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
        panel.Children.Add(key); panel.Children.Add(count); panel.Children.Add(status);
        Content = panel;
        PreviewKeyDown += (_, e) =>
        {
            Key actual = e.Key == Key.System ? e.SystemKey : e.Key;
            if (actual is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift) return;
            received++;
            key.Text = actual == Key.Space ? "␣" : actual.ToString();
            count.Text = $"{received:N0} keystrokes received";
            e.Handled = true;
        };
    }
    public void SetState(bool active, string hotkey) => status.Text = active
        ? $"Session running. Press {hotkey} to stop."
        : $"Press {hotkey}. Input starts after a three-second countdown.";
}
