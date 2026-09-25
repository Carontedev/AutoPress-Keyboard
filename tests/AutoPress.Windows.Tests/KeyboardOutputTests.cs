using System.Collections.Concurrent;
using AutoPress.Windows;
using Xunit;

namespace AutoPress.Windows.Tests;

public sealed class KeyboardOutputTests
{
    [Fact]
    public async Task EmergencyStopRetriesRejectedKeyReleaseBeforeRestart()
    {
        bool allowRelease = false;
        int downs = 0, ups = 0;
        var output = new WindowsKeyboardOutput(input =>
        {
            if ((input.Data.Keyboard.Flags & 2) == 0) { downs++; return 1; }
            ups++;
            return allowRelease ? 1u : 0u;
        }, _ => false, _ => Task.CompletedTask);
        var session = new AutoPress.Core.AutomationSession(output);
        var request = new AutoPress.Core.SessionRequest(0x57, 100, null, 1, TimeSpan.Zero);
        session.Start(request);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (session.Snapshot.IsActive) await Task.Delay(5, timeout.Token);
        int firstAttempts = ups;
        await session.StopAsync("Emergency stop");
        Assert.True(ups > firstAttempts, "Emergency stop must retry a rejected key-up.");
        Assert.Throws<InvalidOperationException>(() => session.Start(request));
        allowRelease = true;
        await session.StopAsync("Emergency stop");
        Assert.Equal(AutoPress.Core.SessionStatus.Idle, session.Snapshot.Status);
        Assert.Equal(1, downs);
        Assert.True(session.Start(request));
        while (session.Snapshot.IsActive) await Task.Delay(5, timeout.Token);
        Assert.Equal(2, downs);
    }
    [Fact]
    public async Task KeyRemainsDownUntilHoldCompletes()
    {
        var events = new ConcurrentQueue<NativeMethods.Input>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new WindowsKeyboardOutput(input => { events.Enqueue(input); return 1; }, _ => false, token => release.Task.WaitAsync(token));
        var pulse = output.PressAsync(0x57, CancellationToken.None).AsTask();
        var down = Assert.Single(events);
        Assert.Equal(0u, down.Data.Keyboard.Flags & 2);
        Assert.Equal(8u, down.Data.Keyboard.Flags & 8);
        Assert.Equal(0, down.Data.Keyboard.VirtualKey);
        Assert.NotEqual(0, down.Data.Keyboard.ScanCode);
        Assert.False(pulse.IsCompleted);
        release.SetResult();
        Assert.True(await pulse);
        var pair = events.ToArray();
        Assert.Equal(2, pair.Length);
        Assert.Equal(2u, pair[1].Data.Keyboard.Flags & 2);
        Assert.Equal(pair[0].Data.Keyboard.ScanCode, pair[1].Data.Keyboard.ScanCode);
        output.ReleasePending();
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task CancellationReleasesTheKeyBeforeCompleting()
    {
        var events = new ConcurrentQueue<NativeMethods.Input>();
        using var cancellation = new CancellationTokenSource();
        var output = new WindowsKeyboardOutput(input => { events.Enqueue(input); return 1; }, _ => false, token => Task.Delay(Timeout.Infinite, token));
        var pulse = output.PressAsync(0x57, cancellation.Token).AsTask();
        Assert.Single(events);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pulse);
        Assert.Equal(2u, events.Last().Data.Keyboard.Flags & 2);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task FailedKeyDownDoesNotSendAnUnrelatedKeyUp()
    {
        int sends = 0;
        var output = new WindowsKeyboardOutput(_ => { sends++; return 0; }, _ => false, _ => Task.CompletedTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => output.PressAsync(0x57, CancellationToken.None).AsTask());
        output.ReleasePending();
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task PhysicalModifierSkipsInput()
    {
        var output = new WindowsKeyboardOutput(_ => throw new Exception("No debe emitir"), key => key == 0x11, _ => Task.CompletedTask);
        Assert.False(await output.PressAsync(0x57, CancellationToken.None));
    }

    [Theory]
    [InlineData(0x25)]
    [InlineData(0x26)]
    [InlineData(0x27)]
    [InlineData(0x28)]
    public void NavigationRetainsTheExtendedKeyFlag(int key)
    {
        var input = WindowsKeyboardOutput.CreateDownInput(key);
        Assert.Equal(9u, input.Data.Keyboard.Flags);
    }
}
