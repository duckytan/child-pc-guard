using System.Security.Cryptography;
using BCrypt.Net;

namespace ChildPCGuard.AdminPanel.Services;

/// <summary>
/// 密码验证器 - 支持 BCrypt 哈希 + 重试锁定
/// </summary>
public class PasswordValidator
{
    private const int MaxAttempts = 5;
    private const int LockDurationMinutes = 5;

    private readonly string _storedHash;
    private int _failedAttempts = 0;
    private DateTime? _lockedUntil = null;

    public PasswordValidator(string passwordHash)
    {
        _storedHash = passwordHash;
    }

    /// <summary>
    /// 检查当前锁定状态
    /// </summary>
    public (bool IsLocked, int RemainingSeconds) CheckLockStatus()
    {
        if (_lockedUntil.HasValue)
        {
            var now = DateTime.UtcNow;
            if (now < _lockedUntil.Value)
            {
                var remainingSeconds = (int)(_lockedUntil.Value - now).TotalSeconds;
                return (true, remainingSeconds);
            }
            else
            {
                // 锁定已过期
                _lockedUntil = null;
                _failedAttempts = 0;
            }
        }

        return (false, 0);
    }

    /// <summary>
    /// 验证密码（异步接口，实际同步执行）
    /// </summary>
    public async Task<(bool IsValid, int Remaining)> VerifyPasswordAsync(string password)
    {
        // 模拟异步延迟
        await Task.Delay(100);

        // 检查锁定状态
        var (isLocked, _) = CheckLockStatus();
        if (isLocked)
        {
            return (false, 0);
        }

        // 验证密码（使用 BCrypt）
        var isValid = BCrypt.Net.BCrypt.Verify(password, _storedHash);

        if (isValid)
        {
            // 验证成功，重置计数器
            _failedAttempts = 0;
            _lockedUntil = null;
            return (true, MaxAttempts);
        }
        else
        {
            // 验证失败
            _failedAttempts++;
            var remainingAttempts = MaxAttempts - _failedAttempts;

            if (_failedAttempts >= MaxAttempts)
            {
                // 达到最大尝试次数，锁定
                _lockedUntil = DateTime.UtcNow.AddMinutes(LockDurationMinutes);
                return (false, 0);
            }
            else
            {
                return (false, remainingAttempts);
            }
        }
    }

    /// <summary>
    /// 获取剩余尝试次数
    /// </summary>
    public int GetRemainingAttempts()
    {
        return Math.Max(0, MaxAttempts - _failedAttempts);
    }

    /// <summary>
    /// 生成密码哈希（包含盐）
    /// </summary>
    public static string GeneratePasswordHash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
}
