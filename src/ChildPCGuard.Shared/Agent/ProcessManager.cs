using Serilog;
using System.Diagnostics;

namespace ChildPCGuard.Shared.Agent;

/// <summary>
/// 进程管理器（用于重启 Agent 进程）
/// </summary>
public sealed class ProcessManager
{
    private readonly ILogger _logger;

    public ProcessManager(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查进程是否存在
    /// </summary>
    public bool IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
        return processes.Length > 0;
    }

    /// <summary>
    /// 启动进程（带重试）
    /// </summary>
    /// <param name="executablePath">可执行文件完整路径</param>
    /// <param name="maxRetries">最大重试次数</param>
    /// <returns>启动成功返回进程对象，否则返回 null</returns>
    public Process? StartProcess(string executablePath, int maxRetries = 3)
    {
        if (!File.Exists(executablePath))
        {
            _logger.Error("可执行文件不存在: {Path}", executablePath);
            return null;
        }

        Process? process = null;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                if (process != null && !process.HasExited)
                {
                    _logger.Information("进程已启动: {Path}, PID: {Pid}",
                        executablePath, process.Id);
                    return process;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "启动进程失败（尝试 {Attempt}/{MaxRetries}）: {Path}",
                    i + 1, maxRetries, executablePath);

                if (i < maxRetries - 1)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        _logger.Error("启动进程失败（已重试 {MaxRetries} 次）: {Path}",
            maxRetries, executablePath);
        return null;
    }

    /// <summary>
    /// 终止进程（强制）
    /// </summary>
    public bool TerminateProcess(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
            bool success = false;

            foreach (var p in processes)
            {
                try
                {
                    p.Kill(true);
                    p.WaitForExit(5000);
                    success = true;
                    _logger.Information("进程已终止: {Name}, PID: {Pid}", processName, p.Id);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "终止进程失败: {Name}, PID: {Pid}", processName, p.Id);
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "终止进程异常: {Name}", processName);
            return false;
        }
    }

    /// <summary>
    /// 获取进程 PID
    /// </summary>
    public int? GetProcessId(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
            return processes.Length > 0 ? processes[0].Id : null;
        }
        catch
        {
            return null;
        }
    }
}
