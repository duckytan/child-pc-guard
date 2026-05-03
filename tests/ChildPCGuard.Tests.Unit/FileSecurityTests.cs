using Xunit;
using System.IO;
using ChildPCGuard.Shared.Protection;

namespace ChildPCGuard.Tests.Unit;

/// <summary>
/// Phase 2: 文件和目录 ACL 保护测试
/// </summary>
public class FileSecurityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _testFile;

    public FileSecurityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ChildPCGuard_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _testFile = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(_testFile, "test content");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                // 恢复继承权限以便删除
                var dirInfo = new DirectoryInfo(_tempDir);
                var acl = dirInfo.GetAccessControl();
                acl.SetAccessRuleProtection(false, true);
                dirInfo.SetAccessControl(acl);

                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // 清理失败忽略
        }
    }

    [Fact(Skip = "需要管理员权限运行")]
    public void ProtectInstallDirectory_SetsDaclSuccessfully()
    {
        var result = FileSecurityManager.ProtectInstallDirectory(_tempDir);
        Assert.True(result);
    }

    [Fact(Skip = "需要管理员权限运行")]
    public void ProtectFile_SetsDaclSuccessfully()
    {
        var result = FileSecurityManager.ProtectFile(_testFile);
        Assert.True(result);
    }

    [Fact]
    public void ProtectFile_NonExistentFile_ReturnsFalse()
    {
        var result = FileSecurityManager.ProtectFile("C:\\nonexistent\\file.txt");
        Assert.False(result);
    }

    [Fact]
    public void ProtectDirectory_NonExistentDirectory_ReturnsFalse()
    {
        var result = FileSecurityManager.ProtectInstallDirectory("C:\\nonexistent\\dir");
        Assert.False(result);
    }

    [Fact(Skip = "需要管理员权限运行")]
    public void CreateLogsDirectory_CreatesAndProtects()
    {
        var logsPath = Path.Combine(_tempDir, "logs");

        var result = FileSecurityManager.CreateLogsDirectory(logsPath);

        Assert.True(result);
        Assert.True(Directory.Exists(logsPath));
    }

    [Fact(Skip = "需要管理员权限运行")]
    public void CreateLogsDirectory_AlreadyExists_Protects()
    {
        var logsPath = Path.Combine(_tempDir, "logs");
        Directory.CreateDirectory(logsPath);

        var result = FileSecurityManager.CreateLogsDirectory(logsPath);

        Assert.True(result);
    }
}
