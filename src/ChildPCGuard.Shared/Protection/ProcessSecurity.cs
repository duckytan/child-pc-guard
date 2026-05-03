using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace ChildPCGuard.Shared.Protection;

/// <summary>
/// 进程安全描述符保护 - 防止任务管理器/其他进程终止本进程
/// 通过 SetKernelObjectSecurity 设置进程句柄 DACL
/// </summary>
public static class ProcessSecurity
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetKernelObjectSecurity(
        SafeHandle handle,
        SecurityInformation information,
        byte[] securityDescriptor,
        uint length,
        out uint lengthNeeded);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetKernelObjectSecurity(
        SafeHandle handle,
        SecurityInformation information,
        byte[] securityDescriptor);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint processAccess,
        bool inheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle GetCurrentProcess();

    [Flags]
    private enum SecurityInformation
    {
        Owner = 0x1,
        Group = 0x2,
        Dacl = 0x4,
        Label = 0x10,
        Attribute = 0x20,
        Scope = 0x40,
        ProcessTrustLabel = 0x80,
        Backup = 0x10000
    }

    private class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeProcessHandle(bool ownsHandle) : base(ownsHandle) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    /// <summary>
    /// 保护当前进程，防止被非特权进程终止
    /// DACL 规则：
    /// - SYSTEM: FullControl
    /// - Administrators: FullControl
    /// - CurrentUser: FullControl
    /// - 其他: PROCESS_QUERY_INFORMATION | SYNCHRONIZE（无 PROCESS_TERMINATE）
    /// </summary>
    public static bool ProtectCurrentProcess()
    {
        try
        {
            var currentProcess = GetCurrentProcess();
            var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

            // 创建 DACL
            var dacl = new DiscretionaryAcl(false, false, 16);

            // SYSTEM - FullControl
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            dacl.AddAccess(AccessControlType.Allow, unchecked((int)0x001F0FFF), // PROCESS_ALL_ACCESS
                          InheritanceFlags.None, PropagationFlags.None, systemSid);

            // Administrators - FullControl
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            dacl.AddAccess(AccessControlType.Allow, unchecked((int)0x001F0FFF),
                          InheritanceFlags.None, PropagationFlags.None, adminsSid);

            // Current User - FullControl
            var currentUser = WindowsIdentity.GetCurrent().User;
            dacl.AddAccess(AccessControlType.Allow, unchecked((int)0x001F0FFF),
                          InheritanceFlags.None, PropagationFlags.None, currentUser);

            // Everyone/Others - 仅查询和同步（无 PROCESS_TERMINATE）
            // PROCESS_QUERY_INFORMATION = 0x0400, SYNCHRONIZE = 0x001000
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            dacl.AddAccess(AccessControlType.Allow, 0x00000400 | 0x00100000,
                          InheritanceFlags.None, PropagationFlags.None, everyoneSid);

            // 构建安全描述符
            var securityDescriptor = new CommonSecurityDescriptor(
                false, false,
                ControlFlags.OwnerDefaulted | ControlFlags.GroupDefaulted | ControlFlags.DaclPresent,
                null, null, null, dacl, null, null);

            // 转换为二进制
            var sdBinary = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(sdBinary, 0);

            // 设置进程安全描述符
            var success = SetKernelObjectSecurity(
                currentProcess,
                SecurityInformation.Dacl,
                sdBinary);

            if (!success)
            {
                var error = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(error, "SetKernelObjectSecurity failed");
            }

            currentProcess.Close();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProcessSecurity] Failed to protect process: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 生成服务 DACL SDDL 字符串（用于 sc.exe sdset）
    /// 限制只有 SYSTEM 和 Administrators 能修改/停止服务
    /// </summary>
    public static string GenerateServiceSddl()
    {
        // D:(A;;CCLCSWRPLOCRRC;;;SY)(A;;CCLCSWLOCRRC;;;BA)
        // SY = SYSTEM, BA = BUILTIN\Administrators
        // CCLCSWLOCRRC = SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_QUERY_STATUS |
        //                SERVICE_ENUMERATE_DEPENDENTS | SERVICE_START | SERVICE_STOP |
        //                SERVICE_INTERROGATE | SERVICE_USER_DEFINED_CONTROL
        return "D:(A;;CCLCSWLOCRRC;;;SY)(A;;CCLCSWLOCRRC;;;BA)";
    }

    /// <summary>
    /// 验证当前进程是否拥有预期的 DACL（用于测试/调试）
    /// </summary>
    public static bool VerifyProcessDacl()
    {
        try
        {
            var currentProcess = GetCurrentProcess();

            var sdBinary = new byte[1024];
            if (!GetKernelObjectSecurity(currentProcess, SecurityInformation.Dacl, sdBinary, (uint)sdBinary.Length, out var needed))
            {
                var error = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(error, "GetKernelObjectSecurity failed");
            }

            var sd = new CommonSecurityDescriptor(false, false, sdBinary, 0);

            // 检查 SYSTEM 是否有 FullControl
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            bool systemHasFullControl = false;

            var dacl = sd.DiscretionaryAcl;
            if (dacl != null)
            {
                foreach (RawAcl acl in dacl)
                {
                    var ace = (CommonAce)acl;
                    if (ace.SecurityIdentifier.Equals(systemSid) &&
                        ace.AceType == AceType.AccessAllowed &&
                        (ace.AccessMask & unchecked((int)0x001F0FFF)) == unchecked((int)0x001F0FFF))
                    {
                        systemHasFullControl = true;
                        break;
                    }
                }
            }

            currentProcess.Close();
            return systemHasFullControl;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProcessSecurity] Failed to verify DACL: {ex.Message}");
            return false;
        }
    }
}
