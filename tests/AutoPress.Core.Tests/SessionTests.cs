using System.Diagnostics;
using AutoPress.Core;
using Xunit;

namespace AutoPress.Core.Tests;

public sealed class SessionTests
{
    private sealed class Output : IKeyboardOutput
    {
        public int Presses, Releases;
        public bool Fail, Skip;
        public ValueTask<bool> PressAsync(int key, CancellationToken cancellationToken)
        {
            if (Fail) throw new InvalidOperationException("Envío rechazado");
            if (Skip) return ValueTask.FromResult(false);
            Interlocked.Increment(ref Presses);
            return ValueTask.FromResult(true);
        }
        public void ReleasePending() => Interlocked.Increment(ref Releases);
    }

    private static SessionRequest Request(long? count = 3, TimeSpan? duration = null, int interval = 50, TimeSpan? countdown = null)
        => new(0x46, interval, duration, count, countdown ?? TimeSpan.Zero);

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }

    [Fact]
    public async Task CountLimitEmitsExactlyRequestedNumberAndCleansUp()
    {
        var output = new Output();
        var session = new AutomationSession(output);
        Assert.True(session.Start(Request()));
        await Until(() => !session.Snapshot.IsActive);
        Assert.Equal(3, output.Presses);
        Assert.Equal(3, session.Snapshot.Presses);
        Assert.Equal("Session limit reached", session.Snapshot.Message);
        Assert.Equal(1, output.Releases);
    }

    [Fact]
    public async Task StopInterruptsLongIntervalAndPreventsFurtherInput()
    {
        var output = new Output();
        var session = new AutomationSession(output);
        session.Start(Request(count: 100, interval: 60_000));
        await Until(() => output.Presses == 1);
        var watch = Stopwatch.StartNew();
        await session.StopAsync();
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(250));
        await Task.Delay(100);
        Assert.Equal(1, output.Presses);
        Assert.Equal(SessionStatus.Idle, session.Snapshot.Status);
        await session.StopAsync();
        Assert.Equal(1, output.Releases);
    }

    [Fact]
    public async Task CancelDuringCountdownDoesNotEmit()
    {
        var output = new Output();
        var session = new AutomationSession(output);
        session.Start(Request(countdown: TimeSpan.FromSeconds(3)));
        await session.StopAsync("Emergencia");
        Assert.Equal(0, output.Presses);
        Assert.Equal("Emergencia", session.Snapshot.Message);
    }

    [Fact]
    public async Task DuplicateStartCannotCreateSecondProducer()
    {
        var output = new Output();
        var session = new AutomationSession(output);
        session.Start(Request(countdown: TimeSpan.FromSeconds(3)));
        Assert.False(session.Start(Request()));
        await session.StopAsync();
        Assert.Equal(0, output.Presses);
    }

    [Fact]
    public async Task SenderFailureStopsSessionAndReleasesPendingKey()
    {
        var output = new Output { Fail = true };
        var session = new AutomationSession(output);
        session.Start(Request());
        await Until(() => !session.Snapshot.IsActive);
        Assert.Equal(SessionStatus.Faulted, session.Snapshot.Status);
        Assert.Equal("Envío rechazado", session.Snapshot.Message);
        Assert.Equal(1, output.Releases);
    }

    [Fact]
    public async Task WrongInitialTargetProducesNoInput()
    {
        var output = new Output();
        var session = new AutomationSession(output, () => throw new InvalidOperationException("Destino incorrecto"));
        session.Start(Request());
        await Until(() => !session.Snapshot.IsActive);
        Assert.Equal(0, output.Presses);
        Assert.Equal(SessionStatus.Faulted, session.Snapshot.Status);
    }

    [Fact]
    public async Task TargetIsCheckedOnceNotOnEveryPress()
    {
        var output = new Output();
        int checks = 0;
        var session = new AutomationSession(output, () => checks++);
        session.Start(Request());
        await Until(() => !session.Snapshot.IsActive);
        Assert.Equal(1, checks);
        Assert.Equal(3, output.Presses);
    }

    [Fact]
    public async Task DurationExpiresEvenWhenAllPressesAreSkipped()
    {
        var output = new Output { Skip = true };
        var session = new AutomationSession(output);
        session.Start(Request(count: null, duration: TimeSpan.FromSeconds(1)));
        await Until(() => !session.Snapshot.IsActive);
        Assert.Equal(0, output.Presses);
        Assert.True(session.Snapshot.Elapsed >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DurationIsNotExtendedByLongInterval()
    {
        var output = new Output();
        var session = new AutomationSession(output);
        var watch = Stopwatch.StartNew();
        session.Start(Request(count: null, duration: TimeSpan.FromSeconds(1), interval: 60_000));
        await Until(() => !session.Snapshot.IsActive);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3));
        Assert.Equal(1, output.Presses);
    }

    [Fact]
    public async Task RestartResetsCounterAndAllowsNewSession()
    {
        var output = new Output();
        var session = new AutomationSession(output);
        session.Start(Request(1));
        await Until(() => !session.Snapshot.IsActive);
        // StopAsync joins the worker as well as observing its final state.
        await session.StopAsync();
        Assert.True(session.Start(Request(2)));
        await Until(() => !session.Snapshot.IsActive);
        Assert.Equal(2, session.Snapshot.Presses);
        Assert.Equal(3, output.Presses);
    }

    [Theory]
    [InlineData(0x11, 100, 1)]
    [InlineData(0x46, 49, 1)]
    [InlineData(0x46, 60_001, 1)]
    [InlineData(0x46, 100, 0)]
    [InlineData(0x46, 100, 1_000_001)]
    public void InvalidRequestsAreRejected(int key, int interval, int count)
        => Assert.Throws<ArgumentException>(() => new SessionRequest(key, interval, null, count, TimeSpan.Zero).Validate());

    [Fact]
    public void MissingLimitsRequireExplicitUnlimitedOptIn() => Assert.Throws<ArgumentException>(() => Request(null).Validate());

    [Fact]
    public async Task UnlimitedSessionRunsUntilStoppedAndReleasesInput()
    {
        var output = new Output();
        var session = new AutomationSession(output);
        session.Start(Request(null) with { Unlimited = true });
        try
        {
            await Until(() => output.Presses >= 5);
            Assert.True(session.Snapshot.IsActive);
        }
        finally { await session.StopAsync("Manual stop"); }
        int presses = output.Presses;
        await Task.Delay(100);
        Assert.Equal(presses, output.Presses);
        Assert.Equal(1, output.Releases);
        Assert.Equal(SessionStatus.Idle, session.Snapshot.Status);
        Assert.Equal("Manual stop", session.Snapshot.Message);
    }

    [Fact]
    public async Task UnlimitedCountdownCanBeCancelledWithoutInput()
    {
        var output = new Output();
        var session = new AutomationSession(output);
        session.Start(Request(null, countdown: TimeSpan.FromSeconds(3)) with { Unlimited = true });
        await session.StopAsync("Emergency stop");
        Assert.Equal(0, output.Presses);
        Assert.Equal(SessionStatus.Idle, session.Snapshot.Status);
        Assert.Equal("Emergency stop", session.Snapshot.Message);
    }

    [Fact]
    public void UnlimitedCannotContainHiddenLimits()
    {
        Assert.Throws<ArgumentException>(() => (Request(3) with { Unlimited = true }).Validate());
        Assert.Throws<ArgumentException>(() => (Request(null, TimeSpan.FromSeconds(1)) with { Unlimited = true }).Validate());
    }

    [Fact]
    public async Task UnlimitedStillStopsOnOutputFailure()
    {
        var output = new Output { Fail = true };
        var session = new AutomationSession(output);
        session.Start(Request(null) with { Unlimited = true });
        await Until(() => !session.Snapshot.IsActive);
        Assert.Equal(SessionStatus.Faulted, session.Snapshot.Status);
        Assert.Equal(1, output.Releases);
    }
}
