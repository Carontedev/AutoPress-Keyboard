# Changelog

## 0.4.0-beta.1

- Retry a rejected key release on stop/emergency, and block new sessions until recovered.
- Keep recovery controls available instead of silently closing with a pending key.
- Preserve saved shortcuts when initial registration fails; restore the working selection after replacement conflicts.
- Bound the window to the current monitor work area, with scrolling for small screens.
- Verify packages before replacement; preserve the previous ZIP on failure and write SHA-256 checksums.
- Gate stable packaging on a chosen license, a valid signature, and a stable version.

This beta is not certified for game compatibility or anti-ban safety.
