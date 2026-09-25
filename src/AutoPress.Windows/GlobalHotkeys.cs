using System.Runtime.InteropServices;

namespace AutoPress.Windows;

public readonly record struct Hotkey(uint Key, uint Modifiers)
{
    public const uint Alt = 1, Control = 2, Shift = 4;
    public static Hotkey Default => new(0x77, 0); // F8
    public static Hotkey Emergency => new(0x79, Control | Alt); // Ctrl+Alt+F10 (F12 reserved by Windows)
    public bool IsValid => Key is >= 0x70 and <= 0x7A && (Modifiers & ~7u) == 0;
    public override string ToString() => $"{((Modifiers & Control) != 0 ? "Ctrl + " : "")}{((Modifiers & Alt) != 0 ? "Alt + " : "")}{((Modifiers & Shift) != 0 ? "Shift + " : "")}F{Key - 0x70 + 1}";
}

public sealed class GlobalHotkeys(nint window) : IDisposable
{
    public const int MessageId = 0x0312, EmergencyId = 9100;
    private int activeId = 9101;
    private bool registered, emergencyRegistered;
    public Hotkey Current { get; private set; }
    public bool IsToggleId(int id) => registered && id == activeId;

    public void Register(Hotkey hotkey)
    {
        if (!hotkey.IsValid || hotkey == Hotkey.Emergency) throw new ArgumentException("Choose a shortcut different from the emergency stop shortcut.");
        if (registered && hotkey == Current) return;
        if (!emergencyRegistered)
        {
            if (!NativeMethods.RegisterHotKey(window, EmergencyId, Hotkey.Emergency.Modifiers | 0x4000, Hotkey.Emergency.Key))
                throw new InvalidOperationException("Ctrl + Alt + F10 is already in use. Release that shortcut in the other application before starting.");
            emergencyRegistered = true;
        }
        int nextId = activeId == 9101 ? 9102 : 9101;
        if (!NativeMethods.RegisterHotKey(window, nextId, hotkey.Modifiers | 0x4000, hotkey.Key))
            throw new InvalidOperationException($"{hotkey} is already in use. Choose another shortcut. Windows error: {Marshal.GetLastWin32Error()}.");
        if (registered) NativeMethods.UnregisterHotKey(window, activeId);
        activeId = nextId;
        Current = hotkey;
        registered = true;
    }

    public void Dispose()
    {
        if (registered) NativeMethods.UnregisterHotKey(window, activeId);
        if (emergencyRegistered) NativeMethods.UnregisterHotKey(window, EmergencyId);
        registered = emergencyRegistered = false;
    }
}
