using System.Security.AccessControl;
using System.Security.Principal;
using System.IO;

namespace ChildPCGuard.Shared.Protection;

/// <summary>
/// 文件和目录 ACL 保护
/// 防止删除/修改程序文件和配置文件
/// </summary>
public static class FileSecurityManager
{
    /// <summary>
    /// 保护安装目录及所有子文件
    /// DACL 规则：
    /// - SYSTEM: FullControl
    /// - Administrators: FullControl
    /// - 其他: ReadAndExecute（只读，不能写入/删除）
    /// </summary>
    public static bool ProtectInstallDirectory(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                System.Diagnostics.Debug.WriteLine($"[FileSecurity] Directory not found: {directoryPath}");
                return false;
            }

            var directoryInfo = new DirectoryInfo(directoryPath);
            var security = directoryInfo.GetAccessControl();

            // 移除继承，设置显式 DACL
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // 清空现有规则
            var rules = security.GetAccessRules(true, true, typeof(NTAccount));
            foreach (FileSystemAccessRule rule in rules)
            {
                security.RemoveAccessRule(rule);
            }

            // SYSTEM - FullControl
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var systemRule = new FileSystemAccessRule(
                systemSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);
            security.AddAccessRule(systemRule);

            // Administrators - FullControl
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var adminsRule = new FileSystemAccessRule(
                adminsSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);
            security.AddAccessRule(adminsRule);

            // Users/Everyone - ReadAndExecute（只读）
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var usersRule = new FileSystemAccessRule(
                usersSid,
                FileSystemRights.ReadAndExecute | FileSystemRights.ReadAttributes | FileSystemRights.ReadPermissions,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);
            security.AddAccessRule(usersRule);

            directoryInfo.SetAccessControl(security);

            System.Diagnostics.Debug.WriteLine($"[FileSecurity] Protected directory: {directoryPath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileSecurity] Failed to protect directory {directoryPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 保护单个文件（如 config.bin, state.json）
    /// DACL 规则：
    /// - SYSTEM: FullControl
    /// - Administrators: FullControl
    /// - CurrentUser: FullControl（如果当前用户是管理员）
    /// - 其他: Read（只读）
    /// </summary>
    public static bool ProtectFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"[FileSecurity] File not found: {filePath}");
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            var security = fileInfo.GetAccessControl();

            // 清空现有规则
            var rules = security.GetAccessRules(true, true, typeof(NTAccount));
            foreach (FileSystemAccessRule rule in rules)
            {
                security.RemoveAccessRule(rule);
            }

            // SYSTEM - FullControl
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                systemSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            // Administrators - FullControl
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                adminsSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            // Current User - ReadAndExecute
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.ReadAndExecute | FileSystemRights.ReadAttributes | FileSystemRights.ReadPermissions,
                    AccessControlType.Allow));
            }

            fileInfo.SetAccessControl(security);

            System.Diagnostics.Debug.WriteLine($"[FileSecurity] Protected file: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileSecurity] Failed to protect file {filePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 创建日志目录（ACL: SYSTEM/Admins 可读写，Users 只读）
    /// </summary>
    public static bool CreateLogsDirectory(string logsPath)
    {
        try
        {
            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
            }

            var directoryInfo = new DirectoryInfo(logsPath);
            var security = directoryInfo.GetAccessControl();

            // 清空现有规则
            var rules = security.GetAccessRules(true, true, typeof(NTAccount));
            foreach (FileSystemAccessRule rule in rules)
            {
                security.RemoveAccessRule(rule);
            }

            // SYSTEM - FullControl
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                systemSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            // Administrators - FullControl
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                adminsSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            // Users - Read（只能查看日志，不能删除）
            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                usersSid,
                FileSystemRights.ReadAndExecute | FileSystemRights.ReadAttributes | FileSystemRights.ReadPermissions,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            directoryInfo.SetAccessControl(security);

            System.Diagnostics.Debug.WriteLine($"[FileSecurity] Created logs directory: {logsPath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileSecurity] Failed to create logs directory {logsPath}: {ex.Message}");
            return false;
        }
    }
}
