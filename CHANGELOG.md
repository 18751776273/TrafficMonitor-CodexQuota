# Changelog

## 0.2.0

First public release.

- Display Codex 5-hour and 7-day quota in TrafficMonitor's existing taskbar window.
- Settings UI for refresh interval, remaining/used percentage, decimal precision,
  and cache lifetime; settings apply without restarting TrafficMonitor.
- Default 120-second refresh with exponential failure backoff and Retry-After support.
- Show stale data with `*`, and missing or expired data with `--`.
- Native x64 DLL, .NET helper, installer with backups, and source/build instructions.
