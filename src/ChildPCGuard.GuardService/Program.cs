using ChildPCGuard.GuardService;
using ChildPCGuard.Shared.Config;
using ChildPCGuard.Shared.IPC;
using ChildPCGuard.Shared.Protection;
using ChildPCGuard.Shared.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// 控制台调试模式（开发时使用）
bool consoleMode = args.Contains("--console");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        @"C:\ProgramData\ChildPCGuard\logs\service-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("ChildPCGuard GuardService 正在启动...");

    // Phase 2: 初始化安全措施
    InitializeSecurity();

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog();
    builder.Services.AddSingleton<ConfigManager>();
    builder.Services.AddSingleton<StateManager>();
    builder.Services.AddSingleton<PipeServer>();
    builder.Services.AddHostedService<GuardWorker>();

    if (!consoleMode)
    {
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "WinSecSvc";
        });
    }

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GuardService 启动失败");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
}

/// <summary>
/// Phase 2: 初始化安全措施
/// 包括进程 DACL 保护、日志目录创建
/// </summary>
static void InitializeSecurity()
{
    try
    {
        // 1. 创建日志目录（如果不存在）
        var logsPath = @"C:\ProgramData\ChildPCGuard\logs";
        FileSecurityManager.CreateLogsDirectory(logsPath);

        // 2. 保护当前进程 DACL（防任务管理器 Kill）
        bool processProtected = ProcessSecurity.ProtectCurrentProcess();
        if (processProtected)
        {
            Log.Information("进程 DACL 保护已应用");
        }
        else
        {
            Log.Warning("进程 DACL 保护失败，可能无法防止任务管理器终止进程");
        }

        // 3. 保护服务注册表键（需要在服务安装后设置，此处仅验证）
        var serviceName = "WinSecSvc";
        var registryProtected = RegistrySecurity.VerifyServiceKeyDacl(serviceName);
        if (registryProtected)
        {
            Log.Information("服务注册表键 DACL 已正确保护");
        }
        else
        {
            Log.Warning("服务注册表键 DACL 可能未正确设置，建议检查 install.ps1");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "安全初始化部分失败，服务将继续运行");
    }
}
