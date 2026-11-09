using System.Runtime.InteropServices;

namespace JyPrecomp.Core;

public static class InputSender
{
    public static void SendSelectAll(Action<string>? log = null)
    {
        log?.Invoke("发送 Ctrl+A（全选）");
        SendChord(ctrl: true, vk: 0x41);
        Thread.Sleep(500);
    }

    public static void SendCreateCompound(Action<string>? log = null)
    {
        log?.Invoke("发送 Alt+G（新建复合片段）");
        SendChord(alt: true, vk: 0x47);
        Thread.Sleep(700);
    }

    public static void SendPrecompose(Action<string>? log = null)
    {
        log?.Invoke("发送 Ctrl+Alt+J（预合成复合片段）");
        SendChord(ctrl: true, alt: true, vk: 0x4A);
        Thread.Sleep(700);
    }

    public static void SendSave(Action<string>? log = null)
    {
        log?.Invoke("发送 Ctrl+S（保存）");
        SendChord(ctrl: true, vk: 0x53);
        Thread.Sleep(600);
    }

    public static void SendDelete(Action<string>? log = null)
    {
        log?.Invoke("发送 Delete（清空选中片段）");
        keybd_event(0x2E, 0, 0, UIntPtr.Zero); // VK_DELETE down
        Thread.Sleep(40);
        keybd_event(0x2E, 0, 2, UIntPtr.Zero);
        Thread.Sleep(400);
    }

    public static void SendUndo(Action<string>? log = null)
    {
        log?.Invoke("发送 Ctrl+Z（撤销）");
        SendChord(ctrl: true, vk: 0x5A); // Z
        Thread.Sleep(280);
    }

    public static void SendExport(Action<string>? log = null)
    {
        log?.Invoke("发送 Ctrl+E（导出）");
        SendChord(ctrl: true, vk: 0x45); // E
        Thread.Sleep(500);
    }

    public static void SendHome(Action<string>? log = null)
    {
        log?.Invoke("发送 Home（播放头归零）");
        keybd_event(0x24, 0, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        keybd_event(0x24, 0, 2, UIntPtr.Zero);
        Thread.Sleep(200);
    }

    public static void ClickWindowCenter(IntPtr hWnd, Action<string>? log = null)
    {
        if (hWnd == IntPtr.Zero) return;
        RECT r = default;
        if (!GetWindowRect(hWnd, ref r)) return;
        int x = (r.Left + r.Right) / 2;
        int y = r.Top + (r.Bottom - r.Top) / 3;
        SetCursorPos(x, y);
        Thread.Sleep(80);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        log?.Invoke($"已点击剪映以夺取焦点 ({x},{y})");
        Thread.Sleep(200);
    }

    /// <summary>
    /// 简易拖拽：从屏幕点 press → 移到目标 → release。
    /// 用于把资源管理器中的文件拖到剪映时间线区域。
    /// </summary>
    public static void DragScreen(int fromX, int fromY, int toX, int toY, Action<string>? log = null)
    {
        log?.Invoke($"拖拽 ({fromX},{fromY}) → ({toX},{toY})");
        SetCursorPos(fromX, fromY);
        Thread.Sleep(120);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero); // down
        Thread.Sleep(80);

        // 分步移动，部分程序需要中间点
        int steps = 12;
        for (int i = 1; i <= steps; i++)
        {
            int x = fromX + (toX - fromX) * i / steps;
            int y = fromY + (toY - fromY) * i / steps;
            SetCursorPos(x, y);
            Thread.Sleep(30);
        }

        Thread.Sleep(100);
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero); // up
        Thread.Sleep(300);
    }

    private static void SendChord(bool ctrl = false, bool alt = false, bool shift = false, byte vk = 0)
    {
        if (ctrl) keybd_event(0x11, 0, 0, UIntPtr.Zero);
        if (alt) keybd_event(0x12, 0, 0, UIntPtr.Zero);
        if (shift) keybd_event(0x10, 0, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
        Thread.Sleep(30);
        if (shift) keybd_event(0x10, 0, 2, UIntPtr.Zero);
        if (alt) keybd_event(0x12, 0, 2, UIntPtr.Zero);
        if (ctrl) keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }

    #region Win32
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    #endregion
}
