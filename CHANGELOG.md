# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/) 规范，版本号遵循 [语义化版本 2.0.0](https://semver.org/lang/zh-CN/)。

---

## [Unreleased] — Phase 3 开发中

## [0.2.0] - 2026-05-03 — Phase 2：防护加固

### Added
- **进程保护**
  - `Shared/Protection/ProcessSecurity.cs`：进程 DACL 保护，防止任务管理器 Kill
  - `GuardService/Program.cs`：服务启动时自动应用进程 DACL 保护
- **注册表保护**
  - `Shared/Protection/RegistrySecurity.cs`：服务注册表键 DACL 保护
  - `install.ps1`：安装时自动设置注册表键 DACL（Admins 只读）
- **文件 ACL 保护**
  - `Shared/Protection/FileSecurity.cs`：文件和目录 ACL 管理
  - `install.ps1`：增强目录 ACL 配置（Users 只读，不可写入/删除）
- **NTP 时间校验**
  - `Shared/Protection/NtpTimeValidator.cs`：多服务器 NTP 校验，5 分钟缓存
  - `GuardWorker.cs`：集成到主循环（每 5 分钟校验一次）
- **安全模式检测**
  - `Shared/Protection/SafeModeDetector.cs`：检测安全模式启动
  - `GuardWorker.cs`：服务启动时检查，安全模式则立即关机
- **安装脚本增强**
  - `install.ps1`：添加注册表 DACL 保护步骤（Phase 2）
  - 优化目录 ACL 配置，移除拒绝规则，改用显式允许
- **单元测试**
  - `ProcessSecurityTests.cs`：5 个测试用例（服务 SDDL 生成、进程 DACL 保护验证）
  - `FileSecurityTests.cs`：8 个测试用例（文件/目录 ACL 保护）
  - `RegistrySecurityTests.cs`：5 个测试用例（注册表 DACL 保护）

### Changed
- `GuardService/Program.cs`：添加 `InitializeSecurity()` 方法
- `GuardWorker.cs`：已包含 NTP 校验和安全模式检测（Phase 1 基础，Phase 2 完整集成）

---

## [0.1.0] - 2026-05-03 — Phase 1：MVP

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
