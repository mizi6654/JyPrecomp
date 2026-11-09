using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace JyPrecomp.Core;

/// <summary>
/// 定位/激活剪映主窗口。参考悬浮球助手：JianyingPro.exe + Qt5QWindowIcon。
/// </summary>
public static class JianyingWindow
{
    private const string ProcessName = "JianyingPro";
    private const string TrayProcessName = "JianyingProTray";

    public static bool IsRunning()
    {
        return Process.GetProcessesByName(ProcessName).Length > 0
            || Process.GetProcessesByName(TrayProcessName).Length > 0;
    }

    public static IntPtr FindMainWindow()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            GetWindowThreadProcessId(hWnd, out uint pid);
            try
            {
                using var p = Process.GetProcessById((int)pid);
                if (!p.ProcessName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { return true; }

            var sb = new StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            string cls = sb.ToString();
            // 剪映主窗常见 Qt 类名
            if (cls.Contains("Qt", StringComparison.OrdinalIgnoreCase) ||
                cls.Contains("Jianying", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(cls))
            {
                RECT r = default;
                GetWindowRect(hWnd, ref r);
                int w = r.Right - r.Left, h = r.Bottom - r.Top;
                if (w > 400 && h > 300)
                {
                    found = hWnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static bool Activate(Action<string>? log = null)
    {
        var h = FindMainWindow();
        if (h == IntPtr.Zero)
        {
            log?.Invoke("未找到剪映主窗口，请先打开剪映并进入草稿");
            return false;
        }

        uint foreTid = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint targetTid = GetWindowThreadProcessId(h, out _);
        if (foreTid != targetTid)
            AttachThreadInput(foreTid, targetTid, true);

        // 禁止 SW_RESTORE：会把最大化窗口还原成小窗
        if (IsZoomed(h))
        {
            // 保持最大化，只置前
        }
        else if (IsIconic(h))
        {
            ShowWindow(h, 9); // 仅最小化时还原
        }
        BringWindowToTop(h);
        bool ok = SetForegroundWindow(h);

        if (foreTid != targetTid)
            AttachThreadInput(foreTid, targetTid, false);

        log?.Invoke(ok ? "已置前剪映窗口" : "置前可能失败，请手动点一下剪映窗口");
        Thread.Sleep(400);
        return true;
    }

    public static string? FindJianyingExe(string? configured = null)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            try
            {
                string? path = p.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }
            catch { }
        }

        // 注册表 / 常见路径
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Bytedance\JianyingPro");
            // 历史草稿路径旁可能有安装信息；再试 LocalAppData
        }
        catch { }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates =
        {
            Path.Combine(local, "JianyingPro", "Apps", "JianyingPro.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "JianyingPro", "JianyingPro.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        // Apps 下版本目录
        string apps = Path.Combine(local, "JianyingPro", "Apps");
        if (Directory.Exists(apps))
        {
            var exe = Directory.EnumerateFiles(apps, "JianyingPro.exe", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (exe != null) return exe;
        }
        return null;
    }

    public static bool KillJianying(Action<string>? log = null)
    {
        int n = 0;
        foreach (var name in new[] { ProcessName, TrayProcessName })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(5000);
                    n++;
                }
                catch (Exception ex)
                {
                    log?.Invoke($"结束进程 {name} 失败: {ex.Message}");
                }
            }
        }
        log?.Invoke(n > 0 ? $"已结束剪映相关进程 ({n})" : "未发现剪映进程");
        Thread.Sleep(800);
        return true;
    }

    public static bool StartJianying(string? exePath = null, Action<string>? log = null)
    {
        string? exe = FindJianyingExe(exePath);
        if (exe == null)
        {
            log?.Invoke("找不到 JianyingPro.exe，请在设置中指定路径");
            return false;
        }
        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            log?.Invoke($"已启动: {exe}");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke("启动失败: " + ex.Message);
            return false;
        }
    }

    #region Win32

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    #endregion
}
