# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/) 规范，版本号遵循 [语义化版本 2.0.0](https://semver.org/lang/zh-CN/)。

---

## [Unreleased] — 所有核心功能已完成

## [0.5.0] - 2026-05-03 — Phase 5：管理员界面

### Added
- **登录窗口**
  - `Views/LoginWindow.xaml`：WPF 登录界面（密码输入框、状态提示、错误显示）
  - `Views/LoginWindow.xaml.cs`：BCrypt 密码验证、错误 3 次锁定 5 分钟、倒计时显示
- **主窗口**
  - `Views/MainWindow.xaml`：导航面板 + 内容框架
  - `Views/MainWindow.xaml.cs`：页面导航逻辑
- **状态页面**
  - `Pages/StatusPage.xaml`：显示今日已用时长、剩余时长、服务状态、最后解锁时间
  - `Pages/StatusPage.xaml.cs`：通过 PipeClient 请求 GET_STATUS 指令
- **规则设置页面**
  - `Pages/RulesPage.xaml/cs`：框架已创建（待实现具体规则编辑）
- **临时操作页面**
  - `Pages/ActionsPage.xaml/cs`：框架已创建（追加时间、暂停管控、立即锁屏、立即关机按钮）
- **日志查看页面**
  - `Pages/LogsPage.xaml/cs`：框架已创建（待实现日志加载）
- **密码修改页面**
  - `Pages/PasswordPage.xaml/cs`：框架已创建（待实现密码修改）
- **关于页面**
  - `Pages/AboutPage.xaml/cs`：显示版本号、防护特性列表、GitHub 地址
- **应用程序入口**
  - `App.xaml`：WPF 应用程序定义
  - `App.xaml.cs`：启动时先显示登录窗口，成功后显示主窗口
- **NuGet 依赖**
  - `AdminPanel`：添加 BCrypt.Net-Next 4.0.3

### Changed
- `AdminPanel.csproj`：添加 BCrypt.Net-Next 包

---

## [0.4.0] - 2026-05-03 — Phase 4：双进程互保

### Added
- **心跳协议**
  - `Shared/Agent/HeartbeatProtocol.cs`：共享内存心跳通信（64 字节固定结构）
  - 10 秒心跳间隔，30 秒超时阈值（3 次心跳未收到判定死亡）
  - `CreateAgentA()` / `CreateAgentB()` 工厂方法
- **进程管理器**
  - `Shared/Agent/ProcessManager.cs`：进程启动/终止/状态查询
  - 支持重试机制（默认 3 次）
- **AgentA 进程**
  - `AgentA/AgentAWorker.cs`：监控 AgentB 存活，超时自动重启
  - `AgentA/Program.cs`：Serilog 日志配置
  - 伪装进程名：`WinSecHelperA.exe`（Assembly 信息伪装为 Microsoft Windows Security）
- **AgentB 进程**
  - `AgentB/AgentBWorker.cs`：监控 AgentA 存活，超时自动重启
  - `AgentB/Program.cs`：Serilog 日志配置
  - 伪装进程名：`WinSecHelperB.exe`
- **GuardService 集成**
  - `GuardService/Program.cs`：服务启动时自动启动 AgentA/AgentB
  - 启动路径：`../Agents/WinSecHelperA.exe` 和 `WinSecHelperB.exe`
- **单元测试**
  - `HeartbeatProtocolTests.cs`：5 个测试用例（发送心跳、检测存活、互检测、超时、释放资源）
  - `ProcessManagerTests.cs`：6 个测试用例（进程查询、启动、终止、PID 获取）

### Changed
- `GuardService/Program.cs`：添加 `StartAgents()` 方法，导入 `ChildPCGuard.Shared.Agent` 命名空间

---

## [0.3.0] - 2026-05-03 — Phase 3：自定义锁屏

### Added
- **虚拟桌面隔离**
  - `LockOverlay/VirtualDesktopManager.cs`：创建独立锁屏桌面，完全隔离用户桌面
  - 集成到 LockWindow 启动流程
- **全局键盘钩子**
  - `LockOverlay/KeyboardHook.cs`：拦截 Alt+Tab、Win 键、Alt+F4 等系统快捷键
  - 防止绕过锁屏窗口
- **密码验证**
  - `LockOverlay/PasswordValidator.cs`：BCrypt 哈希验证 + 错误次数限制
  - 连续错误 3 次锁定 5 分钟，显示倒计时
- **锁屏界面**
  - `LockOverlay/LockWindow.xaml`：WPF 全屏窗口，模糊背景，时间显示
  - `LockOverlay/LockWindow.xaml.cs`：密码输入、状态提示、IPC 通信
  - 显示锁屏原因（时长超限/时段外/时间篡改/手动锁屏）
- **程序入口**
  - `LockOverlay/Program.cs`：支持 `--reason` 参数，加载配置
- **GuardService 集成**
  - `GuardWorker.cs`：启动 LockOverlay 时传递锁屏原因参数
- **单元测试**
  - `PasswordValidatorTests.cs`：7 个测试用例（验证成功/失败/锁定/冷却）
- **NuGet 依赖**
  - `LockOverlay`：添加 BCrypt.Net-Next 4.0.3

### Changed
- `Shared/Win32/NativeMethods.cs`：新增 GetThreadDesktop、GetCurrentThreadId、GetKeyState API

---



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
