namespace AutoPress.Core;

public sealed record SessionRequest(int Key, int IntervalMs, TimeSpan? Duration, long? MaxPresses, TimeSpan Countdown, bool Unlimited = false)
{
    public void Validate()
    {
        if (!IsSupportedKey(Key)) throw new ArgumentException("Select a letter, number, function key, or navigation key.");
        if (IntervalMs is < 50 or > 60_000) throw new ArgumentException("The interval must be between 50 and 60,000 ms.");
        if (!Unlimited && Duration is null && MaxPresses is null) throw new ArgumentException("Set a duration or keystroke limit, or enable Unlimited.");
        if (Unlimited && (Duration is not null || MaxPresses is not null)) throw new ArgumentException("Unlimited sessions cannot include a duration or keystroke limit.");
        if (Duration is { } duration && (duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromHours(24)))
            throw new ArgumentException("The duration must be between 1 second and 24 hours.");
        if (MaxPresses is < 1 or > 1_000_000) throw new ArgumentException("The limit must be between 1 and 1,000,000 keystrokes.");
        if (Countdown < TimeSpan.Zero || Countdown > TimeSpan.FromSeconds(10)) throw new ArgumentException("Invalid start delay.");
    }

    public static bool IsSupportedKey(int key) => key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A
        or >= 0x70 and <= 0x7B or 0x20 or 0x0D or 0x09 or >= 0x21 and <= 0x28;
}

public enum SessionStatus { Idle, Countdown, Running, Faulted }
public sealed record SessionSnapshot(SessionStatus Status, long Presses, TimeSpan Elapsed, TimeSpan RemainingCountdown, string Message)
{
    public bool IsActive => Status is SessionStatus.Countdown or SessionStatus.Running;
    public static SessionSnapshot Initial => new(SessionStatus.Idle, 0, TimeSpan.Zero, TimeSpan.Zero, "Ready");
}

public interface IKeyboardOutput
{
    // False means a physical key/modifier is down: skip this tick without affecting it.
    ValueTask<bool> PressAsync(int virtualKey, CancellationToken cancellationToken);
    void ReleasePending();
}
