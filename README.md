# Codex Quota for TrafficMonitor

在 TrafficMonitor 原有任务栏窗口中，同时显示网速与 Codex 的 **5 小时 / 7 天额度**。不另建任务栏浮窗，可避免两个独立工具相互重叠。

```text
↑: 15.6 KB/s    5h: 65%
↓: 22.5 KB/s    7d: 95%
```

以上数字仅为示意。默认显示剩余百分比，沿用 TrafficMonitor 的字体、颜色和双行布局。

- `65%`：最近一次查询成功的额度。
- `65%*`：查询失败后暂时保留的旧值。
- 剩余额度按所选精度显示为 0 时，该项自动改为本地恢复时间（如 `10/29 08:30`）；5h、7d 分别使用自己的恢复时间，两项同时归零时都显示时间。已用模式同样在剩余额度归零时显示恢复时间；已用 0% 仍显示百分比。缓存时间后保留 `*`，过期或到达恢复时间后显示 `--`，等待刷新。
- `--`：尚未登录、缺少该窗口、数据过期或等待首次查询。

[下载最新版](https://github.com/18751776273/TrafficMonitor-CodexQuota/releases/latest) · [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor)

## 运行要求

- Windows x64 和 **x64 版 TrafficMonitor**，已在 1.86 验证；其他版本尚未完整验证。
- [.NET 9 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/9.0)：请选择 Windows Desktop Runtime。下载包不包含此运行库。
- Codex 使用 ChatGPT 账号登录，且 `%USERPROFILE%\.codex\auth.json` 中存在 OAuth 凭据；支持用 `CODEX_HOME` 指定目录。仅保存在其他凭据存储中的登录和 API Key 模式不受支持。
- 使用脚本安装和构建需要 PowerShell 7；也可以手动安装。

这是独立社区插件，与 OpenAI、TrafficMonitor 或 CodexBar 官方无隶属关系。额度读取使用现有 ChatGPT 用量端点，接口变化可能导致插件失效。

## 安装

1. 下载 Release 中的 `CodexQuota-0.2.0-win-x64.zip` 并解压，安装上述运行库。
2. 退出 TrafficMonitor 和插件设置窗口，等待后台程序退出。
3. 在解压目录打开 PowerShell 7，执行以下命令，把路径替换成自己的安装目录：

```powershell
.\scripts\install.ps1 -TrafficMonitorDir 'C:\Apps\TrafficMonitor'
```

4. 重新启动 TrafficMonitor。

脚本会备份原配置及已有同名插件到解压目录下的 `backups`，启用 5h/7d 两个显示项，并设置双行、数值左对齐和 12 的组间距。网速数值及额度数值会紧跟标签，组与组之间留有间隔。若安装目录需要管理员权限，请使用管理员 PowerShell。

手动安装：把 ZIP 中 `dist` 内的 `CodexQuota.dll` 和整个 `CodexQuotaBridge` 文件夹复制到 TrafficMonitor 的 `plugins` 文件夹。在插件管理中确认插件已加载，然后在任务栏显示项中勾选 5h/7d。保留随包附带的许可证和第三方声明。安装后的结构：

```text
TrafficMonitor/
  TrafficMonitor.exe
  plugins/
    CodexQuota.dll
    CodexQuotaBridge/
      CodexQuotaBridge.exe
      ...其他运行文件
```

首次安装脚本需要现有 `config.ini` 中包含 `[task_bar]` 设置；请先运行并配置过 TrafficMonitor。其他配置存储方式可采用手动安装。

## 设置

打开 **TrafficMonitor → 插件管理 → Codex Quota → 选项**，或插件菜单的“额度设置...”。不同宿主版本的菜单位置可能略有不同。

| 选项 | 默认值 | 可选范围 |
| --- | --- | --- |
| 刷新间隔 | 120 秒 | 30～300 秒 |
| 百分比含义 | 剩余 | 剩余 / 已用 |
| 小数位 | 0 | 整数 / 一位小数 |
| 缓存有效期 | 10 分钟 | 10～60 分钟 |

保存后自动生效，无需重启。小数位只控制显示精度，不保证服务端提供小数数据。可在窗口中预览显示样式和恢复默认。

失败后自动退避，最少等待 2 分钟，连续失败时逐步延长到 30 分钟；服务端 `Retry-After` 要求更久时按服务端要求等待。修改间隔不会绕过正在生效的退避。默认 120 秒可以减少后台请求；这里的可选间隔不代表服务端对请求频率的承诺。

设置保存在 `%LOCALAPPDATA%\CodexQuota\settings.json`。也可通过脚本只修改刷新间隔：

```powershell
.\scripts\set-refresh.ps1 -Seconds 120
```

TrafficMonitor 每秒读取本地快照，后台按配置间隔查询网络；插件设置不影响网速刷新频率。

## 更新与卸载

退出 TrafficMonitor 和插件设置窗口，在新版解压目录执行：

```powershell
.\scripts\update-plugin.ps1 -TrafficMonitorDir 'C:\Apps\TrafficMonitor'
```

更新会备份旧插件，保留布局与设置。需要重新应用推荐间距时，退出 TrafficMonitor 后执行：

```powershell
.\scripts\apply-layout.ps1 -TrafficMonitorDir 'C:\Apps\TrafficMonitor'
```

卸载时退出 TrafficMonitor，移走 `plugins\CodexQuota.dll` 和 `plugins\CodexQuotaBridge`，取消相关显示项，再启动。需要恢复原布局时，可用安装前的 `backups` 中 `config.ini` 覆盖原配置；此操作也会恢复其他配置到备份时的状态。用户设置可保留，或自行删除 `%LOCALAPPDATA%\CodexQuota`。

## 数据与故障排查

- 只读取现有 OAuth 凭据，不写回、不主动续签；登录过期时请在 Codex 中重新登录。
- 请求仅发往 `https://chatgpt.com/backend-api/wham/usage`，禁止自动跟随重定向。不扫描会话历史或浏览器 Cookie。
- 本地 `snapshot.ini` 只保存显示文本和时效提示，不保存 token、邮箱、账号 ID 或原始响应。
- 缓存达到有效期或窗口重置时间后显示 `--`。即使后台异常退出，DLL 也会独立拒绝过期数据。
- 无对应 5 小时或 7 天窗口时显示 `--`；不会把其他时长的窗口错误标为 5h/7d。
- 插件没有加载：检查 TrafficMonitor 是否为 x64、DLL 是否放在正确的 `plugins` 目录、文件是否被系统阻止。
- 一直显示 `--`：检查 Desktop Runtime、Codex 登录状态、网络及悬停提示。
- 默认快照有效期是 10 分钟，可在配置界面调整到 60 分钟。

后台随 TrafficMonitor 启动，宿主退出后后台退出。`CODEX_QUOTA_STATE_DIR` 可隔离测试快照；`CODEX_QUOTA_NO_LAUNCH=1` 仅用于 DLL 测试，正常使用无需设置。

## 从源码构建

需要 Windows x64、PowerShell 7、.NET 9 SDK 和网络连接。构建脚本通过官方 SHA256 校验下载的便携 Zig 0.15.2，不修改系统 PATH。

```powershell
.\scripts\bootstrap.ps1
.\scripts\build.ps1
dotnet run --project tests/Tests.csproj -- dist/CodexQuota.dll
.\tests\config-tests.ps1
.\scripts\package.ps1
```

构建产物在 `dist`，ZIP 和 SHA256 校验文件在 `release`。插件使用 MSVC ABI，无需 MFC 或额外 C++ 运行库。GitHub Actions 同样执行构建、测试和打包。

已覆盖额度解析、缓存、刷新设置、失败退避、原生 DLL 接口和配置修改测试。尚未完整覆盖多屏、各类 DPI、Explorer 重启和不同 TrafficMonitor 版本。

## 许可证与致谢

原创代码采用 [MIT License](LICENSE)。TrafficMonitor 的插件接口保留其 **Anti 996 License Version 1.0 (Draft)**，详见 [第三方声明](THIRD_PARTY_NOTICES.md) 和 [原始许可证](vendor/TrafficMonitor/LICENSE)；分发时请一并保留。

感谢 [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor) 提供插件宿主，以及 [CodexBar](https://github.com/steipete/CodexBar) 和 [Win-CodexBar](https://github.com/nesszer/Win-CodexBar) 的用量数据源实现参考。本项目独立实现小型后台，不包含这些项目的完整 GUI 或源码仓库。
