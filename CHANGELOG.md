# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/) 规范，版本号遵循 [语义化版本 2.0.0](https://semver.org/lang/zh-CN/)。

---

## [Unreleased] — Phase 1 开发中

### Added
- 解决方案结构：`ChildPCGuard.sln` + 6 个子项目（Shared/GuardService/LockOverlay/AdminPanel/AgentA/AgentB）+ 单元测试项目
- `ChildPCGuard.Shared`：共享类库
  - `Config/AppConfig.cs`：完整配置数据模型（含 TimeRules、DayRule、TimeWindow）
  - `Config/ConfigManager.cs`：AES-256-GCM 加密配置文件管理器，密钥通过 PBKDF2 从 MachineGuid 派生
  - `State/DailyState.cs`：今日状态数据模型（跨天重置、额外时间、连续使用计时）
  - `State/StateManager.cs`：state.json 读写（带写锁、原子替换）
  - `IPC/IpcMessage.cs`：Named Pipe JSON 消息协议（9 种指令类型）
  - `IPC/PipeServer.cs`：Named Pipe 服务端（DACL 限制只允许 SYSTEM/Admins）
  - `IPC/PipeClient.cs`：Named Pipe 客户端
  - `Win32/NativeMethods.cs`：Win32 API P/Invoke 声明（GetLastInputInfo、CreateDesktop、SetWindowsHookEx 等）
  - `Protection/NtpTimeValidator.cs`：NTP 时间校验（多服务器、5 分钟缓存）
  - `Protection/SafeModeDetector.cs`：安全模式检测（检测到则立即关机）
- `ChildPCGuard.GuardService`：核心守护服务
  - `Core/TimeTracker.cs`：基于 GetLastInputInfo 的精准计时（空闲不计时、跨天重置）
  - `Core/RuleEngine.cs`：规则引擎（时长/时段/NTP/连续使用判断）
  - `Core/ShutdownScheduler.cs`：关机调度器（内部定时器 + 60 秒预警）
  - `Core/NotificationHelper.cs`：Windows 10/11 原生 Toast 通知
  - `GuardWorker.cs`：主 Worker（5 秒主循环、IPC 处理、锁屏管理）
  - `Program.cs`：服务入口（支持 `--console` 调试模式）
- `scripts/install.ps1`：安装脚本（目录创建、ACL/DACL 配置、服务注册、故障恢复、计划任务）
- `scripts/uninstall.ps1`：卸载脚本（服务删除、计划任务删除、文件清理）
- `tests/ChildPCGuard.Tests.Unit`：单元测试项目
  - `RuleEngineTests.cs`：12 个测试用例（时长/时段/NTP/连续使用/预警判断）
  - `DailyStateTests.cs`：5 个测试用例（跨天重置、有效上限计算）
  - `TimeWindowTests.cs`：8 个测试用例（时间窗口含义，含跨午夜场景）

---



## [0.1.0] - 待发布（Phase 1 完成后）

首个可用版本（MVP）。

---

## 文档历史

| 日期 | 变更内容 |
|------|----------|
| 2026-05-03 | 初始化项目文档结构，完成方案设计，确定所有技术决策 |
| 2026-05-03 | 创建 CODEBUDDY.md、task_plan.md、README.md |
| 2026-05-03 | 补充工程文档：SRS、测试计划、验收标准、安全设计、贡献指南 |

---

<!-- 版本链接（代码发布后补充） -->
[Unreleased]: https://github.com/duckytan/child-pc-guard/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/duckytan/child-pc-guard/releases/tag/v0.1.0
