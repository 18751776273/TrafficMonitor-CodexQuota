# Third-party notices

## TrafficMonitor

`vendor/TrafficMonitor/PluginInterface.h` is copied from
[zhongyang219/TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor),
commit `930f17533d6098989ebad62f210aa97d75ef174b`.

Copyright (c) <2019> <copyright zhongyang219>

It is licensed under the **Anti 996 License Version 1.0 (Draft)**. The original,
unmodified license is included at `vendor/TrafficMonitor/LICENSE` and must be
retained when redistributing the header or plugin built using it. The root MIT
license does not replace this third-party license.

## Design references

- [steipete/CodexBar](https://github.com/steipete/CodexBar), snapshot
  `c33760ecc34b122f985327aa917557b7753691f7`.
- [nesszer/Win-CodexBar](https://github.com/nesszer/Win-CodexBar), snapshot
  `b585d4887499c6b62d3a9ce7c22444e4bd283961`.

These projects informed the usage data source and window interpretation.
Their application code is not included in this repository or release package.

The release requires the separately installed Microsoft .NET Desktop Runtime.
Zig is a build tool downloaded separately; it is not shipped in the plugin ZIP.
