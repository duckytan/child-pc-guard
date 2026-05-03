using System.Runtime.InteropServices;

namespace ChildPCGuard.Shared.Win32;

/// <summary>
/// Win32 API P/Invoke 声明
/// </summary>
internal static class NativeMethods
{
    // ── user32.dll ──────────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr CreateDesktop(
        string lpszDesktop,
        IntPtr lpszDevice,
        IntPtr pDevmode,
        int dwFlags,
        uint dwDesiredAccess,
        IntPtr lpsa);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SwitchDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── kernel32.dll ─────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint GetTickCount();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    // ── advapi32.dll ──────────────────────────────────────────────────────────

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetKernelObjectSecurity(
        IntPtr Handle,
        SecurityInformation SecurityInformation,
        IntPtr SecurityDescriptor);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InitializeSecurityDescriptor(
        IntPtr pSecurityDescriptor,
        uint dwRevision);

    // ── 常量 ──────────────────────────────────────────────────────────────────

    /// <summary>GetSystemMetrics: 检测是否在安全模式启动 (SM_CLEANBOOT)</summary>
    internal const int SM_CLEANBOOT = 67;

    /// <summary>安全模式（0=正常，1=安全模式，2=带网络安全模式）</summary>
    internal const int CLEAN_BOOT_NORMAL = 0;

    internal const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_SYSKEYDOWN = 0x0104;

    // 桌面访问权限
    internal const uint DESKTOP_CREATEWINDOW = 0x0002;
    internal const uint DESKTOP_ENUMERATE = 0x0040;
    internal const uint DESKTOP_WRITEOBJECTS = 0x0080;
    internal const uint DESKTOP_SWITCHDESKTOP = 0x0100;
    internal const uint DESKTOP_READOBJECTS = 0x0001;
    internal const uint DESKTOP_HOOKCONTROL = 0x0008;
    internal const uint GENERIC_ALL = 0x10000000;

    [Flags]
    internal enum SecurityInformation : uint
    {
        Dacl = 0x00000004
    }

    // ── 结构体 ────────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // ── 委托 ──────────────────────────────────────────────────────────────────

    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
}
