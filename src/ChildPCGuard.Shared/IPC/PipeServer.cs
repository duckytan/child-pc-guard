using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChildPCGuard.Shared.IPC;

/// <summary>
/// Named Pipe 服务端（GuardService 端）
/// 监听来自 AdminPanel 的指令，通过 DACL 限制只允许本机 SYSTEM/Administrators 连接
/// </summary>
public class PipeServer : IDisposable
{
    public const string PipeName = "ChildPCGuard_Control";

    private readonly ILogger<PipeServer>? _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>收到消息时触发，返回响应消息</summary>
    public event Func<IpcMessage, Task<IpcMessage?>>? MessageReceived;

    public PipeServer(ILogger<PipeServer>? logger = null)
    {
        _logger = logger;
    }

    public void Start(CancellationToken hostCancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(hostCancellationToken);
        _listenTask = ListenLoopAsync(_cts.Token);
        _logger?.LogInformation("Named Pipe 服务端已启动: {PipeName}", PipeName);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipeSecurity = BuildPipeSecurity();
                using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                // 设置安全描述符
                var security = pipe.GetAccessControl();
                security.SetSecurityDescriptorBinaryForm(pipeSecurity.GetSecurityDescriptorBinaryForm());
                pipe.SetAccessControl(security);

                await pipe.WaitForConnectionAsync(ct);
                _ = HandleClientAsync(pipe, ct); // 不等待，继续接受下一个连接
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Named Pipe 监听出错，1 秒后重试");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(pipe, Encoding.UTF8);
            using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };

            var json = await reader.ReadLineAsync(ct);
            if (json is null) return;

            var message = JsonSerializer.Deserialize<IpcMessage>(json);
            if (message is null) return;

            _logger?.LogDebug("收到 IPC 指令: {Command}", message.Command);

            if (MessageReceived is not null)
            {
                var response = await MessageReceived(message);
                if (response is not null)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "处理 IPC 客户端连接时出错");
        }
    }

    /// <summary>构建 DACL：仅允许 SYSTEM 和 Administrators 完全控制</summary>
    private static PipeSecurity BuildPipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl, AccessControlType.Allow));
        return security;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
