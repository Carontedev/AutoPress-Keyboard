using AutoPress.Core;
using Xunit;

namespace AutoPress.Core.Tests;

public sealed class HeldInputTests
{
    private sealed class HeldOutput : IKeyboardOutput
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsDown { get; private set; }
        public async ValueTask<bool> PressAsync(int key, CancellationToken cancellationToken)
        {
            IsDown = true;
            Started.TrySetResult();
            try { await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken); return true; }
            finally { IsDown = false; }
        }
        public void ReleasePending() => IsDown = false;
    }

    [Fact]
    public async Task StopCancelsAnInFlightHoldAndReleasesBeforeReturning()
    {
        var output = new HeldOutput();
        var session = new AutomationSession(output);
        session.Start(new(0x57, 100, null, 100, TimeSpan.Zero));
        await output.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(output.IsDown);
        await session.StopAsync().WaitAsync(TimeSpan.FromMilliseconds(500));
        Assert.False(output.IsDown);
        Assert.Equal(SessionStatus.Idle, session.Snapshot.Status);
    }

    [Fact]
    public async Task DurationLimitReleasesAnInFlightHoldWithoutFaulting()
    {
        var output = new HeldOutput();
        var session = new AutomationSession(output);
        session.Start(new(0x57, 100, TimeSpan.FromSeconds(1), null, TimeSpan.Zero));
        await output.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (session.Snapshot.IsActive) await Task.Delay(10, timeout.Token);
        Assert.False(output.IsDown);
        Assert.Equal(SessionStatus.Idle, session.Snapshot.Status);
        Assert.Equal("Session limit reached", session.Snapshot.Message);
    }
}
