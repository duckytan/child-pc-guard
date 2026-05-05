using Xunit;
using ChildPCGuard.LockOverlay;
using BCrypt.Net;

namespace ChildPCGuard.Tests.Unit;

/// <summary>
/// Phase 3: 密码验证器测试
/// </summary>
public class PasswordValidatorTests
{
    private const string TestPassword = "TestPassword123";
    private readonly string _passwordHash;

    public PasswordValidatorTests()
    {
        _passwordHash = BCrypt.HashPassword(TestPassword);
    }

    [Fact]
    public void Validate_CorrectPassword_ReturnsSuccess()
    {
        var validator = new PasswordValidator(_passwordHash);
        var result = validator.Validate(TestPassword);

        Assert.True(result.IsValid);
        Assert.False(result.IsLocked);
        Assert.Null(result.FailedAttempts);
    }

    [Fact]
    public void Validate_WrongPassword_ReturnsFailed()
    {
        var validator = new PasswordValidator(_passwordHash);
        var result = validator.Validate("WrongPassword");

        Assert.False(result.IsValid);
        Assert.False(result.IsLocked);
        Assert.Equal(1, result.FailedAttempts);
        Assert.Equal(2, result.RemainingAttempts);
    }

    [Fact]
    public void Validate_TwoWrongAttempts_ReturnsOneRemaining()
    {
        var validator = new PasswordValidator(_passwordHash);

        validator.Validate("Wrong1");
        var result = validator.Validate("Wrong2");

        Assert.False(result.IsValid);
        Assert.Equal(2, result.FailedAttempts);
        Assert.Equal(1, result.RemainingAttempts);
    }

    [Fact]
    public void Validate_ThreeWrongAttempts_LocksFor5Minutes()
    {
        var validator = new PasswordValidator(_passwordHash);

        validator.Validate("Wrong1");
        validator.Validate("Wrong2");
        var result = validator.Validate("Wrong3");

        Assert.False(result.IsValid);
        Assert.True(result.IsLocked);
        Assert.NotNull(result.LockedUntil);
        Assert.True(result.LockedUntil.Value > DateTime.Now);
        Assert.True(result.LockedUntil.Value <= DateTime.Now.AddMinutes(5.1));
    }

    [Fact]
    public async Task Validate_LockedDuringCooldown_ReturnsLocked()
    {
        var validator = new PasswordValidator(_passwordHash);

        // 触发锁定
        validator.Validate("Wrong1");
        validator.Validate("Wrong2");
        validator.Validate("Wrong3");

        // 尝试验证（在锁定期间）
        var result = validator.Validate(TestPassword);

        Assert.False(result.IsValid);
        Assert.True(result.IsLocked);
    }

    [Fact]
    public async Task Validate_AfterCooldownUnlock_AllowsCorrectPassword()
    {
        var validator = new PasswordValidator(_passwordHash);

        // 触发锁定
        validator.Validate("Wrong1");
        validator.Validate("Wrong2");
        validator.Validate("Wrong3");

        // 等待锁定过期（模拟：直接修改内部状态）
        // 实际测试中需要等待，这里用 reflection 模拟
        var lockedUntil = DateTime.Now.AddSeconds(-1);
        var validatorType = validator.GetType();
        var lockedUntilField = validatorType.GetField("_lockedUntil",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        lockedUntilField?.SetValue(validator, lockedUntil);

        // 再次验证
        var result = validator.Validate(TestPassword);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyPassword_ReturnsFailed()
    {
        var validator = new PasswordValidator(_passwordHash);
        var result = validator.Validate("");

        Assert.False(result.IsValid);
        Assert.Equal(1, result.FailedAttempts);
    }
}
