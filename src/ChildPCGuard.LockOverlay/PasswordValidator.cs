using BCrypt.Net;

namespace ChildPCGuard.LockOverlay;

/// <summary>
/// 密码验证器 - BCrypt 哈希验证，错误次数限制
/// </summary>
public sealed class PasswordValidator
{
    private readonly string _passwordHash;
    private int _failedAttempts = 0;
    private DateTime? _lockedUntil = null;

    public PasswordValidator(string passwordHash)
    {
        _passwordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="password">用户输入的密码</param>
    /// <returns>验证结果</returns>
    public ValidationResult Validate(string password)
    {
        // 检查是否被锁定
        if (_lockedUntil.HasValue && DateTime.Now < _lockedUntil.Value)
        {
            return ValidationResult.Locked(_lockedUntil.Value);
        }

        // 清除过期的锁定状态
        if (_lockedUntil.HasValue && DateTime.Now >= _lockedUntil.Value)
        {
            _lockedUntil = null;
            _failedAttempts = 0;
        }

        // 验证密码
        if (BCrypt.Net.BCrypt.Verify(password, _passwordHash))
        {
            _failedAttempts = 0;
            _lockedUntil = null;
            return ValidationResult.Success();
        }

        // 密码错误
        _failedAttempts++;

        // 连续错误 3 次，锁定 5 分钟
        if (_failedAttempts >= 3)
        {
            _lockedUntil = DateTime.Now.AddMinutes(5);
            return ValidationResult.Locked(_lockedUntil.Value, _failedAttempts);
        }

        return ValidationResult.Failed(_failedAttempts, 3 - _failedAttempts);
    }

    /// <summary>
    /// 验证结果
    /// </summary>
    public sealed class ValidationResult
    {
        public bool IsValid { get; private set; }
        public bool IsLocked { get; private set; }
        public int? FailedAttempts { get; private set; }
        public int? RemainingAttempts { get; private set; }
        public DateTime? LockedUntil { get; private set; }

        private ValidationResult() { }

        public static ValidationResult Success()
        {
            return new ValidationResult { IsValid = true, IsLocked = false };
        }

        public static ValidationResult Failed(int attempts, int remaining)
        {
            return new ValidationResult
            {
                IsValid = false,
                IsLocked = false,
                FailedAttempts = attempts,
                RemainingAttempts = remaining
            };
        }

        public static ValidationResult Locked(DateTime until, int attempts = 3)
        {
            return new ValidationResult
            {
                IsValid = false,
                IsLocked = true,
                FailedAttempts = attempts,
                LockedUntil = until
            };
        }
    }
}
