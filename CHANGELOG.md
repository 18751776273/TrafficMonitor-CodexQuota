# Changelog

## Unreleased

- Replace each taskbar quota percentage with its local reset date and time when
  the remaining quota rounds down to zero at the selected precision.
- Reserve enough taskbar width for reset times, including the stale marker.
- Preserve reset-only hover details with a countdown updated every minute;
  connection status remains available in the settings window.

## 0.2.0

First public release.

- Display Codex 5-hour and 7-day quota in TrafficMonitor's existing taskbar window.
- Settings UI for refresh interval, remaining/used percentage, decimal precision,
  and cache lifetime; settings apply without restarting TrafficMonitor.
- Default 120-second refresh with exponential failure backoff and Retry-After support.
- Show stale data with `*`, and missing or expired data with `--`.
- Native x64 DLL, .NET helper, installer with backups, and source/build instructions.
