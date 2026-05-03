using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChildPCGuard.Shared.Config;

/// <summary>
/// 配置文件管理器：负责 config.bin 的 AES-256-GCM 加解密读写
/// 密钥通过 PBKDF2 从机器唯一标识（MachineGuid）派生，不直接存储密钥
/// </summary>
public class ConfigManager
{
    private const string ConfigFileName = "config.bin";
    private const string DataDirectory = @"C:\ProgramData\ChildPCGuard";
    private const int Pbkdf2Iterations = 100_000;
    private const int KeySizeBytes = 32; // AES-256
    private const int NonceSizeBytes = 12; // GCM 标准 nonce 大小
    private const int TagSizeBytes = 16;   // GCM 认证标签大小

    private readonly string _configPath;
    private readonly ILogger<ConfigManager>? _logger;

    public ConfigManager(ILogger<ConfigManager>? logger = null)
    {
        _configPath = Path.Combine(DataDirectory, ConfigFileName);
        _logger = logger;
    }

    /// <summary>从 config.bin 读取并解密配置</summary>
    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            _logger?.LogWarning("配置文件不存在，返回默认配置: {Path}", _configPath);
            return new AppConfig();
        }

        try
        {
            var cipherData = File.ReadAllBytes(_configPath);
            var plainJson = Decrypt(cipherData, DeriveKey());
            return JsonSerializer.Deserialize<AppConfig>(plainJson) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "配置文件解密失败，返回默认配置");
            return new AppConfig();
        }
    }

    /// <summary>加密并写入 config.bin</summary>
    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(DataDirectory);

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = false });
        var cipherData = Encrypt(json, DeriveKey());

        // 写入临时文件后原子替换，避免写入中断导致配置损坏
        var tempPath = _configPath + ".tmp";
        File.WriteAllBytes(tempPath, cipherData);
        File.Move(tempPath, _configPath, overwrite: true);

        _logger?.LogInformation("配置已保存: {Path}", _configPath);
    }

    /// <summary>
    /// 从机器唯一标识（MachineGuid）通过 PBKDF2 派生 AES 密钥
    /// 不直接存储密钥，同一台机器派生结果一致
    /// </summary>
    private static byte[] DeriveKey()
    {
        var machineId = GetMachineId();
        // 固定 salt：与项目绑定，防止彩虹表
        var salt = Encoding.UTF8.GetBytes("ChildPCGuard-v1-AES256GCM-Salt-2026");
        using var pbkdf2 = new Rfc2898DeriveBytes(machineId, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySizeBytes);
    }

    /// <summary>读取机器 GUID 作为密钥派生材料</summary>
    private static string GetMachineId()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography", false);
            return key?.GetValue("MachineGuid")?.ToString() ?? "fallback-machine-id";
        }
        catch
        {
            return "fallback-machine-id";
        }
    }

    private static byte[] Encrypt(string plainText, byte[] key)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // 存储格式：[nonce(12)] + [tag(16)] + [ciphertext]
        var result = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);
        return result;
    }

    private static string Decrypt(byte[] data, byte[] key)
    {
        if (data.Length < NonceSizeBytes + TagSizeBytes)
            throw new CryptographicException("配置文件数据长度不合法");

        var nonce = data[..NonceSizeBytes];
        var tag = data[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        var cipherBytes = data[(NonceSizeBytes + TagSizeBytes)..];
        var plainBytes = new byte[cipherBytes.Length];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
