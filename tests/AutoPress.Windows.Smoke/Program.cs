using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoPress.App;
using AutoPress.Windows;
using AutoPress.Windows.Smoke;

internal static class Program
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out WindowRect bounds);
    [DllImport("user32.dll")] private static extern bool PrintWindow(nint window, nint destination, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    private static readonly string QaDirectory = Path.Combine(Environment.CurrentDirectory, "artifacts", "qa");

    [STAThread]
    private static int Main()
    {
        Environment.SetEnvironmentVariable("AUTOPRESS_SETTINGS_DIRECTORY", Path.Combine(QaDirectory, "settings"));
        CheckSettingsMigration();
        var app = new AutoPress.App.App();
        app.InitializeComponent();
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await Task.Delay(350);
                if (app.MainWindow is not MainWindow main) throw new Exception("No se abrió la ventana principal.");
                await CheckAsync(main);
                Console.WriteLine(Environment.GetCommandLineArgs().Contains("--render-only")
                    ? "PASS: WPF render, themes, validation, hotkey registration and cancelling countdown. No keyboard input sent."
                    : "PASS: WPF render, real SendInput, global hotkey toggle, focus changes, emergency and tray.");
                SendMessage(new WindowInteropHelper(main).Handle, 0x0112, 0xF060, 0); // Native SC_CLOSE.
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                app.Shutdown(1);
            }
        });
        return app.Run();
    }

    private static T Find<T>(MainWindow main, string name) where T : FrameworkElement => (T)main.FindName(name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void SendCaptureKey(MainWindow main, Button button, Key key)
    {
        // Routed events only: never inject a physical key into the desktop.
        var inputSource = PresentationSource.FromVisual(main)!;
        var down = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        button.RaiseEvent(down);
        var up = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, Environment.TickCount, key)
            { RoutedEvent = Keyboard.PreviewKeyUpEvent };
        button.RaiseEvent(up);
        if (!down.Handled || !up.Handled) throw new Exception("Capture must consume key down and key up.");
    }

    private static void CheckKeyCapture(MainWindow main)
    {
        var button = Find<Button>(main, "KeyPicker");
        foreach (var key in new[] { Key.W, Key.D7, Key.Space, Key.Tab, Key.Return, Key.Left, Key.Home, Key.F6 })
        {
            Click(button);
            if (button.Content?.ToString() != "Press a key…") throw new Exception("Capture did not start.");
            SendCaptureKey(main, button, key);
            if (SettingsStore.Load().Settings.Key != KeyInterop.VirtualKeyFromKey(key))
                throw new Exception($"Captured key {key} was not persisted.");
        }
        Click(button);
        SendCaptureKey(main, button, Key.Escape);
        if (button.Content?.ToString() != "F6") throw new Exception("Escape changed the previous key.");
        Click(button);
        Capture(main, "autopress-key-capture.png");
        foreach (var reserved in new[] { Key.F8, Key.F10, Key.LeftCtrl, Key.Back })
        {
            SendCaptureKey(main, button, reserved);
            if (button.Content?.ToString() != "Press a key…" || SettingsStore.Load().Settings.Key != 0x75)
                throw new Exception("A reserved or unsupported key was accepted.");
        }
        nint handle = new WindowInteropHelper(main).Handle;
        foreach (int id in new[] { 9101, 9102, GlobalHotkeys.EmergencyId })
            SendMessage(handle, GlobalHotkeys.MessageId, id, 0);
        if (!Find<Button>(main, "ToggleButton").Content.ToString()!.StartsWith("Start"))
            throw new Exception("A global shortcut started automation during capture.");
        button.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount,
            button, Find<TextBox>(main, "IntervalInput")) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
        if (button.Content?.ToString() != "F6") throw new Exception("Losing focus must cancel capture.");
        Find<ComboBox>(main, "HotkeyPicker").SelectedIndex = 5;
        Click(Find<Button>(main, "ApplyHotkeyButton"));
        if (!Find<TextBlock>(main, "FeedbackText").Text.Contains("different from the repeat key")
            || !Find<Button>(main, "ToggleButton").Content.ToString()!.Contains("F8"))
            throw new Exception("A shortcut collision with the repeat key must preserve the existing shortcut.");
        Find<ComboBox>(main, "HotkeyPicker").SelectedIndex = 7;
        Click(Find<Button>(main, "ApplyHotkeyButton"));
        Click(button);
        SendCaptureKey(main, button, Key.F);
        if (button.Content?.ToString() != "F" || Find<TextBlock>(main, "CountText").Text != "0")
            throw new Exception("Key capture unexpectedly started automation.");
        Console.WriteLine("PASS: click-to-capture, key persistence, Escape, focus cancellation, reserved keys, and shortcut isolation.");
    }

    private static void SelectTheme(Button button, string theme)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (button.Content?.ToString() == $"Theme: {theme}") return;
            Click(button);
        }
        if (button.Content?.ToString() != $"Theme: {theme}")
            throw new Exception($"Could not select the {theme} theme.");
    }

    private static void CheckSettingsMigration()
    {
        foreach (var (legacyTheme, expectedTheme) in new[] { ("Sistema", "System"), ("Claro", "Light"), ("Oscuro", "Dark") })
        {
            var legacy = new AppSettings { Version = 1, Theme = legacyTheme, LimitMode = "Pulsaciones",
                Key = 0x57, IntervalMs = 120, LimitValue = 42, TargetPath = @"C:\QA\Example.exe" };
            SettingsStore.Save(legacy);
            var (settings, warning) = SettingsStore.Load();
            if (warning is not null || settings.Version != 2 || settings.Theme != expectedTheme
                || settings.LimitMode != "Keystrokes" || settings.Key != legacy.Key
                || settings.IntervalMs != 120 || settings.LimitValue != 42
                || settings.Hotkey != legacy.Hotkey || settings.TargetPath != legacy.TargetPath || settings.Unlimited)
                throw new Exception("Legacy settings migration did not preserve the configuration.");
        }
        SettingsStore.Save(new AppSettings { Version = 1, Theme = "Sistema", LimitMode = "Tiempo" });
        if (SettingsStore.Load().Settings.LimitMode != "Duration")
            throw new Exception("Legacy duration mode was not migrated.");
        SettingsStore.Save(new AppSettings { TargetPath = Environment.ProcessPath, Key = 0x57, IntervalMs = 150 });
        var migratedMonitor = SettingsStore.Load().Settings;
        if (migratedMonitor.TargetPath is not null || migratedMonitor.Key != 0x57 || migratedMonitor.IntervalMs != 150)
            throw new Exception("The retired input monitor must migrate to Any active window without losing preferences.");
        SettingsStore.Save(new AppSettings { Theme = "Dark", TargetPath = @"C:\QA\Unavailable.exe", Unlimited = true });
        var (current, currentWarning) = SettingsStore.Load();
        if (currentWarning is not null || current.Version != 2 || current.Theme != "Dark" || !current.Unlimited)
            throw new Exception("Current settings did not round-trip.");
        Console.WriteLine("PASS: legacy settings migrate to English without losing configuration.");
    }

    private static nint HitTest(MainWindow main, FrameworkElement element, double? offsetX = null)
    {
        var point = element.PointToScreen(new Point(offsetX ?? element.ActualWidth / 2, element.ActualHeight / 2));
        int packed = (ushort)(short)(int)point.X | ((ushort)(short)(int)point.Y << 16);
        return SendMessage(new WindowInteropHelper(main).Handle, 0x0084, 0, (nint)packed);
    }
    private static async Task Until(Func<bool> condition, int seconds = 8)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(seconds)) throw new Exception("La comprobación agotó el tiempo.");
            await Task.Delay(25);
        }
    }

    private static void Capture(MainWindow window, string filename)
    {
        window.UpdateLayout();
        var bounds = VisualTreeHelper.GetDescendantBounds(window);
        var visual = new RenderTargetBitmap((int)Math.Ceiling(bounds.Right), (int)Math.Ceiling(bounds.Bottom), 96, 96, PixelFormats.Pbgra32);
        visual.Render(window);
        Directory.CreateDirectory(QaDirectory);
        using var file = File.Create(Path.Combine(QaDirectory, filename));
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(visual)); png.Save(file);
    }

    private static void CaptureNative(MainWindow window, string filename)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(handle, out var bounds)) throw new Exception("Cannot read native window bounds.");
        using var bitmap = new System.Drawing.Bitmap(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        nint destination = graphics.GetHdc();
        try
        {
            if (!PrintWindow(handle, destination, 2)) throw new Exception("Cannot capture the native window.");
        }
        finally { graphics.ReleaseHdc(destination); }
        bitmap.Save(Path.Combine(QaDirectory, filename), System.Drawing.Imaging.ImageFormat.Png);
    }

    private static async Task CheckAsync(MainWindow main)
    {
        if (main.Icon is null || main.Icon.Width <= 0)
            throw new Exception("The branded window icon did not load.");
        var trayIcon = (System.Drawing.Icon?)typeof(MainWindow).GetField("trayIcon",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(main);
        if (trayIcon is null || trayIcon.Handle == IntPtr.Zero)
            throw new Exception("The branded tray icon did not load.");
        Console.WriteLine("PASS: branded window and tray icons.");
        // Inspect the actual native large icon supplied to the taskbar, not only WPF's source.
        nint largeIconHandle = SendMessage(new WindowInteropHelper(main).Handle, 0x007F, 1, 0);
        if (largeIconHandle == 0) throw new Exception("The native taskbar icon is missing.");
        using (var nativeIcon = System.Drawing.Icon.FromHandle(largeIconHandle))
        using (var pixels = nativeIcon.ToBitmap())
        {
            int left = pixels.Width, right = -1;
            for (int pixelY = 0; pixelY < pixels.Height; pixelY++)
                for (int pixelX = 0; pixelX < pixels.Width; pixelX++)
                    if (pixels.GetPixel(pixelX, pixelY).A > 32)
                    {
                        left = Math.Min(left, pixelX);
                        right = Math.Max(right, pixelX);
                    }
            int visibleWidth = right - left + 1;
            if (visibleWidth < pixels.Width * 0.8)
                throw new Exception($"Taskbar artwork is undersized: {visibleWidth}px inside {pixels.Width}px.");
            Console.WriteLine($"PASS: native taskbar artwork occupies {visibleWidth}/{pixels.Width}px.");
        }
        var priorSettings = SettingsStore.Load().Settings;
        SettingsStore.Save(priorSettings with { Hotkey = new Hotkey(0x76, 0) });
        var unregisteredWindow = new MainWindow(); // No Show: no native hotkeys registered.
        typeof(MainWindow).GetMethod("SaveSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(unregisteredWindow, null);
        if (SettingsStore.Load().Settings.Hotkey.Key != 0x76)
            throw new Exception("A failed/unavailable startup registration must preserve the saved F7 preference.");
        unregisteredWindow.Close();
        SettingsStore.Save(priorSettings);
        main.UpdateLayout();
        nint nativeHandle = new WindowInteropHelper(main).Handle;
        int style = GetWindowLong(nativeHandle, -16);
        if ((style & 0x00C80000) != 0x00C80000 || (style & 0x00020000) == 0
            || main.WindowStyle != WindowStyle.SingleBorderWindow
            || System.Windows.Shell.WindowChrome.GetWindowChrome(main) is not null)
            throw new Exception("A standard Windows caption, system menu, and minimize control are required.");
        if (!GetWindowRect(nativeHandle, out var bounds)) throw new Exception("Cannot read window bounds.");
        int x = (bounds.Left + bounds.Right) / 2;
        int y = bounds.Top + (int)(15 * VisualTreeHelper.GetDpi(main).DpiScaleY);
        int point = (ushort)(short)x | ((ushort)(short)y << 16);
        if (SendMessage(nativeHandle, 0x0084, 0, (nint)point) != 2)
            throw new Exception("The native title bar must support dragging.");
        if (HitTest(main, Find<Button>(main, "SettingsButton")) != 1)
            throw new Exception("The settings button must remain interactive.");
        var targets = Find<ComboBox>(main, "TargetPicker");
        if (targets.Items[0] is not WindowTarget { Name: "Any active window", Path: "" })
            throw new Exception("Unrestricted active-window mode is missing.");
        if (targets.SelectedItem is not WindowTarget { Path: @"C:\QA\Unavailable.exe" })
            throw new Exception("A saved unavailable target must not silently become unrestricted.");
        if (targets.Items.Cast<WindowTarget>().Any(x => x.Name == "Input Monitor" || x.Path == Environment.ProcessPath)
            || main.FindName("TestButton") is not null)
            throw new Exception("The retired monitor must not appear in the application.");
        targets.SelectedItem = targets.Items.Cast<WindowTarget>().Single(x => x.Path == @"C:\QA\Unavailable.exe");
        if (targets.SelectedIndex == 0)
            throw new Exception("Specific application selection is missing.");
        targets.SelectedIndex = 0;
        Console.WriteLine("PASS: native caption dragging, interactive settings, and optional application selection.");
        var unlimited = Find<CheckBox>(main, "UnlimitedCheckBox");
        var limitControls = Find<FrameworkElement>(main, "LimitControls");
        if (unlimited.IsChecked != true || limitControls.IsEnabled
            || !Find<TextBlock>(main, "TargetSummaryText").Text.Contains("No limit"))
            throw new Exception("Saved Unlimited mode was not restored.");
        unlimited.IsChecked = false;
        if (!limitControls.IsEnabled || SettingsStore.Load().Settings.Unlimited)
            throw new Exception("Disabling Unlimited must restore bounded mode.");
        var toggle = Find<Button>(main, "ToggleButton");
        CheckKeyCapture(main);
        Find<TextBox>(main, "IntervalInput").Text = "100";
        Find<ComboBox>(main, "LimitPicker").SelectedIndex = 1;
        Find<TextBox>(main, "LimitInput").Text = "5";
        Find<ComboBox>(main, "HotkeyPicker").SelectedIndex = 7;
        Find<ComboBox>(main, "ModifiersPicker").SelectedIndex = 0;
        Click(Find<Button>(main, "ApplyHotkeyButton"));
        var theme = Find<Button>(main, "ThemeButton");
        SelectTheme(theme, "Light");
        await Task.Delay(100);
        Capture(main, "autopress-light.png");
        CaptureNative(main, "autopress-native-light.png");
        SelectTheme(theme, "Dark");
        await Task.Delay(100);
        Capture(main, "autopress-dark.png");
        CaptureNative(main, "autopress-native-dark.png");
        int backdropResult = DwmGetWindowAttribute(new WindowInteropHelper(main).Handle, 38, out int backdrop, sizeof(int));
        Console.WriteLine($"Panel={main.ActualWidth:0}x{main.ActualHeight:0}; DWM backdrop={backdrop}; HRESULT=0x{backdropResult:X8}; highContrast={SystemParameters.HighContrast}");
        if (main.ActualWidth > 580 || main.ActualHeight > 360) throw new Exception($"El panel no es compacto: {main.ActualWidth} x {main.ActualHeight}.");
        Click(Find<Button>(main, "SettingsButton"));
        main.UpdateLayout();
        if (Find<FrameworkElement>(main, "AdvancedPanel").Visibility != Visibility.Visible) throw new Exception("No se abrieron los ajustes.");
        Capture(main, "autopress-settings.png");
        await Task.Delay(100);
        double previousMaximum = main.MaxHeight;
        main.MaxHeight = 480;
        main.UpdateLayout();
        var contentScroll = Find<ScrollViewer>(main, "ContentScroll");
        if (main.ActualHeight > 480.5 || contentScroll.ScrollableHeight <= 0)
            throw new Exception("Expanded settings must scroll within a small work area.");
        contentScroll.ScrollToEnd();
        main.UpdateLayout();
        Capture(main, "autopress-small-screen.png");
        contentScroll.ScrollToHome();
        main.MaxHeight = previousMaximum;
        main.UpdateLayout();
        unlimited.ApplyTemplate();
        var thumb = (FrameworkElement)unlimited.Template.FindName("Thumb", unlimited);
        if (thumb.HorizontalAlignment != HorizontalAlignment.Left)
            throw new Exception("The disabled limit switch must show its off position.");
        var peer = new System.Windows.Automation.Peers.CheckBoxAutomationPeer(unlimited);
        var switchProvider = (System.Windows.Automation.Provider.IToggleProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle);
        switchProvider.Toggle();
        if (thumb.HorizontalAlignment != HorizontalAlignment.Right
            || !Find<TextBlock>(main, "LimitHint").Text.StartsWith("Runs until"))
            throw new Exception("The enabled limit switch must show its on position and explanation.");
        if (limitControls.IsEnabled || Find<TextBox>(main, "LimitInput").Text != "5"
            || !SettingsStore.Load().Settings.Unlimited)
            throw new Exception("Unlimited must preserve the previous limit and persist its state.");
        Capture(main, "autopress-unlimited.png");
        SelectTheme(theme, "Light");
        Capture(main, "autopress-unlimited-light.png");
        SelectTheme(theme, "Dark");
        switchProvider.Toggle();
        if (!limitControls.IsEnabled || Find<TextBox>(main, "LimitInput").Text != "5")
            throw new Exception("Disabling Unlimited lost the previous limit.");
        Click(Find<Button>(main, "SettingsButton"));
        main.UpdateLayout();
        if (Environment.GetCommandLineArgs().Contains("--render-only"))
        {
            SendMessage(nativeHandle, 0x0112, 0xF020, 0); // Native SC_MINIMIZE, no keyboard input.
            await Until(() => main.WindowState == WindowState.Minimized && !main.IsVisible);
            main.Show();
            main.WindowState = WindowState.Normal;
            await Until(() => main.IsVisible && main.WindowState == WindowState.Normal);
            nint handle = new WindowInteropHelper(main).Handle;
            if (!RegisterHotKey(handle, 9300, 0x4000, 0x78)) throw new Exception("F9 está ocupado: no se puede simular la colisión.");
            try
            {
                Find<ComboBox>(main, "HotkeyPicker").SelectedIndex = 8;
                Click(Find<Button>(main, "ApplyHotkeyButton"));
                if (!Find<TextBlock>(main, "FeedbackText").Text.Contains("already in use")) throw new Exception("No se informó la colisión de hotkeys.");
                if (!toggle.Content.ToString()!.Contains("F8")) throw new Exception("Se perdió la hotkey anterior tras una colisión.");
                if (Find<ComboBox>(main, "HotkeyPicker").SelectedIndex != 7)
                    throw new Exception("A rejected replacement shortcut must restore the working F8 selection.");
                Click(toggle);
                if (!toggle.Content.ToString()!.StartsWith("Stop"))
                    throw new Exception("The old shortcut must remain usable after a replacement conflict.");
                Click(toggle); // Cancel before keyboard input.
                await Until(() => toggle.Content?.ToString()?.StartsWith("Start") == true);
            }
            finally { UnregisterHotKey(handle, 9300); }
            Find<ComboBox>(main, "HotkeyPicker").SelectedIndex = 7;
            Click(Find<Button>(main, "ApplyHotkeyButton"));
            Find<TextBox>(main, "IntervalInput").Text = "0";
            Click(toggle);
            if (!Find<TextBlock>(main, "FeedbackText").Text.Contains("50")) throw new Exception("No se rechazó el intervalo inválido.");
            Find<TextBox>(main, "IntervalInput").Text = "100";
            if (Find<TextBlock>(main, "FeedbackText").Text.Contains("50"))
                throw new Exception("Correcting a setting must clear stale validation feedback.");
            Click(toggle);
            if (!toggle.Content.ToString()!.StartsWith("Stop")) throw new Exception("No comenzó la cuenta atrás.");
            // Cancel synchronously via the UI before the three-second countdown ends.
            Click(toggle);
            await Until(() => toggle.Content?.ToString()?.StartsWith("Start") == true);
            if (Find<TextBlock>(main, "CountText").Text != "0") throw new Exception("Se emitió input durante la cuenta atrás.");
            Click(toggle);
            PostMessage(handle, GlobalHotkeys.MessageId, GlobalHotkeys.EmergencyId, 0);
            await Until(() => toggle.Content?.ToString()?.StartsWith("Start") == true);
            if (Find<TextBlock>(main, "FeedbackText").Text != "Emergency stop") throw new Exception("No se ejecutó la parada de emergencia.");
            // Disabled limit values must not block an unlimited session.
            Find<TextBox>(main, "LimitInput").Text = "not a number";
            unlimited.IsChecked = true;
            Click(toggle);
            if (!toggle.Content.ToString()!.StartsWith("Stop")
                || Find<ProgressBar>(main, "SessionProgress").Visibility != Visibility.Hidden)
                throw new Exception("Unlimited must start without a numeric limit or finite progress indicator.");
            Click(toggle); // Cancel before the countdown can emit input.
            await Until(() => toggle.Content?.ToString()?.StartsWith("Start") == true);
            if (Find<TextBlock>(main, "CountText").Text != "0")
                throw new Exception("Unlimited countdown cancellation emitted input.");
            Find<TextBox>(main, "LimitInput").Text = "5";
            unlimited.IsChecked = false;
            Console.WriteLine("PASS: Unlimited persistence, preserved limits, disabled fields, and no-input cancellation.");
            var nativeInput = typeof(WindowsKeyboardOutput).Assembly.GetType("AutoPress.Windows.NativeMethods+Input")!;
            if (Marshal.SizeOf(nativeInput) != (IntPtr.Size == 8 ? 40 : 28)) throw new Exception("Tamaño incorrecto de INPUT para Win32.");
            Console.WriteLine("PASS: conflicto de hotkey conserva F8; emergencia; layout Win32 INPUT.");
            await CheckRecoveryAsync(main);
            return;
        }
        // This receiver is a test fixture, not a feature shipped with AutoPress.
        var target = new InputTestWindow { Owner = main };
        target.Show();
        nint targetHandle = new WindowInteropHelper(target).Handle;
        bool foregroundResult = SetForegroundWindow(targetHandle);
        Console.WriteLine($"Focus requested={foregroundResult}, target={targetHandle}, foreground={GetForegroundWindow()}, foregroundPath={WindowTargets.ForegroundPath()}, session={Process.GetCurrentProcess().SessionId}");
        await Until(() => GetForegroundWindow() == targetHandle);
        int downs = 0, ups = 0;
        target.PreviewKeyDown += (_, e) => { if (e.Key == Key.F) downs++; };
        target.PreviewKeyUp += (_, e) => { if (e.Key == Key.F) ups++; };
        // Handled-events-too is needed because the fixture consumes key-down events.
        target.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => { if (e.Key == Key.F) ReceivedDowns++; }), true);
        target.AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler((_, e) => { if (e.Key == Key.F) ReceivedUps++; }), true);
        var sender = new WindowsKeyboardOutput();
        if (!await sender.PressAsync(0x77, CancellationToken.None)) throw new Exception("La hotkey no se pudo enviar: hay teclas físicas presionadas.");
        await Until(() => ReceivedDowns >= 5);
        await Until(() => toggle.Content?.ToString()?.StartsWith("Start") == true);
        if (ReceivedDowns != 5 || ReceivedUps != 5) throw new Exception($"Se esperaban 5 pares completos, recibidos {ReceivedDowns}/{ReceivedUps}.");
        Console.WriteLine($"PASS: SendInput {ReceivedDowns} keydowns / {ReceivedUps} keyups; límite exacto.");

        // A second session changes focus to another owned test window, never to an external app.
        Find<TextBox>(main, "LimitInput").Text = "100";
        SetForegroundWindow(targetHandle);
        await sender.PressAsync(0x77, CancellationToken.None);
        await Until(() => ReceivedDowns >= 6);
        Capture(main, "autopress-active.png");
        var second = new Window { Title = "AutoPress · Test de cambio de foco", Width = 280, Height = 160, Owner = main, Content = new TextBlock { Text = "Prueba de foco en curso", Margin = new Thickness(20) } };
        int otherDowns = 0;
        second.PreviewKeyDown += (_, e) => { if (e.Key == Key.F) otherDowns++; e.Handled = true; };
        second.Show();
        SetForegroundWindow(new WindowInteropHelper(second).Handle);
        await Until(() => otherDowns >= 2);
        await sender.PressAsync(0x77, CancellationToken.None);
        await Until(() => toggle.Content?.ToString()?.StartsWith("Start") == true);
        int stopped = otherDowns;
        await Task.Delay(250);
        if (otherDowns != stopped) throw new Exception("Siguió enviando teclas después de detener.");
        second.Close();
        Console.WriteLine("PASS: cambiar de foco mantiene la sesión; segunda pulsación F8 detiene.");

        SetForegroundWindow(targetHandle);
        await sender.PressAsync(0x77, CancellationToken.None);
        await Until(() => toggle.Content?.ToString()?.StartsWith("Stop") == true);
        PostMessage(new WindowInteropHelper(main).Handle, GlobalHotkeys.MessageId, GlobalHotkeys.EmergencyId, 0);
        await Until(() => toggle.Content?.ToString()?.StartsWith("Start") == true);
        Console.WriteLine("PASS: mensaje de emergencia cancela la cuenta atrás.");
        main.WindowState = WindowState.Minimized;
        await Task.Delay(100);
        SetForegroundWindow(targetHandle);
        await sender.PressAsync(0x77, CancellationToken.None);
        await Until(() => toggle.Content?.ToString()?.StartsWith("Stop") == true);
        await sender.PressAsync(0x77, CancellationToken.None);
        await Until(() => toggle.Content?.ToString()?.StartsWith("Start") == true);
        main.Show(); main.WindowState = WindowState.Normal;
        target.Close();
        Console.WriteLine("PASS: hotkey disponible con la ventana oculta en bandeja.");
        // Leave usable defaults instead of the test limits.
        Find<ComboBox>(main, "LimitPicker").SelectedIndex = 0;
        Find<TextBox>(main, "LimitInput").Text = "300";
    }
    private static int ReceivedDowns, ReceivedUps;
    private sealed class RecoveryOutput : AutoPress.Core.IKeyboardOutput
    {
        public bool AllowRelease;
        public ValueTask<bool> PressAsync(int key, CancellationToken token) => ValueTask.FromResult(true);
        public void ReleasePending() { if (!AllowRelease) throw new InvalidOperationException("Simulated rejected key-up"); }
    }

    private static async Task CheckRecoveryAsync(MainWindow main)
    {
        var output = new RecoveryOutput();
        var pendingSession = new AutoPress.Core.AutomationSession(output);
        pendingSession.Start(new(0x57, 100, null, 1, TimeSpan.Zero));
        await Until(() => !pendingSession.Snapshot.IsActive);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = typeof(MainWindow).GetField("session", flags)!;
        var render = typeof(MainWindow).GetMethod("RenderSnapshot", flags)!;
        field.SetValue(main, pendingSession);
        render.Invoke(main, null);
        if (Find<FrameworkElement>(main, "Configuration").IsEnabled
            || !Find<Button>(main, "ToggleButton").Content.ToString()!.StartsWith("Release key"))
            throw new Exception("Pending release must disable configuration and offer recovery.");
        Find<TextBox>(main, "IntervalInput").Text = "120";
        if (!ReferenceEquals(field.GetValue(main), pendingSession))
            throw new Exception("Configuration changes must not discard pending recovery.");
        main.Close();
        await Task.Delay(50);
        if (!main.IsVisible) throw new Exception("Closing must not abandon a pending key release.");
        output.AllowRelease = true;
        SendMessage(new WindowInteropHelper(main).Handle, GlobalHotkeys.MessageId, GlobalHotkeys.EmergencyId, 0);
        await Until(() => !pendingSession.NeedsKeyRelease);
        if (pendingSession.Snapshot.IsActive) throw new Exception("Recovery must not start another session.");
        field.SetValue(main, null);
        render.Invoke(main, null);
        Find<TextBox>(main, "IntervalInput").Text = "100";
        Console.WriteLine("PASS: pending release survives edits and close, emergency retry recovers without restarting.");
    }
}
