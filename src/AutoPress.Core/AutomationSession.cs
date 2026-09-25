namespace AutoPress.Core;

/// <summary>One serial producer. StopAsync returns only after emission and cleanup finish.</summary>
public sealed class AutomationSession(IKeyboardOutput output, Action? validateStart = null)
{
    private readonly object sync = new();
    private CancellationTokenSource? cancellation;
    private Task? runningTask;
    private SessionSnapshot snapshot = SessionSnapshot.Initial;
    private string stopReason = "Stopped with the toggle shortcut";
    private long activeTimestamp;
    private bool needsKeyRelease;
    public bool NeedsKeyRelease { get { lock (sync) return needsKeyRelease; } }

    public SessionSnapshot Snapshot
    {
        get
        {
            lock (sync) return snapshot.Status == SessionStatus.Running
                ? snapshot with { Elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(activeTimestamp) } : snapshot;
        }
    }

    public bool Start(SessionRequest request)
    {
        request.Validate();
        lock (sync)
        {
            if (runningTask is { IsCompleted: false }) return false;
            if (needsKeyRelease) throw new InvalidOperationException("Release the pending key before starting another session.");
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            stopReason = "Stopped with the toggle shortcut";
            snapshot = new(SessionStatus.Countdown, 0, TimeSpan.Zero, request.Countdown, "Preparing session");
            runningTask = Task.Run(() => RunAsync(request, cancellation.Token));
            return true;
        }
    }

    public async Task StopAsync(string reason = "Stopped with the toggle shortcut")
    {
        Task? task;
        lock (sync)
        {
            stopReason = reason;
            cancellation?.Cancel();
            task = runningTask;
        }
        if (task is not null) await task.ConfigureAwait(false);
        lock (sync)
        {
            if (task != runningTask || !needsKeyRelease) return;
            try
            {
                output.ReleasePending();
                needsKeyRelease = false;
                snapshot = snapshot with { Status = SessionStatus.Idle, Message = reason };
            }
            catch (Exception error)
            {
                snapshot = snapshot with { Status = SessionStatus.Faulted,
                    Message = $"Key release is still pending. Focus a non-elevated window and stop again. {error.Message}" };
            }
        }
    }

    private void SetSnapshot(SessionSnapshot value) { lock (sync) snapshot = value; }

    private async Task RunAsync(SessionRequest request, CancellationToken token)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        long presses = 0;
        var elapsed = TimeSpan.Zero;
        string message = "Session limit reached";
        bool failed = false;
        bool started = false;
        CancellationTokenSource? activeCancellation = null;
        try
        {
            while (clock.Elapsed < request.Countdown)
            {
                SetSnapshot(new(SessionStatus.Countdown, 0, TimeSpan.Zero, request.Countdown - clock.Elapsed, "Focus the window that should receive input"));
                await Task.Delay(TimeSpan.FromMilliseconds(50), token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            validateStart?.Invoke();
            clock.Restart();
            lock (sync) activeTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            started = true;
            activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (request.Duration is { } maximumDuration) activeCancellation.CancelAfter(maximumDuration);
            var activeToken = activeCancellation.Token;
            var nextPress = TimeSpan.Zero;
            while (true)
            {
                activeToken.ThrowIfCancellationRequested();
                elapsed = clock.Elapsed;
                if (request.Duration is { } duration && elapsed >= duration) break;
                if (request.MaxPresses is { } max && presses >= max) break;
                if (elapsed < nextPress)
                {
                    var wakeAt = request.Duration is { } deadline && deadline < nextPress ? deadline : nextPress;
                    // Task.Delay may wake early. Re-check both deadlines before emitting another key.
                    var waitMs = Math.Max(1, Math.Ceiling((wakeAt - elapsed).TotalMilliseconds));
                    await Task.Delay(TimeSpan.FromMilliseconds(waitMs), activeToken).ConfigureAwait(false);
                    continue;
                }
                var pressedAt = clock.Elapsed;
                if (await output.PressAsync(request.Key, activeToken).ConfigureAwait(false)) presses++;
                elapsed = clock.Elapsed;
                SetSnapshot(new(SessionStatus.Running, presses, elapsed, TimeSpan.Zero, "Use the toggle shortcut to stop"));
                if (request.MaxPresses is { } limit && presses >= limit) break;
                nextPress = pressedAt + TimeSpan.FromMilliseconds(request.IntervalMs);
            }
            elapsed = clock.Elapsed;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || activeCancellation?.IsCancellationRequested == true)
        {
            if (started) elapsed = clock.Elapsed;
            if (token.IsCancellationRequested) { lock (sync) message = stopReason; }
        }
        catch (Exception error)
        {
            failed = true;
            message = error.Message;
        }
        finally
        {
            activeCancellation?.Dispose();
            bool releaseFailed = false;
            try { output.ReleasePending(); }
            catch (Exception error)
            {
                failed = releaseFailed = true;
                message = $"Key release is pending. Focus a non-elevated window and stop again. {error.Message}";
            }
            lock (sync)
            {
                needsKeyRelease = releaseFailed;
                snapshot = new(failed ? SessionStatus.Faulted : SessionStatus.Idle, presses, elapsed, TimeSpan.Zero, message);
            }
        }
    }
}
