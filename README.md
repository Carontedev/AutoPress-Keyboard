# AutoPress Keyboard

A compact Windows x64 keyboard automation utility built with C# / WPF and .NET 10. Runs locally without accounts, network connections, or keystroke logging.

## Version 0.4.0-beta.1

Publication-readiness fixes: recover rejected key releases without losing the pending key, preserve working shortcuts after conflicts, and scroll settings within the current monitor's available area.

This is an unsigned evaluation beta. Public distribution still requires an owner-selected license, code signing, and the manual checks in [the release checklist](docs/RELEASE_CHECKLIST.md).

- Standard Windows title bar and system menu, with native minimize and close controls. The glass panel and automation controls are unchanged.
- **No limit** disables duration and keystroke limits until you stop the session. Turning off the switch restores your previous limit.
- Click **Key**, then press the key to repeat. **Esc** or leaving the field cancels without changing the previous key. Capture is local to the focused field, not a global keyboard hook.
- Professional English interface with focused controls and clear startup-target guidance.
- Removed the built-in Input Monitor and Test input action. Existing monitor selections migrate to Any active window; other preferences are preserved.
- Optional application selection: choose **Any active window** or a specific **Startup target**.
- Existing key, interval, shortcut, target, and theme settings are preserved when upgrading.

## Run

Open `artifacts/AutoPress-win-x64/AutoPress.exe`. The portable executable includes the .NET runtime; no runtime installation is required. The downloadable package is `artifacts/AutoPress-win-x64.zip`.

## Controls

1. Click **Key** and press a supported letter, number, function, or navigation key. Set **Interval (ms)**.
2. Open settings using the gear, shortcut button, or target summary.
3. Under **Startup target**, choose:
   - **Any active window**: no application selection or initial target check is required.
   - A specific application: it must be focused when the three-second countdown ends.
   Enable **No limit** beside the **Session limit** heading if you do not want an automatic time or keystroke cutoff.
4. Press **F8** to start or stop. The **Start** button also starts the countdown.
5. Use **Ctrl + Alt + F10** for an emergency stop.

After activation, input always follows the foreground window. Switching windows does not pause or stop the session, including when a specific startup target was selected. Input is not delivered to a background application or every open window simultaneously.

The selected target is checked only at startup. A saved application that is not currently running remains selected and is marked **(not running)**; it is never silently replaced with unrestricted mode. Choose **Any active window** explicitly to remove the startup restriction.

The toggle shortcut supports F1–F11 and the available modifiers. Click **Apply** to register a change. A shortcut conflict preserves the previous working shortcut. F12 is reserved by Windows for debugging.

The repeat-key field accepts one key, not a modifier combination. The toggle and emergency shortcut keys are reserved. Global shortcuts cannot start a session while the field is recording a key; changing windows cancels only key capture, not an already-running session. Successful key assignments are saved immediately.

## Compact interface

The collapsed content panel measures **560 × 280 logical pixels**, plus the native Windows title bar and frame (576 × 319 overall on the tested display). Native Desktop Acrylic is available on Windows 11 22H2 or later. Unsupported systems, disabled transparency, and high-contrast mode use an opaque fallback.

- Drag the Windows title bar to move the window normally.
- The native close button stops and exits; minimize sends the application to the system tray. If Windows rejects a pending key release, the window stays open with a **Release key** action so you can retry instead of abandoning it. Open settings using the gear, shortcut button, or target summary.
- Select **Theme: System**, **Theme: Dark**, or **Theme: Light** in settings.
- Global shortcuts remain available while minimized.
- Closing, signing out, locking the session, or suspending Windows stops automation without automatic resume.

The interface uses WPF software rendering; Windows DWM composes the native backdrop.

## Limits and input behavior

- Interval: 50–60,000 ms, measured between the starts of successive presses.
- Bounded session limit: 1–86,400 seconds or 1–1,000,000 keystrokes. **No limit** applies neither limit, disables the limit fields without clearing them, and hides finite progress.
- Unlimited sessions still stop with the toggle shortcut, emergency stop, application close, Windows lock/suspend/disconnect, or input errors. Changing focus does not stop them. There is no automatic restart after a stop.
- Each synthetic press uses a Windows scan code, holds the key for 40 ms, then releases it. A virtual-key fallback is used if Windows supplies no scan code.
- Holding a physical Ctrl, Alt, Shift, Windows key, or the repeat key skips that tick.
- Normal stop and error paths release pending keys, including cancellation during a press.
- A rejected release blocks new sessions and settings changes. Focus a non-elevated window, then use **Release key**, the toggle shortcut, or emergency stop to retry. Retrying release does not start a new session. OS restrictions cannot be bypassed.
- This is repeated input, not continuous key holding. Timing depends on Windows scheduling.

Settings are stored in `%LocalAppData%\AutoPressKeyboard\settings\settings.json`. Legacy preferences are migrated to schema version 2.

## Compatibility

Windows input injection is subject to application permissions and input handling. An application can reject synthetic input; elevated targets may not accept input from a non-elevated utility. **Any active window** removes the startup target restriction, not these Windows or application limitations.

Reception inside Roblox has not been verified. This utility does not bypass anti-cheat or guarantee protection from account penalties. Use automation only where the game or server permits it. See [research](docs/research/autopress-key-research.md).

## Build and verify

Requires Windows x64 and .NET SDK 10.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Publish
```

The bootstrap installs the SDK in `%LocalAppData%\AutoPressKeyboard\sdk` without changing the global PATH. The build runs Core and Windows tests, then optionally publishes the executable and ZIP with SHA-256 hashes.

Close any other AutoPress instance before running the isolated, no-input UI check:

```powershell
& "$env:LOCALAPPDATA\AutoPressKeyboard\sdk\dotnet.exe" run --project tests/AutoPress.Windows.Smoke -c Release -- --render-only
```

This validates native caption hit testing, application selection, settings migration, themes, compact layout, shortcut conflicts, input validation, and countdown cancellation. It sends no keyboard input. Screenshots and isolated preferences are saved under `artifacts/qa`.

The optional interactive smoke test (`scripts/build.ps1 -Smoke`) emits input only after acquiring its own test window. Do not run it while gaming or working in another application. Interactive keyboard acceptance remains separate from the no-input checks.

See [validation notes](docs/VALIDATION.md) for verified results and remaining limitations.

Packaging verifies the executable inside a temporary ZIP before replacing the previous package. A SHA-256 sidecar is generated. Use `scripts/test-package.ps1` to exercise failure preservation. The package script's `-Stable` gate requires a LICENSE, a valid signature, and a non-prerelease version; it is not a substitute for manual validation.

## Project structure

- `src/AutoPress.Core`: session lifecycle, limits, cancellation, and validation.
- `src/AutoPress.Windows`: Win32 keyboard output, shortcuts, and application enumeration.
- `src/AutoPress.App`: WPF interface, tray, themes, and preferences.
- `tests/AutoPress.Core.Tests`: session and cleanup tests.
- `tests/AutoPress.Windows.Tests`: injected keyboard-sender tests without real input.
- `tests/AutoPress.Windows.Smoke`: UI checks and optional interactive input tests.
