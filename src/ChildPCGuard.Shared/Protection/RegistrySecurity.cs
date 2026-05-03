using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace ChildPCGuard.Shared.Protection;

/// <summary>
/// 注册表服务键 DACL 保护
/// 防止通过注册表修改服务配置或禁用服务
/// </summary>
public static class RegistrySecurity
{
    private const string ServicesKeyPath = @"SYSTEM\CurrentControlSet\Services";

    /// <summary>
    /// 保护指定服务的注册表键
    /// DACL 规则：
    /// - SYSTEM: FullControl
    /// - Administrators: ReadKey (仅读取，防止修改服务配置)
    /// - CurrentUser: ReadKey (仅读取)
    /// </summary>
    /// <param name="serviceName">服务名称（如 WinSecSvc_xxxx）</param>
    public static bool ProtectServiceKey(string serviceName)
    {
        try
        {
            var serviceKeyPath = $@"{ServicesKeyPath}\{serviceName}";

            using var serviceKey = Registry.LocalMachine.OpenSubKey(
                serviceKeyPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.TakeOwnership | RegistryRights.ChangePermissions);

            if (serviceKey == null)
            {
                System.Diagnostics.Debug.WriteLine($"[RegistrySecurity] Service key not found: {serviceKeyPath}");
                return false;
            }

            // 获取当前安全描述符
            var security = serviceKey.GetAccessControl();

            // 移除现有的继承规则
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // 清空现有规则
            var rules = security.GetAccessRules(true, true, typeof(NTAccount));
            foreach (RegistryAccessRule rule in rules)
            {
                security.RemoveAccessRule(rule);
            }

            // SYSTEM - FullControl
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var systemRule = new RegistryAccessRule(
                systemSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);
            security.AddAccessRule(systemRule);

            // Administrators - ReadKey（仅读取，防止修改服务配置）
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var adminsRule = new RegistryAccessRule(
                adminsSid,
                RegistryRights.ReadKey | RegistryRights.EnumerateSubKeys | RegistryRights.QueryValues,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);
            security.AddAccessRule(adminsRule);

            // Current User - ReadKey
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                var userRule = new RegistryAccessRule(
                    currentUser,
                    RegistryRights.ReadKey | RegistryRights.EnumerateSubKeys | RegistryRights.QueryValues,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow);
                security.AddAccessRule(userRule);
            }

            // 应用新的安全描述符
            serviceKey.SetAccessControl(security);

            System.Diagnostics.Debug.WriteLine($"[RegistrySecurity] Protected service key: {serviceName}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistrySecurity] Failed to protect service key {serviceName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 验证服务键 DACL 是否已正确设置（用于测试）
    /// </summary>
    public static bool VerifyServiceKeyDacl(string serviceName)
    {
        try
        {
            var serviceKeyPath = $@"{ServicesKeyPath}\{serviceName}";

            using var serviceKey = Registry.LocalMachine.OpenSubKey(
                serviceKeyPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ReadKey);

            if (serviceKey == null)
                return false;

            var security = serviceKey.GetAccessControl();
            var rules = security.GetAccessRules(true, true, typeof(NTAccount));

            // 检查 SYSTEM 是否有 FullControl
            bool systemHasFullControl = false;
            // 检查 Administrators 是否只有 ReadKey
            bool adminsReadOnly = true;

            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            foreach (RegistryAccessRule rule in rules)
            {
                if (rule.IdentityReference.Value.Equals("S-1-5-18") || // SYSTEM
                    rule.IdentityReference.Value.StartsWith("S-1-5-32-544")) // BUILTIN\Administrators
                {
                    if (rule.IdentityReference.Equals(systemSid))
                    {
                        if ((rule.RegistryRights & RegistryRights.FullControl) == RegistryRights.FullControl)
                            systemHasFullControl = true;
                    }
                    else if (rule.IdentityReference.Equals(adminsSid))
                    {
                        // Administrators 不应有 WriteKey 或 SetValue 权限
                        if ((rule.RegistryRights & (RegistryRights.WriteKey | RegistryRights.SetValue)) != 0)
                            adminsReadOnly = false;
                    }
                }
            }

            return systemHasFullControl && adminsReadOnly;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistrySecurity] Failed to verify service key DACL: {ex.Message}");
            return false;
        }
    }
}
