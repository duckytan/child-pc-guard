using ChildPCGuard.Shared.Win32;
using System.Runtime.InteropServices;

namespace ChildPCGuard.LockOverlay;

/// <summary>
/// 全局低级键盘钩子 - 拦截 Alt+Tab、Win 键等系统快捷键
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private bool _disposed = false;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    /// <summary>
    /// 安装键盘钩子
    /// </summary>
    public bool Install()
    {
        using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;

        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule?.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"[KeyboardHook] Install failed, error: {error}");
            return false;
        }

        System.Diagnostics.Debug.WriteLine("[KeyboardHook] Installed");
        return true;
    }

    /// <summary>
    /// 卸载键盘钩子
    /// </summary>
    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            System.Diagnostics.Debug.WriteLine("[KeyboardHook] Uninstalled");
        }
    }

    /// <summary>
    /// 钩子回调函数 - 拦截特定按键
    /// </summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)NativeMethods.WM_KEYDOWN ||
                             wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int vkCode = (int)hookStruct.vkCode;

            // 拦截以下按键：
            if (ShouldBlockKey(vkCode))
            {
                return (IntPtr)1; // 阻止消息传递
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>
    /// 判断是否应该拦截此按键
    /// </summary>
    private static bool ShouldBlockKey(int vkCode)
    {
        return vkCode switch
        {
            // Win 键 (LWIN/RWIN)
            0x5B or 0x5C => true,

            // Alt 键 (LALT/RALT) - 当配合其他键时拦截
            0x12 => true,

            // Tab 键（配合 Alt 拦截 Alt+Tab）
            0x09 when IsAltPressed() => true,

            // Esc 键（配合 Alt 或 Ctrl 拦截）
            0x1B when IsAltPressed() || IsCtrlPressed() => true,

            // F4 键（配合 Alt 拦截 Alt+F4）
            0x73 when IsAltPressed() => true,

            // 其他需要拦截的按键可在此添加

            _ => false
        };
    }

    private static bool IsAltPressed()
    {
        // 检查 Alt 键状态
        short state = NativeMethods.GetKeyState(0x12); // VK_MENU
        return (state & 0x8000) != 0;
    }

    private static bool IsCtrlPressed()
    {
        short state = NativeMethods.GetKeyState(0x11); // VK_CONTROL
        return (state & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Uninstall();
    }
}
