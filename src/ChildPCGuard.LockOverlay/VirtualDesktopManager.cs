using ChildPCGuard.Shared.Win32;
using System.Runtime.InteropServices;

namespace ChildPCGuard.LockOverlay;

/// <summary>
/// 虚拟桌面管理器 - 创建独立的锁屏桌面
/// </summary>
public sealed class VirtualDesktopManager : IDisposable
{
    private IntPtr _lockDesktop = IntPtr.Zero;
    private IntPtr _originalDesktop = IntPtr.Zero;
    private bool _disposed = false;

    public bool CreateLockDesktop()
    {
        if (_lockDesktop != IntPtr.Zero) return true;

        try
        {
            _originalDesktop = GetThreadDesktop();
            _lockDesktop = NativeMethods.CreateDesktop(
                "ChildPCGuard_Lock",
                IntPtr.Zero, IntPtr.Zero, 0,
                NativeMethods.GENERIC_ALL, IntPtr.Zero);

            if (_lockDesktop == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            System.Diagnostics.Debug.WriteLine("[VirtualDesktop] Desktop created");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VirtualDesktop] Create failed: {ex.Message}");
            return false;
        }
    }

    public bool SwitchToLockDesktop()
    {
        if (_lockDesktop == IntPtr.Zero) return false;
        return NativeMethods.SwitchDesktop(_lockDesktop);
    }

    public bool SwitchToOriginalDesktop()
    {
        if (_originalDesktop == IntPtr.Zero) return false;
        return NativeMethods.SwitchDesktop(_originalDesktop);
    }

    private IntPtr GetThreadDesktop()
    {
        return NativeMethods.GetThreadDesktop(NativeMethods.GetCurrentThreadId());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SwitchToOriginalDesktop();

        if (_lockDesktop != IntPtr.Zero)
        {
            NativeMethods.CloseDesktop(_lockDesktop);
            _lockDesktop = IntPtr.Zero;
        }
    }
}
