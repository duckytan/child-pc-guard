using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ChildPCGuard.Shared.IPC;

/// <summary>
/// Named Pipe 客户端（AdminPanel 端）
/// </summary>
public class PipeClient
{
    private const int ConnectTimeoutMs = 3000;

    /// <summary>发送指令并等待响应，超时返回 null</summary>
    public static async Task<IpcMessage?> SendAsync(IpcMessage message, CancellationToken ct = default)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeServer.PipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous);

            await pipe.ConnectAsync(ConnectTimeoutMs, ct);

            using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8);

            await writer.WriteLineAsync(JsonSerializer.Serialize(message));

            var responseJson = await reader.ReadLineAsync(ct);
            return responseJson is null ? null : JsonSerializer.Deserialize<IpcMessage>(responseJson);
        }
        catch (TimeoutException)
        {
            return null; // 服务未运行
        }
        catch (Exception)
        {
            return null;
        }
    }
}
