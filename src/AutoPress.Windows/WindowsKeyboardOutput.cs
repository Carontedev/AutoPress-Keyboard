using System.Runtime.InteropServices;
using AutoPress.Core;

namespace AutoPress.Windows;

public sealed class WindowsKeyboardOutput : IKeyboardOutput
{
    public const int HoldMilliseconds = 40;
    private NativeMethods.Input? pending;
    private readonly Func<NativeMethods.Input, uint> send;
    private readonly Func<int, bool> isDown;
    private readonly Func<CancellationToken, Task> hold;

    public WindowsKeyboardOutput() : this(
        input => NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.Input>()),
        key => (NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0,
        cancellation => Task.Delay(HoldMilliseconds, cancellation)) { }

    // Internal seam exercises the real down/hold/up flow without emitting desktop input.
    internal WindowsKeyboardOutput(Func<NativeMethods.Input, uint> send, Func<int, bool> isDown, Func<CancellationToken, Task> hold)
    {
        this.send = send;
        this.isDown = isDown;
        this.hold = hold;
    }

    public async ValueTask<bool> PressAsync(int virtualKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (isDown(virtualKey) || isDown(0x10) || isDown(0x11) || isDown(0x12) || isDown(0x5B) || isDown(0x5C)) return false;
        var down = CreateDownInput(virtualKey);
        if (send(down) != 1)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Windows could not send the key. Check the destination's permissions. Windows error: {error}.");
        }
        pending = down;
        try { await hold(cancellationToken).ConfigureAwait(false); }
        finally { ReleasePending(); }
        return true;
    }

    public void ReleasePending()
    {
        if (pending is not { } up) return;
        up.Data.Keyboard.Flags |= 2; // KEYEVENTF_KEYUP; retain the original scan code/layout.
        if (send(up) != 1) throw new InvalidOperationException("Windows rejected the key release.");
        pending = null;
    }

    internal static NativeMethods.Input CreateDownInput(int virtualKey)
    {
        uint thread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        uint scan = NativeMethods.MapVirtualKeyEx((uint)virtualKey, 4, NativeMethods.GetKeyboardLayout(thread));
        // Some layouts return the keypad scan without E0 for the dedicated navigation keys.
        bool extended = (scan & 0xFF00) == 0xE000 || virtualKey is >= 0x21 and <= 0x28;
        return new NativeMethods.Input
        {
            Type = 1,
            Data = new() { Keyboard = new()
            {
                VirtualKey = scan == 0 ? (ushort)virtualKey : (ushort)0,
                ScanCode = (ushort)(scan & 0xFF),
                Flags = scan == 0 ? 0u : 8u | (extended ? 1u : 0u) // KEYEVENTF_SCANCODE
            } }
        };
    }
}
