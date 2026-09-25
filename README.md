<p align="center">
  <img src="docs/assets/logo.png" alt="AutoPress Keyboard" width="220">
</p>

<p align="center">
  <a href="https://github.com/Carontedev/AutoPress-Keyboard/releases/download/v0.4.0-beta.1/AutoPress-Keyboard-v0.4.0-beta.1-win-x64.zip"><img src="https://img.shields.io/badge/Download-Windows_x64-8B5CF6?style=for-the-badge" alt="Download AutoPress Keyboard for Windows x64"></a>
</p>

<h1 align="center">AutoPress Keyboard</h1>

<p align="center">
  <strong>Keyboard automation with precise controls and a compact Windows interface.</strong><br>
  Repeat a key, set your interval, and manage sessions with global shortcuts.<br>
  Runs locally — no accounts, network connections, or keystroke logging.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-Beta-8B5CF6?style=flat-square" alt="Status: Beta">
  <img src="https://img.shields.io/badge/Platform-Windows_x64-0078D4?style=flat-square" alt="Platform: Windows x64">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square" alt=".NET 10">
  <img src="https://img.shields.io/badge/UI-WPF-0D7490?style=flat-square" alt="Interface: WPF">
  <img src="https://img.shields.io/badge/Distribution-Portable-brightgreen?style=flat-square" alt="Portable distribution">
</p>

<p align="center">
  <a href="#key-features"><strong>Features</strong></a> ·
  <a href="#getting-started"><strong>Getting Started</strong></a> ·
  <a href="#build"><strong>Build</strong></a> ·
  <a href="#compatibility--status"><strong>Compatibility</strong></a> ·
  <a href="docs/USER_GUIDE.md"><strong>User Guide</strong></a>
</p>

---

## Key Features

- **Click-to-capture** — select the key field and press the key to automate.
- **Adjustable intervals** — from 50 to 60,000 milliseconds between presses.
- **Session controls** — stop after a duration, a keystroke count, or run with **No limit**.
- **Global shortcuts** — start, stop, and trigger an emergency stop without opening the app.
- **Optional startup target** — choose a specific application or **Any active window**.
- **Native Windows experience** — standard title bar, system tray, and light, dark, or system themes.

## Getting Started

1. [Download the Windows ZIP](https://github.com/Carontedev/AutoPress-Keyboard/releases/download/v0.4.0-beta.1/AutoPress-Keyboard-v0.4.0-beta.1-win-x64.zip).
2. Right-click the ZIP and choose **Extract All**.
3. Open **AutoPress.exe**.

No installer, account, or separate .NET installation is needed. The ZIP contains only the app and a `README.txt`. Choose this ZIP, not GitHub's automatically generated **Source code** downloads.

### Quick Start

1. Click **Key** and press the key you want to repeat.
2. Set **Interval (ms)** and, optionally, a session limit in settings.
3. Press **F8** to start the three-second countdown. Press it again to stop.

**Emergency stop:** `Ctrl + Alt + F10`. The toggle shortcut is configurable.

> Input follows the active window. Switching windows does **not** stop automation. A selected startup target is checked only when the countdown ends; it does not receive background input.

## Build

Requires Windows x64. Bootstrap installs the .NET 10 SDK locally if needed.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Publish
```

The build runs unit tests and produces `artifacts/AutoPress-win-x64/AutoPress.exe`, a ZIP package, and its SHA-256 checksum.

## Compatibility & Status

This is an **unsigned beta**. Applications may reject synthetic input, and game compatibility is not guaranteed. AutoPress does not bypass anti-cheat or provide protection against bans. Use it only where automation is permitted.

Windows may show an unrecognized-app warning because this beta is not digitally signed. Do not disable Windows or antivirus protections.

A distribution license has not yet been selected. See [distribution status](DISTRIBUTION.md) before redistributing.

---

[User guide](docs/USER_GUIDE.md) · [Changelog](CHANGELOG.md) · [Validation](docs/VALIDATION.md) · [Release checklist](docs/RELEASE_CHECKLIST.md)
