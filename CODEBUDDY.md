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

---

## 项目文档体系

本项目共 9 份工程文档，按**使用时机**分为三类：开发前必读、开发中参考、开发后维护。

---

### 一、开发前必读

#### `ChildPCGuard_项目方案.md` ← 唯一权威技术参考
**作用**：完整技术实现方案，包含架构设计、各模块代码级规格、12 层防护机制的逐条实现、关键 API 调用、数据结构定义。  
**使用方式**：
- 开始任意模块编码前，先查阅对应章节（例如：实现 `TimeTracker` 前读"第 4 节 时间计时模块"）
- 遇到技术选型分歧时，以本文档为准，不得随意偏离
- 本文档是唯一开发参考，其他文档（SRS、测试计划等）均以此为技术基础
- **禁止**在未更新本文档的情况下单方面修改已确定的架构决策

#### `docs/REQUIREMENTS.md`（SRS，需求规格说明书）
**作用**：39 条功能需求（MoSCoW 优先级标注）+ 19 条非功能需求（性能/安全/兼容性），是需求基线文档。  
**使用方式**：
- 开发前明确"要做什么"——每条需求有唯一 ID（如 `FR-001`），开发时注释中可引用对应 ID
- 编写测试用例时，根据本文档中的功能需求逐条对应
- 需求发生变更时，先更新本文档并标注版本，再同步修改技术方案
- Must-have 需求必须在 Phase 6 结束前全部实现；Should-have 按 Phase 计划交付；Could-have 为可选增强

#### `README.md`（项目首页）
**作用**：向外部人员介绍项目，包含功能亮点、架构概览、安装方式、开发路线图、技术栈。  
**使用方式**：
- 外部人员初次了解项目时阅读
- 每次发布新版本（Phase 完成）后，同步更新功能列表和路线图进度
- 不包含技术实现细节，技术细节见 `ChildPCGuard_项目方案.md`

---

### 二、开发中参考

#### `docs/ACCEPTANCE_CRITERIA.md`（验收标准 / DoD）
**作用**：定义每个 Phase 的"完成定义"（Definition of Done），共 6 个 Phase × 若干验收条件，共约 40 条。  
**使用方式**：
- 每个 Phase 开发**结束时**，逐条对照本文档执行验收自检，未全部通过不得视为该 Phase 完成
- 验收通过后，在对应验收项旁打勾（`[x]`），并更新 `task_plan.md` 和 `CHANGELOG.md`
- 如验收中发现方案设计问题，记录在本文档末尾的"遗留问题"区域
- 通用 DoD（文档顶部）适用于所有 Phase，每次验收时也须检查

#### `docs/TEST_PLAN.md`（测试计划）
**作用**：4 层测试策略（单元/集成/功能/渗透）+ 50+ 个具体测试用例 + 缺陷优先级管理（P0~P3）。  
**使用方式**：
- 编写单元测试时，参照"第 5 节 单元测试用例"确认覆盖点，不得遗漏 P0 级用例
- 集成测试阶段（Phase 4/5 后），按"第 6 节 集成测试用例"执行跨进程/IPC 场景测试
- 安全渗透测试在每个 Phase 验收前执行，按"第 7 节 防护机制测试"逐项验证 12 层防护
- 发现缺陷时，按 P0~P3 分级标注，P0（系统级功能失效）必须当天修复后方可继续开发
- 本文档中的测试用例 ID 与 `docs/REQUIREMENTS.md` 中的需求 ID 对应，方便需求溯源

#### `docs/SECURITY.md`（安全设计说明）
**作用**：STRIDE 威胁建模、12 层防护的安全原理、加密方案（AES-256-GCM + BCrypt）、IPC 通信安全设计、已知局限。  
**使用方式**：
- 实现任何涉及加密、权限控制、进程保护的代码前，先阅读对应章节，确保实现与设计一致
- 加密相关代码（`ConfigManager`、`StateManager`）必须严格遵循"第 3 节 加密方案"，不得自行简化
- 若需调整防护机制，先评估对威胁建模的影响，更新本文档后再修改代码
- "已知局限"章节说明了本程序防护边界，超出边界的场景不属于 Bug，不予修复

#### `docs/CONTRIBUTING.md`（贡献指南 / 代码规范）
**作用**：代码风格规范、命名约定、分支策略、Commit Message 格式（Conventional Commits）、PR 流程、调试命令。  
**使用方式**：
- 首次开始写代码前通读一遍，建立统一的代码风格认知
- 每次提交代码前，检查 Commit Message 是否符合本文档"第 4 节 提交规范"（`feat:`、`fix:`、`test:` 等前缀）
- 分支命名遵循本文档"第 3 节 分支策略"（`feat/phase1-guard-service`、`fix/timer-overflow` 等）
- 代码审查时，以本文档中的"代码规范清单"作为 checklist

#### `task_plan.md`（开发进度追踪）
**作用**：按 Phase 拆解的细粒度任务列表，以 `[ ]`/`[x]` 标记完成状态，是日常开发的"看板"。  
**使用方式**：
- 每次开始工作前查看当前待完成任务（`[ ]`）
- 每完成一个子任务，立即将其标记为 `[x]`，并 push 到 GitHub
- Phase 验收通过后，对照 `docs/ACCEPTANCE_CRITERIA.md` 确认所有任务已打勾
- 若需新增任务（如发现遗漏功能），在对应 Phase 下添加新的 `[ ]` 条目，不得修改已完成条目

---

### 三、开发后维护

#### `CHANGELOG.md`（版本变更记录）
**作用**：遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/) 规范，记录每个版本的新增/修改/修复/移除内容。  
**使用方式**：
- 每个 Phase 验收通过后，将 `[Unreleased]` 中对应内容移至新版本块（如 `[0.1.0] - 2026-06-xx`）
- 日常开发中的小变更，在 `[Unreleased]` 区域实时追加，不要积压到 Phase 结束再补写
- 版本号规则：Phase 1 = `v0.1.0`，Phase 2 = `v0.2.0`，依此类推；正式发布为 `v1.0.0`
- 每条记录写明用户可感知的变化，避免写"重构了 XXX"此类无意义的内部变更

#### `CODEBUDDY.md`（本文件 / AI 开发指引）
**作用**：为 CodeBuddy AI 提供项目上下文，包含铁律约束、架构概览、构建命令、IPC 协议、数据文件位置、开发路线图。  
**使用方式**：
- AI 在每次对话开始时自动读取本文件，获取项目上下文
- 架构或约束发生变化时，同步更新本文件，保持与 `ChildPCGuard_项目方案.md` 的一致性
- "铁律"章节为最高优先级约束，AI 和开发者均须遵守，不得以任何理由绕过

---

### 文档使用时机速查

| 时机 | 应查阅的文档 |
|------|-------------|
| 开始一个新 Phase | `task_plan.md`（确认任务范围）→ `ChildPCGuard_项目方案.md`（查阅技术规格）→ `docs/REQUIREMENTS.md`（确认需求 ID） |
| 实现某个具体功能 | `ChildPCGuard_项目方案.md`（对应章节）+ `docs/SECURITY.md`（涉及安全时）|
| 编写单元测试 | `docs/TEST_PLAN.md`（测试用例）+ `docs/REQUIREMENTS.md`（需求 ID 对应）|
| Phase 开发完成，准备验收 | `docs/ACCEPTANCE_CRITERIA.md`（逐条自检）→ 更新 `task_plan.md` + `CHANGELOG.md` |
| 提交代码 | `docs/CONTRIBUTING.md`（Commit 格式 + 分支规范）|
| 发现安全相关 Bug | `docs/SECURITY.md`（确认是否在防护边界内）+ `docs/TEST_PLAN.md`（补充测试用例）|
| 外部人员了解项目 | `README.md` |

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
