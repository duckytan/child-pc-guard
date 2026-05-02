# CODEBUDDY.md This file provides guidance to CodeBuddy when working with code in this repository.

## ⚠️ 铁律（最高优先级，不可违反）

1. **禁止提前开发**：在用户明确说出"开始开发"之前，始终保持讨论/方案阶段。不得生成任何项目源代码文件，不得创建 `src/` 目录或任何 `.cs`、`.xaml`、`.csproj`、`.sln` 等开发文件。
2. **强制推送 GitHub**：每次对话结束，或每次修改/新增任何文件后，必须立即执行以下命令推送到远程仓库：
   ```powershell
   cd "c:/Users/Administrator/CodeBuddy/20260502215151/project/child-pc-guard"
   git add -A
   git commit -m "docs: update"
   git push origin main
   ```
   远程仓库地址：`https://github.com/duckytan/child-pc-guard.git`

---

## 项目概述

**ChildPCGuard** — Windows 10/11 儿童电脑使用时间管控程序。核心亮点：企业级自我保护能力（12 层防护），对抗有一定电脑基础的初中生。

关键文档（按用途查阅）：

| 文档 | 用途 |
|------|------|
| `ChildPCGuard_项目方案.md` | 唯一完整技术参考，开发前必读 |
| `docs/REQUIREMENTS.md` | 需求规格（SRS），39 条功能需求 + MoSCoW 优先级 |
| `docs/ACCEPTANCE_CRITERIA.md` | 各 Phase DoD，每个 Phase 完成后对照验收 |
| `docs/TEST_PLAN.md` | 测试用例（单元/集成/功能/渗透），缺陷 P0~P3 流程 |
| `docs/SECURITY.md` | 安全设计：威胁建模、加密方案、IPC 安全 |
| `docs/CONTRIBUTING.md` | 代码规范、分支策略、Commit 规范 |
| `task_plan.md` | 当前开发进度，任务勾选状态 |

---

## 开发环境与构建命令

### 前提条件
- Visual Studio 2022 或 JetBrains Rider
- .NET 8 SDK
- Windows 10/11，调试服务需管理员权限

### 常用命令

**构建整个解决方案**
```powershell
dotnet build ChildPCGuard.sln
```

**运行所有测试**
```powershell
dotnet test tests/ChildPCGuard.Tests.Unit/
```

**运行单个测试类或方法**
```powershell
dotnet test tests/ChildPCGuard.Tests.Unit/ --filter "FullyQualifiedName~TimeTrackerTests"
dotnet test tests/ChildPCGuard.Tests.Unit/ --filter "FullyQualifiedName~TimeTrackerTests.GetElapsedToday_ExcludesIdleTime"
```

**以控制台模式调试 GuardService（无需注册服务）**
```powershell
dotnet run --project src/ChildPCGuard.GuardService -- --console
```

**发布（自包含，Windows x64）**
```powershell
dotnet publish src/GuardService -c Release -r win-x64 --self-contained true -o ./publish/GuardService
dotnet publish src/LockOverlay  -c Release -r win-x64 --self-contained true -o ./publish/LockOverlay
dotnet publish src/AdminPanel   -c Release -r win-x64 --self-contained true -o ./publish/AdminPanel
dotnet publish src/AgentA       -c Release -r win-x64 --self-contained true -o ./publish/AgentA
dotnet publish src/AgentB       -c Release -r win-x64 --self-contained true -o ./publish/AgentB
```

**安装程序（管理员 PowerShell）**
```powershell
.\scripts\install.ps1
```

**卸载程序（管理员 PowerShell，需输入管理密码）**
```powershell
.\scripts\uninstall.ps1
```

**以服务方式调试 GuardService（管理员 PowerShell）**
```powershell
# 注册临时服务用于调试
sc.exe create ChildPCGuardDebug binPath="C:\path\to\GuardService.exe" start=demand
sc.exe start ChildPCGuardDebug
sc.exe stop  ChildPCGuardDebug
sc.exe delete ChildPCGuardDebug
```

**查看服务状态**
```powershell
sc.exe query ChildPCGuard
Get-Service -Name "ChildPCGuard*"
```

**查看实时日志**
```powershell
Get-Content "C:\ProgramData\ChildPCGuard\logs\usage-$(Get-Date -f yyyy-MM-dd).log" -Wait
```

---

## 架构概览

项目由 **5 个可执行文件** + **1 个共享库** 组成，解决方案文件为 `ChildPCGuard.sln`。

### 进程角色

| 进程 | 权限 | 是否常驻 | 职责 |
|------|------|----------|------|
| `GuardService.exe` | SYSTEM | 是（Windows 服务） | 核心调度：计时、规则判断、关机调度、NTP 校验、发 Toast 通知、监控 LockOverlay/AgentA/AgentB |
| `AgentA.exe` | SYSTEM | 是 | 双进程互保，监控 AgentB，被杀后由 AgentB 复活 |
| `AgentB.exe` | SYSTEM | 是 | 双进程互保，监控 AgentA，被杀后由 AgentA 复活 |
| `LockOverlay.exe` | SYSTEM | 仅锁屏时 | 全屏锁屏覆盖层，处理密码验证，验证通过后通过 Named Pipe 通知 GuardService 解锁 |
| `AdminPanel.exe` | 当前用户 | 家长主动打开 | 图形化管理界面，通过 Named Pipe 与 GuardService 通信 |

### 数据流向

```
开机
 └─ GuardService（SYSTEM 服务）启动
     ├─ 读取 C:\ProgramData\ChildPCGuard\config.bin（AES-256 解密）
     ├─ 读取 C:\ProgramData\ChildPCGuard\state.json（恢复今日已用时长）
     ├─ 启动 AgentA、AgentB（双进程互保）
     └─ 主循环（每 5 秒）
          ├─ TimeTracker：调用 GetLastInputInfo，统计活跃使用时长
          ├─ RuleEngine：判断是否需要锁屏（时长/时段/NTP篡改）
          ├─ ShutdownScheduler：检查是否到 22:00
          ├─ NotificationHelper：发送 10/5/1 分钟 Toast 预警
          └─ 持久化 state.json

锁屏时：GuardService 启动 LockOverlay → 虚拟桌面隔离 → 等待密码
解锁时：LockOverlay → Named Pipe → GuardService → 销毁 LockOverlay
管理时：AdminPanel → Named Pipe → GuardService（热更新配置/追加时间/立即锁屏等）
```

### 共享库 `src/Shared/`

所有项目均引用 `Shared` 类库，内含：

- **`Config/`**：`AppConfig`（配置数据模型）、`ConfigManager`（AES-256 加解密读写）
- **`State/`**：`DailyState`（今日状态模型）、`StateManager`（state.json 读写）
- **`IPC/`**：`PipeServer`/`PipeClient`（Named Pipe 封装）、`IpcMessage`（JSON 消息协议）
- **`Protection/`**：`ServiceProtector`、`RegistryProtector`、`FileProtector`、`ProcessAccessControl`、`NtpTimeValidator`、`SafeModeDetector`、`PEDetector`、`VirtualMachineDetector`
- **`Logging/`**：`Logger`（Serilog 封装，写入 `C:\ProgramData\ChildPCGuard\logs\`）

### 关键设计决策

1. **虚拟桌面锁屏（优先方案）**：`LockOverlay` 调用 `CreateDesktop` + `SwitchDesktop`，将用户完全隔离到新桌面，Ctrl+Alt+Del 无法切回原桌面。备选：WPF 全屏 `Topmost` 窗口 + `SetWindowsHookEx(WH_KEYBOARD_LL)` 拦截快捷键。

2. **双重关机保障**：GuardService 内部定时器检测 22:00，同时 `install.ps1` 创建 Windows 任务计划程序任务（`ChildPCGuard_AutoShutdown`），两者互为备份。

3. **配置安全**：`config.bin` 使用 **AES-256-GCM** 加密（提供加密 + 完整性验证），密钥通过 PBKDF2 从机器唯一标识派生，不直接存储密钥。密码使用 BCrypt（cost factor 12）不可逆哈希，明文密码不落盘。详见 `docs/SECURITY.md`。

4. **12 层防护核心**：SYSTEM 权限 > 服务 DACL（禁 `sc stop`）> 进程句柄 DACL（禁 Kill）> 双进程互保 > SCM 3 次自动重启 > 文件/注册表 ACL > NTP 时间校验 > 安全模式检测 > 配置 AES 加密。**前提**：孩子 Windows 账户必须是标准用户，这是所有软件防护的基础。

5. **计时精度**：使用 `GetLastInputInfo` 判断用户是否活跃（空闲 < 5 秒才计时），每次 tick 间隔 5 秒，精度 ±5 秒，同时支持跨天自动重置。

### IPC 消息协议（Named Pipe，JSON）

AdminPanel ↔ GuardService 通过 Named Pipe 交换 JSON 消息，指令包括：`GET_STATUS`、`UPDATE_CONFIG`、`ADD_TIME`、`UNLOCK`、`LOCK_NOW`、`PAUSE`、`SHUTDOWN_NOW`。Pipe 本身受 DACL 保护，仅允许本机进程连接。

### 数据文件位置

| 文件 | 路径 | 说明 |
|------|------|------|
| `config.bin` | `C:\ProgramData\ChildPCGuard\config.bin` | AES-256 加密配置，含规则+密码哈希 |
| `state.json` | `C:\ProgramData\ChildPCGuard\state.json` | 今日累计时长，重启恢复用 |
| 使用日志 | `C:\ProgramData\ChildPCGuard\logs\usage-YYYY-MM-DD.log` | 每日使用记录 |
| 安全日志 | `C:\ProgramData\ChildPCGuard\logs\security.log` | 违规尝试记录 |

---

## 开发路线图

详细任务清单和当前进度见 `task_plan.md`。Phase 顺序：

1. **Phase 1**（~2 周）：GuardService + TimeTracker + RuleEngine + 系统原生锁屏 + state.json 恢复
2. **Phase 2**（~1 周）：防护加固（服务/进程/文件/注册表 ACL + NTP + AES 配置加密）
3. **Phase 3**（~1 周）：自定义锁屏（LockOverlay + 虚拟桌面 + 键盘钩子）
4. **Phase 4**（~3-5 天）：双进程互保（AgentA/AgentB + Named Pipe 心跳）
5. **Phase 5**（~1 周）：管理员界面（AdminPanel + Named Pipe 通信）
6. **Phase 6**（可选）：应用黑名单、统计图表等增强功能

每个 Phase 完成后，对照 `docs/ACCEPTANCE_CRITERIA.md` 验收，更新 `task_plan.md` 和 `CHANGELOG.md`。
