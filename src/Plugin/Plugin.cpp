// Native MSVC-compatible C++ ABI; no CRT, MFC, or network activity in the host.
#include "../../vendor/TrafficMonitor/PluginInterface.h"
using DWORD = unsigned long;
using HANDLE = void*;
struct FILETIME { DWORD low; DWORD high; };
struct STARTUPINFO {
    DWORD cb; wchar_t* reserved; wchar_t* desktop; wchar_t* title;
    DWORD x, y, width, height, charsX, charsY, fill, flags;
    unsigned short show, reservedCount; unsigned char* reservedData;
    HANDLE input, output, error;
};
struct PROCESS_INFORMATION { HANDLE process, thread; DWORD pid, tid; };
extern "C" {
int _fltused = 0;
__declspec(dllimport) DWORD __stdcall GetModuleFileNameW(HANDLE, wchar_t*, DWORD);
__declspec(dllimport) DWORD __stdcall GetEnvironmentVariableW(const wchar_t*, wchar_t*, DWORD);
__declspec(dllimport) DWORD __stdcall GetPrivateProfileStringW(const wchar_t*, const wchar_t*, const wchar_t*, wchar_t*, DWORD, const wchar_t*);
__declspec(dllimport) void __stdcall GetSystemTimeAsFileTime(FILETIME*);
__declspec(dllimport) DWORD __stdcall GetCurrentProcessId();
__declspec(dllimport) DWORD __stdcall GetFileAttributesW(const wchar_t*);
__declspec(dllimport) void __stdcall AcquireSRWLockExclusive(void*);
__declspec(dllimport) void __stdcall ReleaseSRWLockExclusive(void*);
__declspec(dllimport) int __stdcall CreateProcessW(const wchar_t*, wchar_t*, void*, void*, int, DWORD, void*, const wchar_t*, STARTUPINFO*, PROCESS_INFORMATION*);
__declspec(dllimport) int __stdcall GetExitCodeProcess(HANDLE, DWORD*);
__declspec(dllimport) int __stdcall CloseHandle(HANDLE);
__declspec(dllimport) int __stdcall MessageBoxW(HANDLE, const wchar_t*, const wchar_t*, unsigned int);
}
void* operator new(unsigned long long, void* p) noexcept { return p; }
static HANDLE module;
static wchar_t statePath[1024];
static HANDLE worker;
static void* dataLock;
static unsigned long long lastLaunch;
struct Lock {
    Lock() { AcquireSRWLockExclusive(&dataLock); }
    ~Lock() { ReleaseSRWLockExclusive(&dataLock); }
};
static void copy(wchar_t* out, const wchar_t* src, int capacity) {
    int i = 0; for (; i + 1 < capacity && src[i]; ++i) out[i] = src[i]; out[i] = 0;
}
static int length(const wchar_t* s) { int n = 0; while (s[n]) ++n; return n; }
static void append(wchar_t* out, const wchar_t* s, int capacity) { int n = length(out); copy(out + n, s, capacity - n); }
static unsigned long long number(const wchar_t* s) {
    unsigned long long n = 0; int i = 0;
    for (; s[i]; ++i) { if (i >= 12 || s[i] < L'0' || s[i] > L'9') return 0; n = n * 10 + s[i] - L'0'; }
    return n;
}
static void decimal(DWORD value, wchar_t* out) {
    wchar_t reverse[16]; int n = 0; do { reverse[n++] = L'0' + value % 10; value /= 10; } while (value);
    for (int i = 0; i < n; ++i) out[i] = reverse[n-i-1]; out[n] = 0;
}
static unsigned long long nowSeconds() {
    FILETIME t; GetSystemTimeAsFileTime(&t);
    return (((unsigned long long)t.high << 32) | t.low) / 10000000 - 11644473600ULL;
}
static HANDLE launchSettings() {
    wchar_t path[1024]; DWORD size = GetModuleFileNameW(module, path, 1024);
    if (!size || size >= 950) return nullptr;
    for (int n = (int)size - 1; n >= 0; --n) if (path[n] == L'\\') { path[n+1] = 0; break; }
    append(path, L"CodexQuotaBridge\\CodexQuotaBridge.exe", 1024);
    wchar_t arguments[1200] = L"\""; append(arguments, path, 1200); append(arguments, L"\" --settings", 1200);
    STARTUPINFO startup{}; startup.cb = sizeof(startup); startup.flags = 1; startup.show = 1;
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(path, arguments, nullptr, nullptr, 0, 0x08000000, nullptr, nullptr, &startup, &process)) return nullptr;
    CloseHandle(process.thread); return process.process;
}
static void ensureBridge() {
    if (worker) {
        DWORD code; if (GetExitCodeProcess(worker, &code) && code == 259) return;
        CloseHandle(worker); worker = nullptr;
    }
    wchar_t disabled[8];
    if (GetEnvironmentVariableW(L"CODEX_QUOTA_NO_LAUNCH", disabled, 8) > 0) return;
    auto now = nowSeconds(); if (now < lastLaunch + 60) return; lastLaunch = now;
    wchar_t path[1024]; DWORD size = GetModuleFileNameW(module, path, 1024);
    if (!size || size >= 1024) return;
    for (int n = (int)size - 1; n >= 0; --n) if (path[n] == L'\\') { path[n+1] = 0; break; }
    append(path, L"CodexQuotaBridge\\CodexQuotaBridge.exe", 1024);
    if (GetFileAttributesW(path) == 0xffffffffUL) return;
    wchar_t arguments[1200] = L"\""; append(arguments, path, 1200); append(arguments, L"\" --watch --parent ", 1200);
    wchar_t pid[16]; decimal(GetCurrentProcessId(), pid); append(arguments, pid, 1200);
    // Launch occurs during DataRequired, outside DllMain / the loader lock.
    STARTUPINFO startup{}; startup.cb = sizeof(startup); startup.flags = 1; startup.show = 0;
    PROCESS_INFORMATION process{};
    if (CreateProcessW(path, arguments, nullptr, nullptr, 0, 0x08000000, nullptr, nullptr, &startup, &process)) {
        worker = process.process; CloseHandle(process.thread);
    }
}
class QuotaItem final : public IPluginItem {
public:
    bool weekly;
    wchar_t value[16];
    mutable wchar_t displayValue[16];
    explicit QuotaItem(bool w) : weekly(w) { copy(value, L"--", 16); }
    const wchar_t* GetItemName() const override { return weekly ? L"Codex 7d 额度" : L"Codex 5h 额度"; }
    const wchar_t* GetItemId() const override { return weekly ? L"CodexQuota.7d" : L"CodexQuota.5h"; }
    const wchar_t* GetItemLableText() const override { return weekly ? L"7d: " : L"5h: "; }
    const wchar_t* GetItemValueText() const override { Lock guard; copy(displayValue, value, 16); return displayValue; }
    const wchar_t* GetItemValueSampleText() const override { return L"100.0%*"; }
    void Read(unsigned long long now) {
        Lock guard;
        wchar_t until[32];
        GetPrivateProfileStringW(L"quota", weekly ? L"weekly_until" : L"five_until", L"0", until, 32, statePath);
        const auto deadline = number(until);
        if (deadline <= now || deadline > now + 3601) { copy(value, L"--", 16); return; }
        GetPrivateProfileStringW(L"quota", weekly ? L"weekly" : L"five", L"--", value, 16, statePath);
    }
};
class QuotaPlugin final : public ITMPlugin {
    QuotaItem five{false}; QuotaItem week{true}; wchar_t tooltip[1024]; wchar_t displayTooltip[1024];
public:
    QuotaPlugin() { copy(tooltip, L"Codex 剩余额度：正在连接", 1024); }
    OptionReturn ShowOptionsDialog(void* parent) override {
        auto process = launchSettings();
        if (process) CloseHandle(process);
        else MessageBoxW(parent, L"无法打开设置，请检查 CodexQuotaBridge 文件夹是否完整。", L"Codex Quota", 0x10);
        // Settings are saved by a separate UI process and hot-reloaded by the worker.
        return OR_OPTION_UNCHANGED;
    }
    int GetCommandCount() override { return 1; }
    const wchar_t* GetCommandName(int index) override { return index == 0 ? L"额度设置..." : L""; }
    void OnPluginCommand(int index, void* parent, void*) override { if (index == 0) ShowOptionsDialog(parent); }
    IPluginItem* GetItem(int index) override { return index == 0 ? &five : index == 1 ? &week : nullptr; }
    void DataRequired() override {
        ensureBridge(); const auto now = nowSeconds(); five.Read(now); week.Read(now);
        Lock guard;
        GetPrivateProfileStringW(L"quota", L"tooltip", L"Codex: 等待后台刷新；-- 表示暂不可用", tooltip, 1024, statePath);
        if (five.value[0] == L'-' && week.value[0] == L'-') append(tooltip, L" | 暂无有效额度（等待刷新或检查登录）", 1024);
    }
    const wchar_t* GetInfo(PluginInfoIndex index) override {
        switch (index) {
        case TMI_NAME: return L"Codex Quota";
        case TMI_DESCRIPTION: return L"Codex 5h / 7d 额度，可在插件选项调整刷新和显示；* 为缓存，-- 为不可用";
        case TMI_AUTHOR: return L"Personal project";
        case TMI_COPYRIGHT: return L"2026; TrafficMonitor interface by Zhong Yang";
        case TMI_VERSION: return L"0.2.0";
        default: return L"";
        }
    }
    const wchar_t* GetTooltipInfo() override { Lock guard; copy(displayTooltip, tooltip, 1024); return displayTooltip; }
};
extern "C" __declspec(dllexport) ITMPlugin* TMPluginGetInstance() {
    alignas(QuotaPlugin) static unsigned char storage[sizeof(QuotaPlugin)];
    static QuotaPlugin* instance;
    if (!instance) {
        DWORD count = GetEnvironmentVariableW(L"CODEX_QUOTA_STATE_DIR", statePath, 900);
        if (count > 0 && count < 900) append(statePath, L"\\snapshot.ini", 1024);
        else {
            count = GetEnvironmentVariableW(L"LOCALAPPDATA", statePath, 900);
            if (count == 0 || count >= 900) statePath[0] = 0;
            else append(statePath, L"\\CodexQuota\\snapshot.ini", 1024);
        }
        instance = new (storage) QuotaPlugin;
    }
    return instance;
}
extern "C" int __stdcall DllMain(HANDLE h, DWORD reason, void*) {
    if (reason == 1) module = h;
    if (reason == 0 && worker) CloseHandle(worker);
    return 1;
}
