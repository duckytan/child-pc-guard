using System.Security.Cryptography;

namespace ChildPCGuard.AdminPanel.Services;

/// <summary>
/// 密码验证器 - 支持 PBKDF2 哈希 + 重试锁定
/// </summary>
public class PasswordValidator
{
    private const int MaxAttempts = 5;
    private const int LockDurationMinutes = 5;
    private const int Pbkdf2Iterations = 10000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly string _storedHash;
    private readonly byte[] _storedSalt;
    private int _failedAttempts = 0;
    private DateTime? _lockedUntil = null;

    public PasswordValidator(string passwordHash)
    {
        // 假设存储的哈希格式为: base64(salt):base64(hash)
        var parts = passwordHash.Split(':');
        if (parts.Length != 2)
        {
            throw new ArgumentException("Invalid password hash format");
        }

        _storedSalt = Convert.FromBase64String(parts[0]);
        _storedHash = parts[1];
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    public ValidationResult Validate(string password)
    {
        // 检查锁定状态
        if (_lockedUntil.HasValue && DateTime.Now < _lockedUntil.Value)
        {
            return new ValidationResult
            {
                IsValid = false,
                IsLocked = true,
                LockedUntil = _lockedUntil.Value,
                RemainingAttempts = null
            };
        }

        // 验证密码
        var computedHash = ComputeHash(password, _storedSalt);
        var isValid = computedHash == _storedHash;

        if (isValid)
        {
            // 验证成功，重置计数器
            _failedAttempts = 0;
            _lockedUntil = null;
            return new ValidationResult
            {
                IsValid = true,
                IsLocked = false,
                RemainingAttempts = MaxAttempts
            };
        }
        else
        {
            // 验证失败
            _failedAttempts++;
            var remainingAttempts = MaxAttempts - _failedAttempts;

            if (_failedAttempts >= MaxAttempts)
            {
                // 达到最大尝试次数，锁定
                _lockedUntil = DateTime.Now.AddMinutes(LockDurationMinutes);
                return new ValidationResult
                {
                    IsValid = false,
                    IsLocked = true,
                    LockedUntil = _lockedUntil.Value,
                    RemainingAttempts = 0
                };
            }
            else
            {
                return new ValidationResult
                {
                    IsValid = false,
                    IsLocked = false,
                    RemainingAttempts = remainingAttempts
                };
            }
        }
    }

    /// <summary>
    /// 计算密码哈希（静态方法用于生成初始哈希）
    /// </summary>
    public static string ComputeHash(string password, byte[]? salt = null)
    {
        if (salt == null || salt.Length != SaltSize)
        {
            using var rng = RandomNumberGenerator.Create();
            salt = new byte[SaltSize];
            rng.GetBytes(salt);
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(HashSize);

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// 生成密码哈希（包含盐）
    /// </summary>
    public static string GeneratePasswordHash(string password)
    {
        var salt = new byte[SaltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        var hash = ComputeHash(password, salt);
        return $"{Convert.ToBase64String(salt)}:{hash}";
    }
}

/// <summary>
/// 密码验证结果
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedUntil { get; set; }
    public int? RemainingAttempts { get; set; }
}
