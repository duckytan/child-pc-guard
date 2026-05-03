using Xunit;
using Microsoft.Win32;
using ChildPCGuard.Shared.Protection;

namespace ChildPCGuard.Tests.Unit;

/// <summary>
/// Phase 2: 注册表服务键 DACL 保护测试
/// </summary>
public class RegistrySecurityTests : IDisposable
{
    private const string TestServiceName = "TestChildPCGuard_";
    private readonly string _testServiceName;

    public RegistrySecurityTests()
    {
        _testServiceName = $"{TestServiceName}{Guid.NewGuid():N}";

        // 创建测试服务键（模拟真实服务）
        using var servicesKey = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services", true);

        servicesKey?.CreateSubKey(_testServiceName);
    }

    public void Dispose()
    {
        try
        {
            // 清理测试服务键
            var serviceKeyPath = $@"SYSTEM\CurrentControlSet\Services\{_testServiceName}";
            Registry.LocalMachine.DeleteSubKeyTree(serviceKeyPath);
        }
        catch
        {
            // 清理失败忽略
        }
    }

    [Fact(Skip = "需要管理员权限运行")]
    public void ProtectServiceKey_SetsDaclSuccessfully()
    {
        var result = RegistrySecurity.ProtectServiceKey(_testServiceName);
        Assert.True(result);
    }

    [Fact(Skip = "需要管理员权限运行")]
    public void ProtectServiceKey_NonExistentService_ReturnsFalse()
    {
        var result = RegistrySecurity.ProtectServiceKey("NonExistentService_12345");
        Assert.False(result);
    }

    [Fact(Skip = "需要管理员权限运行")]
    public void VerifyServiceKeyDacl_ReturnsTrueWhenProtected()
    {
        // 先保护服务键
        RegistrySecurity.ProtectServiceKey(_testServiceName);

        // 验证 DACL
        var verified = RegistrySecurity.VerifyServiceKeyDacl(_testServiceName);
        Assert.True(verified);
    }

    [Fact]
    public void VerifyServiceKeyDacl_NonExistentService_ReturnsFalse()
    {
        var verified = RegistrySecurity.VerifyServiceKeyDacl("NonExistentService_67890");
        Assert.False(verified);
    }

    [Fact]
    public void VerifyServiceKeyDacl_UnprotectedService_ReturnsFalse()
    {
        // 未保护的服务键默认验证应返回 false
        var verified = RegistrySecurity.VerifyServiceKeyDacl(_testServiceName);
        // 默认系统 DACL 可能不满足我们的严格规则（Admins 只读）
        Assert.False(verified);
    }
}
