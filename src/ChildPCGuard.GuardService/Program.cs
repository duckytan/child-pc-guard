using ChildPCGuard.GuardService;
using ChildPCGuard.Shared.Config;
using ChildPCGuard.Shared.IPC;
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
