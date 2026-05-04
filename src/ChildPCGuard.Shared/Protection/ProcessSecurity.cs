using System.Runtime.InteropServices;
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
        uint securityInformation,
        byte[] securityDescriptor,
        uint length,
        out uint lengthNeeded);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetKernelObjectSecurity(
        SafeHandle handle,
        uint securityInformation,
        byte[] securityDescriptor);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle GetCurrentProcess();

    private const uint DACL_SECURITY_INFORMATION = 0x00000004;
    private const int PROCESS_ALL_ACCESS = 0x001F0FFF;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private const int SYNCHRONIZE = 0x00100000;

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

            // 使用 P/Invoke 创建安全描述符
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var currentUser = WindowsIdentity.GetCurrent().User;
            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

            // 使用 RawSecurityDescriptor 和 RawAcl 构建
            var rawAcl = new RawAcl(0, 0);

            // SYSTEM - FullControl
            rawAcl.AddAce(new CommonAce(
                AceFlags.None,
                AceQualifier.AccessAllowed,
                PROCESS_ALL_ACCESS,
                systemSid,
                false,
                null));

            // Administrators - FullControl
            rawAcl.AddAce(new CommonAce(
                AceFlags.None,
                AceQualifier.AccessAllowed,
                PROCESS_ALL_ACCESS,
                adminsSid,
                false,
                null));

            // Current User - FullControl
            rawAcl.AddAce(new CommonAce(
                AceFlags.None,
                AceQualifier.AccessAllowed,
                PROCESS_ALL_ACCESS,
                currentUser,
                false,
                null));

            // Everyone/Others - 仅查询和同步（无 PROCESS_TERMINATE）
            rawAcl.AddAce(new CommonAce(
                AceFlags.None,
                AceQualifier.AccessAllowed,
                PROCESS_QUERY_INFORMATION | SYNCHRONIZE,
                everyoneSid,
                false,
                null));

            // 构建安全描述符
            var securityDescriptor = new RawSecurityDescriptor(
                ControlFlags.DiscretionaryAclPresent,
                null,
                null,
                null,
                rawAcl,
                null,
                null);

            // 转换为二进制
            var sdBinary = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(sdBinary, 0);

            // 设置进程安全描述符
            var success = SetKernelObjectSecurity(
                currentProcess,
                DACL_SECURITY_INFORMATION,
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
            if (!GetKernelObjectSecurity(currentProcess, DACL_SECURITY_INFORMATION, sdBinary, (uint)sdBinary.Length, out var needed))
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
                foreach (CommonAce ace in dacl)
                {
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
