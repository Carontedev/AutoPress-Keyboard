using System.ComponentModel;
using System.Globalization;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using AutoPress.Core;
using AutoPress.Windows;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace AutoPress.App;

public partial class MainWindow : Window
{
    private sealed record ModifierChoice(uint Value, string Label) { public override string ToString() => Label; }
    private readonly DispatcherTimer uiTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly AppSettings loaded;
    private readonly string? loadWarning;
    private GlobalHotkeys? hotkeys;
    private HwndSource? source;
    private AutomationSession? session;
    private SessionRequest? currentRequest;
    private Forms.NotifyIcon? tray;
    private Hotkey preferredHotkey;
    private bool restoringHotkey;
    private bool initialized, closing, allowClose, toggling;
    private string theme = "System";
    private string? transientError;

    public MainWindow()
    {
        (loaded, loadWarning) = SettingsStore.Load();
        preferredHotkey = loaded.Hotkey;
        InitializeComponent();

        repeatKey = SessionRequest.IsSupportedKey(loaded.Key) ? loaded.Key : 0x46;
        KeyPicker.Content = KeyLabel(repeatKey);
        IntervalInput.Text = Math.Clamp(loaded.IntervalMs, 50, 60_000).ToString(CultureInfo.InvariantCulture);
        LimitPicker.SelectedIndex = loaded.LimitMode == "Keystrokes" ? 1 : 0;
        LimitInput.Text = Math.Clamp(loaded.LimitValue, 1, loaded.LimitMode == "Keystrokes" ? 1_000_000 : 86_400).ToString(CultureInfo.InvariantCulture);
        UnlimitedCheckBox.IsChecked = loaded.Unlimited;
        ModifiersPicker.ItemsSource = new[] { new ModifierChoice(0, "None"), new ModifierChoice(2, "Ctrl"), new ModifierChoice(1, "Alt"), new ModifierChoice(4, "Shift"), new ModifierChoice(3, "Ctrl + Alt"), new ModifierChoice(6, "Ctrl + Shift") };
        ModifiersPicker.SelectedItem = ModifiersPicker.Items.Cast<ModifierChoice>().FirstOrDefault(x => x.Value == loaded.Hotkey.Modifiers) ?? ModifiersPicker.Items[0];
        HotkeyPicker.ItemsSource = Enumerable.Range(1, 11).Select(n => $"F{n}").ToArray();
        HotkeyPicker.SelectedIndex = (int)loaded.Hotkey.Key - 0x70;
        RefreshTargetList(loaded.TargetPath);
        theme = loaded.Theme is "Light" or "Dark" ? loaded.Theme : "System";
        ApplyTheme();
        initialized = true;
        UpdateConfigurationSummary();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        LocationChanged += (_, _) => UpdateWindowBounds(false);
        Deactivated += (_, _) => CancelKeyCapture();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && tray is not null) Hide(); };
        uiTimer.Tick += (_, _) => RenderSnapshot();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        Application.Current.SessionEnding += OnSessionEnding;
    }

    private int repeatKey = 0x46;
    private bool capturingKey, suppressCaptureKeyUp;

    private static string KeyLabel(int key) => key switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)key).ToString(),
        >= 0x70 and <= 0x7B => $"F{key - 0x70 + 1}",
        0x20 => "Space", 0x0D => "Enter", 0x09 => "Tab",
        0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
        0x24 => "Home", 0x23 => "End", 0x21 => "Page Up", 0x22 => "Page Down",
        _ => "F"
    };

    private void CaptureKey(object sender, RoutedEventArgs e)
    {
        if (session?.Snapshot.IsActive == true || closing) return;
        if (session?.NeedsKeyRelease == true) { SetFeedback("Release the pending key before changing settings.", true); return; }
        if (capturingKey) { CancelKeyCapture(); return; }
        KeyPicker.Focus();
        capturingKey = true;
        suppressCaptureKeyUp = false;
        KeyPicker.Content = "Press a key…";
        KeyPicker.SetResourceReference(BorderBrushProperty, "AccentBrush");
        SetFeedback("Press a key to assign it. Esc cancels.", false);
    }

    private void CancelKeyCapture()
    {
        if (!capturingKey) return;
        EndKeyCapture();
        SetFeedback("Key capture cancelled.", false);
    }

    private void EndKeyCapture()
    {
        capturingKey = false;
        KeyPicker.Content = KeyLabel(repeatKey);
        KeyPicker.ClearValue(BorderBrushProperty);
    }

    private void CaptureFocusLost(object sender, KeyboardFocusChangedEventArgs e) => CancelKeyCapture();

    private void CaptureKeyDown(object sender, KeyEventArgs e)
    {
        if (!capturingKey) return;
        e.Handled = true;
        if (e.IsRepeat) return;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            suppressCaptureKeyUp = true;
            CancelKeyCapture();
            return;
        }
        int code = KeyInterop.VirtualKeyFromKey(key);
        if (code == (hotkeys?.Current.Key ?? Hotkey.Default.Key)
            || code == SelectedHotkey().Key || code == Hotkey.Emergency.Key)
        {
            SetFeedback("That key is reserved for a shortcut. Press another key, or Esc to cancel.", true);
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.None || !SessionRequest.IsSupportedKey(code))
        {
            SetFeedback("Use a single letter, number, function, or navigation key. Esc cancels.", true);
            return;
        }
        repeatKey = code;
        suppressCaptureKeyUp = true;
        EndKeyCapture();
        ConfigChanged(sender, e);
        try
        {
            SaveSettings();
            SetFeedback($"Repeat key set to {KeyLabel(repeatKey)}.", false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetFeedback("The key was changed, but preferences could not be saved.", true);
        }
    }

    private void CaptureKeyUp(object sender, KeyEventArgs e)
    {
        if (capturingKey || suppressCaptureKeyUp) e.Handled = true;
        suppressCaptureKeyUp = false;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        nint handle = new WindowInteropHelper(this).Handle;
        source = HwndSource.FromHwnd(handle);
        UpdateWindowBounds(false);
        source?.AddHook(WindowMessage);
        ApplyTheme();
        hotkeys = new GlobalHotkeys(handle);
        TryApplyHotkey();
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "AutoPress · Idle", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open AutoPress", null, (_, _) => Dispatcher.Invoke(ShowWindow));
        menu.Items.Add("Stop", null, (_, _) => Dispatcher.InvokeAsync(() => StopAsync("Stopped from the system tray")));
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(Close));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow);
        if (loadWarning is not null) SetFeedback(loadWarning, true);
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is 0x007E or 0x001A) // Display/work-area settings changed.
            Dispatcher.BeginInvoke(() => UpdateWindowBounds(true));
        if (message != GlobalHotkeys.MessageId || hotkeys is null) return 0;
        int id = (int)wParam;
        if (capturingKey && (id == GlobalHotkeys.EmergencyId || hotkeys.IsToggleId(id)))
        {
            handled = true;
            SetFeedback("That key is reserved for a shortcut. Press another key, or Esc to cancel.", true);
            return 0;
        }
        if (id == GlobalHotkeys.EmergencyId)
        {
            handled = true;
            _ = StopAsync("Emergency stop");
        }
        else if (hotkeys.IsToggleId(id))
        {
            handled = true;
            _ = ToggleAsync();
        }
        return 0;
    }

    private async void ToggleClick(object sender, RoutedEventArgs e) => await ToggleAsync();

    private async Task ToggleAsync()
    {
        if (toggling || closing || capturingKey) return;
        toggling = true;
        try
        {
            if (session?.NeedsKeyRelease == true) { await StopAsync("Pending key released"); return; }
            if (session?.Snapshot.IsActive == true) { await StopAsync(); return; }
            if (hotkeys is null || !hotkeys.IsToggleId(9101) && !hotkeys.IsToggleId(9102))
                throw new InvalidOperationException("Register an available shortcut before starting.");
            if (SelectedHotkey() != hotkeys.Current) throw new InvalidOperationException("Click Apply to register the new shortcut.");
            SessionRequest request = ReadRequest();
            if (request.Key == hotkeys.Current.Key || request.Key == Hotkey.Emergency.Key)
                throw new InvalidOperationException("The repeat key must differ from the toggle and emergency shortcut keys.");
            string? targetPath = (TargetPicker.SelectedItem as WindowTarget)?.Path;
            session = new AutomationSession(new WindowsKeyboardOutput(), string.IsNullOrEmpty(targetPath) ? null : () =>
            {
                if (!string.Equals(WindowTargets.ForegroundPath(), targetPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The selected application must be focused when the countdown ends. Select Any active window to start anywhere.");
            });
            currentRequest = request;
            transientError = null;
            SaveSettings();
            session.Start(request);
            Configuration.IsEnabled = false;
            RenderSnapshot();
            uiTimer.Start();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            SetFeedback(error.Message, true);
        }
        finally { toggling = false; }
    }

    private SessionRequest ReadRequest()
    {
        if (!int.TryParse(IntervalInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int interval))
            throw new ArgumentException("Enter the interval as a whole number of milliseconds.");
        bool unlimited = UnlimitedCheckBox.IsChecked == true;
        int limit = 0;
        if (!unlimited && !int.TryParse(LimitInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out limit))
            throw new ArgumentException("Enter a whole number for the session limit.");
        var request = new SessionRequest(repeatKey, interval, !unlimited && LimitPicker.SelectedIndex == 0 ? TimeSpan.FromSeconds(limit) : null,
            !unlimited && LimitPicker.SelectedIndex == 1 ? limit : null, TimeSpan.FromSeconds(3), unlimited);
        request.Validate();
        return request;
    }

    private async Task StopAsync(string reason = "Stopped with the toggle shortcut")
    {
        if (session is not null) await session.StopAsync(reason);
        transientError = null;
        RenderSnapshot();
    }

    private void RenderSnapshot()
    {
        var state = session?.Snapshot ?? SessionSnapshot.Initial;
        bool active = state.IsActive;
        bool recovery = session?.NeedsKeyRelease == true;
        Configuration.IsEnabled = !active && !recovery;
        StateText.Text = state.Status switch
        {
            SessionStatus.Countdown => $"●  Starting in {Math.Ceiling(state.RemainingCountdown.TotalSeconds):0}…",
            SessionStatus.Running => "●  Running",
            SessionStatus.Faulted => "●  Error",
            _ => "●  Idle"
        };
        StateBadge.Background = state.Status == SessionStatus.Running ? new SolidColorBrush(Color.FromArgb(40, 48, 209, 88)) : (Brush)Application.Current.Resources["InputBrush"];
        StateText.SetResourceReference(TextBlock.ForegroundProperty, state.Status == SessionStatus.Running ? "SuccessBrush" : "MutedBrush");
        ToggleButton.Content = recovery ? $"Release key  ·  {hotkeys?.Current}"
            : active ? $"Stop  ·  {hotkeys?.Current}" : $"Start  ·  {hotkeys?.Current}";
        ToggleButton.SetResourceReference(BackgroundProperty, active ? "DangerBrush" : "AccentBrush");
        CountText.Text = state.Presses.ToString("N0", CultureInfo.GetCultureInfo("en-US"));
        ElapsedText.Text = state.Elapsed.TotalHours >= 1 ? $"{(long)state.Elapsed.TotalHours:00}:{state.Elapsed.Minutes:00}:{state.Elapsed.Seconds:00}" : state.Elapsed.ToString(@"mm\:ss");
        SessionProgress.Visibility = (active ? currentRequest?.Unlimited == true : UnlimitedCheckBox.IsChecked == true)
            ? Visibility.Hidden : Visibility.Visible;
        SessionProgress.Value = currentRequest?.Duration is { } duration ? Math.Min(100, state.Elapsed.TotalSeconds / duration.TotalSeconds * 100)
            : currentRequest?.MaxPresses is { } max ? Math.Min(100, state.Presses * 100.0 / max) : 0;
        if (transientError is null) SetFeedback(state.Message, state.Status == SessionStatus.Faulted, false);
        if (tray is not null) tray.Text = $"AutoPress · {(active ? "Running" : "Idle")} · {hotkeys?.Current}";
        if (!active) uiTimer.Stop();
    }

    private void SetFeedback(string message, bool error, bool remember = true)
    {
        if (remember) transientError = error ? message : null;
        FeedbackText.Text = message;
        FeedbackText.SetResourceReference(TextBlock.ForegroundProperty, error ? "DangerBrush" : "MutedBrush");
    }

    private void ConfigChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        transientError = null;
        UpdateConfigurationSummary();
        if (session?.Snapshot.Status == SessionStatus.Faulted && !session.NeedsKeyRelease)
        {
            session = null;
            currentRequest = null;
        }
        RenderSnapshot();
    }

    private void UpdateConfigurationSummary()
    {
        bool unlimited = UnlimitedCheckBox.IsChecked == true;
        LimitControls.IsEnabled = !unlimited;
        LimitHint.Text = unlimited ? "Runs until you stop it. No time or keystroke limit."
            : "Stop automatically after the duration or keystroke count below.";
        LimitControls.Opacity = unlimited ? 0.5 : 1;
        SessionProgress.Visibility = unlimited ? Visibility.Hidden : Visibility.Visible;
        LimitLabel.Text = LimitPicker.SelectedIndex == 1 ? "Keystroke limit" : "Duration (seconds)";
        string limit = unlimited ? "No limit" : LimitPicker.SelectedIndex == 1 ? $"{LimitInput.Text} keystrokes" : $"{LimitInput.Text} s";
        string target = (TargetPicker.SelectedItem as WindowTarget)?.Name ?? "Any active window";
        if (target.Length > 40) target = target[..37] + "…";
        TargetSummaryText.Text = unlimited ? $"{target}  ·  No limit" : $"{target}  ·  Limit: {limit}";
        TargetSummaryButton.ToolTip = "Click to change the target application or session limit.";
    }

    private Hotkey SelectedHotkey() => new((uint)(0x70 + HotkeyPicker.SelectedIndex), (ModifiersPicker.SelectedItem as ModifierChoice)?.Value ?? 0);
    private void UnlimitedChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        ConfigChanged(sender, e);
        try { SaveSettings(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetFeedback("The session limit was changed, but preferences could not be saved.", true);
        }
    }
    private void HotkeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!restoringHotkey && initialized && hotkeys is not null) ApplyHotkeyButton.IsEnabled = SelectedHotkey() != hotkeys.Current;
    }
    private void ApplyHotkey(object sender, RoutedEventArgs e) => TryApplyHotkey();
    private void TryApplyHotkey()
    {
        try
        {
            if (SelectedHotkey().Key == repeatKey)
                throw new InvalidOperationException("Choose a toggle shortcut different from the repeat key.");
            hotkeys?.Register(SelectedHotkey());
            preferredHotkey = SelectedHotkey();
            ApplyHotkeyButton.IsEnabled = false;
            SetFeedback($"{SelectedHotkey()} starts or stops the session.", false);
            ToggleButton.Content = $"Start  ·  {SelectedHotkey()}";
            HotkeySummaryButton.Content = SelectedHotkey().ToString();
            SaveSettings();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            if (hotkeys?.Current.IsValid == true)
            {
                restoringHotkey = true;
                try
                {
                    HotkeyPicker.SelectedIndex = (int)hotkeys.Current.Key - 0x70;
                    ModifiersPicker.SelectedItem = ModifiersPicker.Items.Cast<ModifierChoice>()
                        .First(x => x.Value == hotkeys.Current.Modifiers);
                }
                finally { restoringHotkey = false; }
                ApplyHotkeyButton.IsEnabled = false;
            }
            else ApplyHotkeyButton.IsEnabled = true;
            SetFeedback(error.Message, true);
        }
    }

    private void RefreshTargets(object sender, RoutedEventArgs e) => RefreshTargetList((TargetPicker.SelectedItem as WindowTarget)?.Path);
    private void RefreshTargetList(string? selectedPath)
    {
        var targets = new List<WindowTarget> { new("Any active window", "") };
        targets.AddRange(WindowTargets.List());
        if (!string.IsNullOrEmpty(selectedPath) && !targets.Any(x => string.Equals(x.Path, selectedPath, StringComparison.OrdinalIgnoreCase)))
            targets.Add(new($"{Path.GetFileNameWithoutExtension(selectedPath)} (not running)", selectedPath));
        TargetPicker.ItemsSource = targets;
        TargetPicker.SelectedItem = string.IsNullOrEmpty(selectedPath) ? targets[0]
            : targets.First(x => string.Equals(x.Path, selectedPath, StringComparison.OrdinalIgnoreCase));
    }

    private void ChangeTheme(object sender, RoutedEventArgs e)
    {
        theme = theme switch { "System" => "Dark", "Dark" => "Light", _ => "System" };
        ApplyTheme();
    }
    private void ApplyTheme()
    {
        bool dark = theme == "Dark";
        if (theme == "System")
        {
            try { dark = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
            catch (System.Security.SecurityException) { dark = false; }
        }
        string[] keys = ["CanvasBrush", "SurfaceBrush", "InputBrush", "TextBrush", "MutedBrush", "BorderBrush", "AccentBrush", "DangerBrush", "GlassEdgeBrush", "SuccessBrush"];
        string[] colors = dark ? ["#252528", "#EA29292E", "#1FFFFFFF", "#F5F5F7", "#B8B8C0", "#30FFFFFF", "#007AFF", "#FF6961", "#38FFFFFF", "#63E68B"]
            : ["#F2F2F4", "#EBF5F5F7", "#8FFFFFFF", "#1D1D1F", "#606069", "#20000000", "#0069DF", "#BC302B", "#90FFFFFF", "#196C38"];
        for (int i = 0; i < keys.Length; i++) Application.Current.Resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        bool acrylic = WindowBackdrop.Apply(source?.Handle ?? 0, dark);
        string tint = acrylic ? (dark ? "#55222226" : "#66F7F7FA") : (dark ? "#FF252528" : "#FFF2F2F4");
        Application.Current.Resources["GlassBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(tint));
        ThemeButton.Content = $"Theme: {theme}";
    }

    private void ToggleAdvanced(object sender, RoutedEventArgs e)
    {
        AdvancedPanel.Visibility = AdvancedPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        Dispatcher.BeginInvoke(() => UpdateWindowBounds(true));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(() => UpdateWindowBounds(true));
    }

    private void UpdateWindowBounds(bool reposition)
    {
        if (source is null || closing) return;
        var pixels = Forms.Screen.FromHandle(source.Handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var area = new Rect(pixels.Left / dpi.DpiScaleX, pixels.Top / dpi.DpiScaleY,
            pixels.Width / dpi.DpiScaleX, pixels.Height / dpi.DpiScaleY);
        MaxHeight = area.Height;
        MaxWidth = area.Width;
        if (!reposition || WindowState != WindowState.Normal) return;
        UpdateLayout();
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
    }


    private void SaveSettings()
    {
        SettingsStore.Save(new AppSettings
        {
            Key = repeatKey,
            IntervalMs = int.TryParse(IntervalInput.Text, out int interval) ? interval : 100,
            LimitMode = LimitPicker.SelectedIndex == 1 ? "Keystrokes" : "Duration",
            LimitValue = int.TryParse(LimitInput.Text, out int limit) ? limit : 300,
            Unlimited = UnlimitedCheckBox.IsChecked == true,
            Hotkey = preferredHotkey,
            Theme = theme,
            TargetPath = (TargetPicker.SelectedItem as WindowTarget)?.Path is { Length: > 0 } path ? path : null
        });
    }

    private async void ShowAbout(object sender, RoutedEventArgs e)
    {
        await StopAsync("Stopped to open preferences");
        if (session?.NeedsKeyRelease == true) return;
        var dialog = new Window { Title = "AutoPress · About & preferences", Width = 510, Height = 390, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(28) };
        string version = typeof(MainWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().First().InformationalVersion.Split('+')[0];
        panel.Children.Add(new TextBlock { Text = $"AutoPress Keyboard {version}", FontSize = 23, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Use automated input only where permitted. Game and server rules may prohibit macros; this application cannot guarantee protection from account penalties.\n\nRuns locally without accounts, network connections, or keystroke logging. Global shortcuts remain available when minimized to the system tray.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 20) });
        var reset = new Button { Content = "Restore defaults" };
        reset.Click += (_, _) =>
        {
            try
            {
                if (File.Exists(SettingsStore.FilePath)) File.Delete(SettingsStore.FilePath);
                repeatKey = 0x46; EndKeyCapture(); IntervalInput.Text = "100"; LimitPicker.SelectedIndex = 0; LimitInput.Text = "300";
                UnlimitedCheckBox.IsChecked = false;
                HotkeyPicker.SelectedIndex = 7; ModifiersPicker.SelectedIndex = 0; TargetPicker.SelectedIndex = 0;
                theme = "System"; ApplyTheme(); TryApplyHotkey();
                dialog.Close();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { MessageBox.Show(dialog, error.Message, "Preferences"); }
        };
        panel.Children.Add(reset);
        dialog.Content = panel;
        dialog.ShowDialog();
    }

    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) session?.StopAsync("Windows entered sleep mode").GetAwaiter().GetResult();
    }
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
            session?.StopAsync("Windows session locked or disconnected").GetAwaiter().GetResult();
    }
    private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) { if (theme == "System") Dispatcher.InvokeAsync(ApplyTheme); }
    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e) => session?.StopAsync("Windows session is ending").GetAwaiter().GetResult();

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        await StopAsync("Application closed");
        if (session?.NeedsKeyRelease == true)
        {
            closing = false;
            _ = Dispatcher.BeginInvoke(ShowWindow);
            return; // Keep emergency/retry controls alive instead of abandoning a held key.
        }
        try { SaveSettings(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* No input remains active. */ }
        hotkeys?.Dispose();
        source?.RemoveHook(WindowMessage);
        uiTimer.Stop();
        tray?.ContextMenuStrip?.Dispose();
        tray?.Dispose();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        Application.Current.SessionEnding -= OnSessionEnding;
        allowClose = true;
        _ = Dispatcher.BeginInvoke(Close);
    }
}
