# AutoPress Keyboard logo

- `logo.png`: current user-supplied artwork from Curvas.png, preserved without modification.
- `logo-purple.png`: previous purple single-key artwork, retained as a backup.
- `logo-original.png`: previous keyboard artwork, retained as a backup.
- `autopress.ico`: Windows executable, title-bar/taskbar and notification-area icon.
  Contains 16, 20, 24, 32, 48, 64, 128 and 256 px images.

Regenerate from the repository root with:

```powershell
powershell -NoProfile -File scripts/create-icon.ps1
```

Conversion removes only fully transparent outer margins, preserves colors and
aspect ratio, and centers the artwork on a transparent square. No artwork is
redrawn. At native 16 px sizes the detailed keyboard will naturally lose detail.

Supplying this artwork does not establish its licensing or redistribution rights;
confirm those before public distribution.
