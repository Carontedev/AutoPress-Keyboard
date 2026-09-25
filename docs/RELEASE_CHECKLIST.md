# Release checklist

## Automated checks

- Build Release and run Core/Windows tests: scripts/build.ps1.
- Run the WPF smoke project with --render-only (close other AutoPress instances).
- Run scripts/test-package.ps1 to check archive integrity, failure preservation, and the stable gate.
- Run scripts/verify-release.ps1 against the published executable.
- Inspect small-screen, light, and dark screenshots in artifacts/qa.

## Required owner decisions and manual validation

- Select the distribution license and add LICENSE; review bundled runtime notices.
- Sign the final executable using an owner-controlled code-signing certificate and trusted timestamp.
- Test physical shortcuts and paired key down/up on a dedicated test desktop.
- Test real lock/unlock, suspend/resume, tray recovery, mixed-DPI monitors, and 125–200% scaling.
- Run an extended unlimited-session soak test on a dedicated test receiver.
- Validate supported games individually and check their automation rules.

No physical keyboard, lock, suspend, or game test is implied by the no-input smoke.
The package script's -Stable option checks license presence, signature validity,
and a non-prerelease version. It does not certify this manual checklist.

Beta ZIPs have an adjacent SHA-256 file. Packaging verifies the archived executable
and replaces an existing ZIP only after success. Do not package while the release
executable is open; a failed package leaves the previous ZIP intact.
