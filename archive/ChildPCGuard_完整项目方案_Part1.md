# ChildPCGuard — 儿童电脑时间管控程序 完整项目方案（第一部分）

> **版本**: 2.0 终极防护版  
> **编写时间**: 2026-05-02  
> **目标平台**: Windows 10/11（专业版或家庭版均可）  
> **开发语言**: C# / .NET 8  
> **目标用户**: 初一学生（使用电脑约5年，会任务管理器，有一定技术能力）  
> **阅读说明**: 本文档共两部分。Part1 包含需求、架构、模块设计、配置文件、安装脚本、防护机制等全部设计文档。Part2 包含全部源代码。两部分合并即为完整开发文档，其他AI凭此两份文档即可独立完成整个项目。

---

## 目录

1. [项目背景与需求](#1-项目背景与需求)
2. [威胁模型与防护层级](#2-威胁模型与防护层级)
3. [系统架构设计](#3-系统架构设计)
4. [项目文件结构](#4-项目文件结构)
5. [配置文件设计](#5-配置文件设计)
6. [核心模块详细设计](#6-核心模块详细设计)
7. [安装部署脚本](#7-安装部署脚本)
8. [卸载脚本](#8-卸载脚本)
9. [防护机制说明](#9-防护机制说明)
10. [杀毒软件兼容性](#10-杀毒软件兼容性)
11. [无法防御的场景与应对](#11-无法防御的场景与应对)
12. [依赖包清单](#12-依赖包清单)
13. [测试清单](#13-测试清单)
14. [注意事项与伦理说明](#14-注意事项与伦理说明)
15. [Win32 API 参考](#15-win32-api-参考)

---

## 1. 项目背景与需求

### 1.1 项目背景

家长需要限制初一孩子（约12-13岁）的电脑使用时间。孩子已使用电脑约5年，具备：
- 会使用任务管理器结束进程
- 了解基本的 Windows 服务管理（sc stop、services.msc）
- 可能尝试修改系统时间、注册表等手段
- 可能尝试进入安全模式、使用PE系统绕过

### 1.2 功能需求清单

| 需求编号 | 功能描述 | 优先级 | 默认值 |
|----------|----------|--------|--------|
| R-01 | 每天设定可玩电脑的总时长 | P0 | 工作日120分钟，周末240分钟 |
| R-02 | 每连续使用N分钟，强制休息M分钟 | P0 | 45分钟使用，5分钟休息 |
| R-03 | 超时后锁屏，输入家长密码才能解锁 | P0 | 必须实现 |
| R-04 | 每天晚上22:00自动关机 | P0 | 22:00 |
| R-05 | 程序以隐秘后台服务运行，不显示在任务栏 | P0 | 必须实现 |
| R-06 | 孩子账户无法通过任务管理器结束进程 | P0 | 必须实现 |
| R-07 | 孩子账户无法停止或卸载程序 | P0 | 必须实现 |
| R-08 | 意外崩溃或重启后自动恢复计时 | P0 | 必须实现 |
| R-09 | 防止孩子修改系统时间绕过限制 | P0 | NTP校验 |
| R-10 | 锁屏前10/5/1分钟弹出倒计时提醒 | P1 | 必须实现 |
| R-11 | 锁屏界面拦截 Alt+Tab、Win键等快捷键 | P1 | 必须实现 |
| R-12 | 家长可通过密码访问管理界面 | P1 | Ctrl+Shift+Alt+P 或运行 AdminPanel.exe |
| R-13 | 记录使用日志（每天玩了什么、用了多久） | P1 | 必须实现 |
| R-14 | 错误密码3次锁定5分钟 | P1 | 必须实现 |
| R-15 | 检测安全模式，进入安全模式自动关机 | P1 | 必须实现 |
| R-16 | 虚拟机检测（防止孩子用虚拟机绕过） | P2 | 可选 |
| R-17 | 配置文件AES加密（防止直接修改JSON） | P1 | 必须实现 |
| R-18 | 黑名单程序/网站（触发立即锁屏） | P2 | 可选 |
| R-19 | PE环境检测 | P1 | 必须实现 |
| R-20 | 进程句柄权限控制（防任务管理器Kill） | P0 | 必须实现 |

---

## 2. 威胁模型与防护层级

### 2.1 孩子的可能反制手段

| 攻击手段 | 危险等级 | 防御方案 | 所在防护层 |
|----------|----------|----------|------------|
| 任务管理器结束进程 | ★★★★★ | SYSTEM权限 + 进程句柄DACL + 双进程互保 | 第2/3/9层 |
| `sc stop` 停止服务 | ★★★★★ | 服务DACL拒绝普通用户操作 | 第4层 |
| `services.msc` 禁用服务 | ★★★★ | 服务DACL + 注册表DACL | 第4/5层 |
| 删除程序文件 | ★★★ | 安装目录ACL保护 | 第6层 |
| 修改系统时间 | ★★★ | NTP网络时间校验（5分钟误差即告警锁屏） | 第8层 |
| 重启电脑 | ★★★ | 开机自启，重启后恢复计时（state.json持久化） | 第2层 |
| 修改注册表启动项 | ★★★ | 注册表ACL保护 | 第5层 |
| 进入安全模式 | ★★★★ | 检测SafeBoot注册表键，检测到立即关机 | 第10层 |
| PE系统启动 | ★★★★★ | 检测MiniNT注册表键 + 建议启用BitLocker | 第11层 |
| 虚拟机玩游戏 | ★★ | 检测VMware/VirtualBox特征，锁屏或记录 | 第12层 |
| 修改config.json配置 | ★★★ | 文件ACL + AES-256加密 | 第6/12层 |
| 以管理员账户登录 | ★★★★★ | **核心前提：孩子账户必须是标准用户（非管理员）** | 第1层 |
| 程序崩溃 | ★★★ | SCM故障恢复（崩溃1秒内重启）+ 双进程互保 | 第3/7层 |

### 2.2 防护层级总览（12层）

```
第01层: 标准用户账户权限限制 ← 最基础，最重要，必须先做
第02层: Windows服务 + SYSTEM权限（核心调度以系统最高权限运行）
第03层: 双进程互保（AgentA ↔ AgentB 互相监控，杀一复活一）
第04层: 服务DACL（sc stop / services.msc 被拒绝返回"拒绝访问"）
第05层: 注册表DACL（无法修改/删除服务相关注册表键）
第06层: 文件ACL（安装目录和配置文件对标准用户只读/不可访问）
第07层: SCM故障恢复（服务崩溃后1秒内由SCM自动重启，重试3次）
第08层: NTP时间校验（5分钟误差检测时间篡改，立即锁屏）★
第09层: 进程句柄DACL（移除PROCESS_TERMINATE权限，任务管理器Kill失败）★
第10层: 安全模式检测（检测到SafeBoot标志立即强制关机）★
第11层: PE环境检测（检测WinPE注册表特征，立即关机）★
第12层: 配置文件AES-256加密（.bin格式，无法直接读取修改配置内容）★
```

> ★ = 相比基础方案新增的防护层

### 2.3 防护层级对比（针对不同技术水平）

| 孩子技术水平 | 能突破的层 | 无法突破的层 |
|-------------|-----------|------------|
| 普通用户（只会任务管理器） | 无 | 全部12层 |
| 了解服务管理（会sc stop） | 无 | 全部12层（DACL阻止） |
| 知道安全模式 | 无 | 第10层阻止+关机 |
| 会PE系统 | 无（需BitLocker配合） | 第11层阻止+关机 |
| 有管理员密码 | 全部 | 软件层全部失效 |

---

## 3. 系统架构设计

### 3.1 进程架构

```
┌─────────────────────────────────────────────────────────────────┐
│                     SYSTEM 权限层（最高权限）                     │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │           ChildControlService.exe                        │    │
│  │    注册为Windows服务，以LocalSystem(SYSTEM)权限运行       │    │
│  │    服务名: WinSecSvc_a1b2c3d4                            │    │
│  │    显示名: "Windows Security Update Service"（伪装）      │    │
│  │                                                          │    │
│  │  ┌────────────┐ ┌───────────────┐ ┌──────────────────┐  │    │
│  │  │TimeTracker │ │ProcessGuardian│ │ShutdownScheduler │  │    │
│  │  │计时+判断   │ │进程守护       │ │22:00关机         │  │    │
│  │  └────────────┘ └───────────────┘ └──────────────────┘  │    │
│  │  ┌────────────┐ ┌───────────────┐ ┌──────────────────┐  │    │
│  │  │AppMonitor  │ │WebMonitor     │ │NamedPipeServer   │  │    │
│  │  │程序监控    │ │网站监控       │ │IPC通信           │  │    │
│  │  └────────────┘ └───────────────┘ └──────────────────┘  │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
         │ Named Pipe IPC              │ Named Pipe IPC
         ▼                            ▼
┌──────────────────────┐   ┌────────────────────────┐
│  svchost.exe         │◄─►│  RuntimeBroker.exe      │
│  (实际是AgentA)       │   │ (实际是AgentB)           │
│  伪装成系统进程       │   │ 伪装成系统进程           │
│  互相心跳监控        │   │ 互相心跳监控            │
└──────────────────────┘   └────────────────────────┘
         │
         ▼（触发锁屏时由服务启动）
┌──────────────────────────────────────────────────┐
│  LockScreen.exe                                   │
│  虚拟桌面方案（CreateDesktop/SwitchDesktop）       │
│  全屏锁屏界面，WPF技术开发                        │
│  PIN密码验证 + 全局键盘钩子                       │
│  错误3次锁定5分钟                                 │
└──────────────────────────────────────────────────┘
         │
         ▼（家长管理界面，独立进程）
┌──────────────────────────────────────────────────┐
│  AdminPanel.exe                                   │
│  启动方式：Ctrl+Shift+Alt+P 或直接运行            │
│  需要输入家长密码才能进入                         │
│  功能：查看日志、修改规则、临时加时、黑名单管理   │
└──────────────────────────────────────────────────┘
```

### 3.2 数据流

```
[用户操作鼠标/键盘]
    ↓
[GetLastInputInfo 获取最后输入时间，计算空闲时长]
    ↓
[NTP校验当前时间是否被篡改] ←→ [pool.ntp.org / time.windows.com]
    ↓
[TimeTracker 每30秒累加使用时间 → 写入state.json]
    ↓
[判断触发条件]
    ├── 总时长 >= dailyLimitMinutes → 今日时长已用完锁屏
    ├── 连续时长 >= continuousLimitMinutes → 连续使用强制休息锁屏
    ├── 当前进程在黑名单 → 立即锁屏
    ├── 浏览器访问了黑名单网站 → 立即锁屏
    ├── 当前时间 >= 22:00 → 关机锁屏
    └── NTP时间偏差 > 5分钟 → 时间篡改锁屏
              ↓
[启动 LockScreen.exe --reason=X]
              ↓
[虚拟桌面隔离 + 全局键盘钩子]
              ↓
[PIN验证] → 正确 → [发送UnlockRequest到Service，Service验证BCrypt密码]
               ↓                    ↓
          [解锁/关闭锁屏]    [继续锁定/加时/标记]
```

### 3.3 IPC通信协议

所有进程间通信使用 Named Pipe（命名管道）：
- 管道名称：`\\.\pipe\ChildControlPipe`
- 传输格式：JSON 序列化的 `PipeMessage` 对象
- 编码：UTF-8
- 每次发送一行（以 `\n` 结尾）

---

## 4. 项目文件结构

```
ChildPCGuard/
├── ChildPCGuard.sln                           ← Visual Studio 解决方案
│
├── src/
│   ├── ChildControl.Service/                  ← 核心：Windows 服务
│   │   ├── ChildControl.Service.csproj
│   │   ├── Program.cs                         ← 服务入口，启动时检测安全/PE模式
│   │   ├── ChildControlWorker.cs              ← 服务主循环（BackgroundService）
│   │   ├── TimeTracker.cs                     ← 时间追踪（GetLastInputInfo）
│   │   ├── ProcessGuardian.cs                 ← 双进程守护
│   │   ├── AppMonitor.cs                      ← 程序使用监控
│   │   ├── WebMonitor.cs                      ← 网站访问监控
│   │   ├── ShutdownScheduler.cs               ← 定时关机
│   │   ├── NamedPipeServer.cs                 ← IPC服务端
│   │   └── Configuration.cs                   ← 配置加载
│   │
│   ├── ChildControl.Agent/                    ← 守护进程（双进程互保）
│   │   ├── ChildControl.Agent.csproj
│   │   ├── Program.cs                         ← Agent 入口
│   │   └── Agent.cs                           ← 心跳发送 + 对等体监控
│   │
│   ├── ChildControl.LockScreen/               ← 锁屏界面（WPF）
│   │   ├── ChildControl.LockScreen.csproj
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── LockScreenWindow.xaml              ← 主锁屏窗口
│   │   ├── LockScreenWindow.xaml.cs
│   │   ├── KeyboardHook.cs                    ← 全局键盘钩子
│   │   └── PinVerifier.cs                     ← PIN验证逻辑
│   │
│   ├── ChildControl.AdminPanel/               ← 家长管理界面（WinForms）
│   │   ├── ChildControl.AdminPanel.csproj
│   │   ├── Program.cs
│   │   ├── LoginForm.cs / LoginForm.Designer.cs
│   │   ├── MainForm.cs / MainForm.Designer.cs ← 主界面（统计+配置）
│   │   └── DashboardForm.cs                   ← 统计看板
│   │
│   ├── ChildControl.Shared/                   ← 共享库（所有项目引用）
│   │   ├── ChildControl.Shared.csproj
│   │   ├── NativeAPI.cs                       ← Win32 API 声明
│   │   ├── PipeMessages.cs                    ← IPC 消息定义
│   │   └── Models.cs                          ← 数据模型
│   │
│   └── ChildControl.Protection/               ← 自我保护模块
│       ├── ChildControl.Protection.csproj
│       ├── ServiceProtector.cs                ← 服务 DACL 保护
│       ├── RegistryProtector.cs               ← 注册表 DACL 保护
│       ├── FileProtector.cs                   ← 文件/目录 ACL 保护
│       ├── ProcessAccessControl.cs            ← 进程句柄权限控制
│       ├── NtpTimeValidator.cs                ← NTP 时间校验
│       ├── SecureConfigStorage.cs             ← 配置文件 AES 加密
│       ├── SafeModeDetector.cs                ← 安全模式检测
│       ├── PEDetector.cs                      ← PE 环境检测
│       ├── VirtualMachineDetector.cs          ← 虚拟机检测
│       └── ServiceNameGenerator.cs            ← 服务名生成（机器ID绑定）
│
├── installer/
│   ├── install.ps1                            ← PowerShell 安装脚本
│   ├── install.bat                            ← 安装启动器（以管理员运行）
│   ├── uninstall.ps1                          ← 卸载脚本
│   └── uninstall.bat
│
├── data/
│   └── config.example.json                    ← 配置文件示例（明文）
│
└── docs/
    └── README.md
```

---

## 5. 配置文件设计

### 5.1 主配置文件

**存储路径**: `C:\ProgramData\ChildControl\config.bin`（AES-256加密后的二进制文件）  
**明文格式**（config.json 仅供参考，实际存储是加密后的 .bin）:

```json
{
  "version": "2.0",
  "isEnabled": true,
  "dailyLimitMinutes": 180,
  "continuousLimitMinutes": 45,
  "restDurationMinutes": 5,
  "shutdownTime": "22:00",
  "unlockPasswordHash": "$2a$11$xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "warningMinutes": [10, 5, 1],
  "timeZone": "China Standard Time",
  "useNtpValidation": true,
  "ntpToleranceMinutes": 5,
  "blockedApps": ["steam.exe", "leagueoflegends.exe"],
  "blockedSites": ["youtube.com", "bilibili.com", "twitch.tv"],
  "monitoringIntervalSeconds": 30,
  "heartbeatIntervalSeconds": 5,
  "serviceName": "WinSecSvc_a1b2c3d4",
  "displayName": "Windows Security Update Service",
  "weekdayRules": {
    "dailyLimitMinutes": 120,
    "allowedTimeWindows": [
      { "start": "15:00", "end": "21:00" }
    ]
  },
  "weekendRules": {
    "dailyLimitMinutes": 240,
    "allowedTimeWindows": [
      { "start": "09:00", "end": "22:00" }
    ]
  }
}
```

**字段说明**：

| 字段 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `dailyLimitMinutes` | int | 每日总使用时长（分钟） | 180 |
| `continuousLimitMinutes` | int | 连续使用上限（分钟），超出强制休息 | 45 |
| `restDurationMinutes` | int | 强制休息时长（分钟） | 5 |
| `shutdownTime` | string | 每天自动关机时间，格式 `HH:mm` | "22:00" |
| `unlockPasswordHash` | string | BCrypt 哈希后的解锁密码 | 需初始化 |
| `warningMinutes` | int[] | 锁屏前提前几分钟弹出警告 | [10, 5, 1] |
| `useNtpValidation` | bool | 是否启用NTP时间校验 | true |
| `ntpToleranceMinutes` | int | NTP误差容忍分钟数 | 5 |
| `blockedApps` | string[] | 黑名单进程名（含.exe） | [] |
| `blockedSites` | string[] | 黑名单域名 | [] |
| `monitoringIntervalSeconds` | int | 主监控循环间隔（秒） | 30 |
| `heartbeatIntervalSeconds` | int | Agent心跳间隔（秒） | 5 |
| `serviceName` | string | 服务内部名（伪装名） | "WinSecSvc_..." |
| `displayName` | string | 服务显示名（伪装） | "Windows Security Update Service" |
| `weekdayRules` | object | 工作日规则（覆盖默认值） | null |
| `weekendRules` | object | 周末规则（覆盖默认值） | null |

### 5.2 每日状态文件

**路径**: `C:\ProgramData\ChildControl\data\state.json`（明文，但被文件ACL保护）

```json
{
  "date": "2026-05-02",
  "totalUsedMinutes": 87.5,
  "continuousUsedMinutes": 23.0,
  "currentState": 0,
  "sessionStart": "2026-05-02T14:30:00",
  "lastInputTime": "2026-05-02T16:00:00",
  "restEndTime": null,
  "lockCount": 1
}
```

`currentState` 枚举值：`0=Using, 1=Resting, 2=Locked, 3=Shutdown, 4=Paused`

### 5.3 使用日志文件

**路径**: `C:\ProgramData\ChildControl\logs\2026-05-02_process.json`

```json
[
  {
    "timestamp": "2026-05-02T14:30:00",
    "processName": "chrome",
    "processPath": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
    "windowTitle": "YouTube - Google Chrome",
    "durationSeconds": 1800
  }
]
```

### 5.4 安全事件日志

**路径**: `C:\ProgramData\ChildControl\logs\security.log`（追加写入纯文本）

```
[2026-05-02 16:30:00] TIME TAMPERING DETECTED: System=2026-05-02 10:00:00, Network=2026-05-02 16:30:00, Diff=390.00 minutes
[2026-05-02 17:00:00] WRONG PASSWORD: attempt 1/3
[2026-05-02 17:01:00] WRONG PASSWORD: attempt 2/3
[2026-05-02 17:01:30] LOCKOUT TRIGGERED: locked for 5 minutes
```

---

## 6. 核心模块详细设计

### 6.1 TimeTracker（时间追踪器）

**核心算法**：

1. 每 `monitoringIntervalSeconds`（默认30秒）执行一次
2. 调用 `GetLastInputInfo` 获取 Windows 系统最后输入时间（Tick值）
3. 计算 `idleMs = Environment.TickCount - lastInput.dwTime`
4. 如果 `idleMs < 5000`（5秒以内有输入）→ 认为"正在使用"
5. 使用量 += `monitoringIntervalSeconds / 60.0` 分钟
6. 总使用量 + 连续使用量 同时累加
7. 每次更新后持久化到 `state.json`（重启后恢复）

**状态转换**：

```
Using
  ├──(连续时长 >= continuousLimitMinutes) → StartRest() → Resting
  │                                              ↓（休息倒计时结束）
  │                                          EndRest() → Using
  ├──(总时长 >= dailyLimitMinutes) → Locked（今日不可自动解锁）
  └──(22:00到达) → Shutdown
```

**跨天重置**：每次 `Update()` 检查日期，如日期不同则重置状态。

### 6.2 ProcessGuardian（进程守护）

**实现原理**：

```
安装时复制两份 Agent.exe：
  C:\Program Files\ChildControl\svchost.exe       ← AgentA的可执行文件
  C:\Program Files\ChildControl\RuntimeBroker.exe ← AgentB的可执行文件

启动参数：
  svchost.exe --id=A --peer=B --peer-path="...\RuntimeBroker.exe"
  RuntimeBroker.exe --id=B --peer=A --peer-path="...\svchost.exe"

守护机制（三层）：
  层1: Service 监控两个Agent进程是否存活（进程ID检测 + HasExited）
  层2: Service 监控两个Agent的心跳（超过3倍心跳间隔视为死亡）
  层3: AgentA 监控 AgentB 是否存活，AgentB 监控 AgentA 是否存活

任一 Agent 死亡 → 另一个立即重启它 + Service 也重启它（双重保障）
```

**心跳协议**：
- Agent每5秒通过Named Pipe发送 `HeartbeatMessage`
- 消息中包含 `AgentId`（"A" 或 "B"）
- Service收到后更新 `_lastHeartbeat[agentId]`
- 超过 `heartbeatIntervalSeconds * 3` 秒无心跳 → 触发重启

### 6.3 LockScreen（锁屏界面）

**虚拟桌面方案（比全屏窗口更安全）**：

```csharp
// 创建隔离的虚拟桌面
IntPtr lockDesktop = CreateDesktop("LockScreen_" + Guid.NewGuid().ToString("N"),
    IntPtr.Zero, IntPtr.Zero, 0, DESKTOP_ALL_ACCESS, IntPtr.Zero);

// 切换到锁屏桌面
SwitchDesktop(lockDesktop);

// 在新桌面上启动锁屏进程
// 原桌面上的窗口对用户完全不可见
// Alt+Tab、Win+D 等快捷键在新桌面上无法切换回原桌面
```

**全局键盘钩子**（防快捷键绕过）：

```csharp
// 使用低级键盘钩子（WH_KEYBOARD_LL）
// 拦截的按键：
// - VK_LWIN / VK_RWIN (Windows键)
// - Alt+Tab
// - Alt+F4
// - Ctrl+Esc
// - F11
// 注意：Ctrl+Alt+Del 是内核级，无法拦截
// 但孩子不知道 Windows 密码，即使进入也无法操作
```

**PIN验证逻辑**：
- 4-6位数字密码
- 发送 `UnlockRequestMessage` 到 Service
- Service 用 BCrypt 验证密码
- 错误3次 → 锁定5分钟，禁止输入
- 正确 → `UnlockResponseMessage.Success=true` → 关闭锁屏
- 连续使用休息锁屏：即使密码正确也必须等休息倒计时结束

**启动参数**：
```
LockScreen.exe --reason=1    ← 今日时长已用完
LockScreen.exe --reason=2    ← 连续使用休息
LockScreen.exe --reason=3    ← 黑名单程序
LockScreen.exe --reason=4    ← 黑名单网站
LockScreen.exe --reason=5    ← 22:00关机
LockScreen.exe --reason=6    ← 时间篡改
```

### 6.4 AdminPanel（管理界面）

**功能列表**：

1. **登录界面**：BCrypt密码验证
2. **今日统计**：总使用时间、剩余时间、连续时间、锁屏次数
3. **历史图表**：过去7天使用趋势（WinForms Chart控件）
4. **规则配置**：修改每日时长、休息设置、关机时间
5. **临时加时**：输入"给孩子再加N分钟"
6. **暂停/恢复**：临时关闭管控
7. **日志查看**：程序日志 + 网站日志（按日期）
8. **黑名单管理**：添加/删除程序、网站黑名单
9. **修改密码**：修改家长密码

**调用方式**：
- 直接运行 `C:\Program Files\ChildControl\AdminPanel.exe`
- 或配置快捷键（需要在安装时设置）

### 6.5 ShutdownScheduler（定时关机）

**双重保障**：

方案1（主要）：Service 每分钟检查当前时间，到达 `shutdownTime` 时：
1. 发送锁屏触发（`LockReason.Shutdown`）
2. 等待 30 秒（让孩子看到关机提示）
3. 执行 `shutdown.exe /s /f /t 0`

方案2（备用）：安装时创建 Windows 任务计划程序任务：
```xml
<Task>
  <Triggers>
    <CalendarTrigger>
      <StartBoundary>2026-01-01T22:00:00</StartBoundary>
      <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>
    </CalendarTrigger>
  </Triggers>
  <Actions>
    <Exec>
      <Command>shutdown.exe</Command>
      <Arguments>/s /f /t 0</Arguments>
    </Exec>
  </Actions>
</Task>
```

即使服务被意外停止，任务计划也会执行关机。

### 6.6 NtpTimeValidator（NTP时间校验）

**工作原理**：

1. 实现 NTP 协议（UDP 123端口）
2. 向以下NTP服务器请求时间（按顺序尝试）：
   - `cn.pool.ntp.org`（中国节点，速度快）
   - `pool.ntp.org`
   - `time.windows.com`
   - `time.google.com`
3. 缓存结果5分钟（避免频繁请求）
4. 比较网络时间与系统时间的差值
5. 差值超过 `ntpToleranceMinutes`（默认5分钟）→ 触发安全告警
6. 记录到 `security.log`，同时触发锁屏

**断网场景**：如果无法获取NTP时间（孩子断网），使用系统时间作为fallback，但记录NTP不可用警告（不触发锁屏，避免误报）。

---

## 7. 安装部署脚本

### 7.1 install.bat（启动器）

```batch
@echo off
:: 检查管理员权限
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo 请以管理员身份运行此脚本！
    echo 右键点击 install.bat，选择"以管理员身份运行"
    pause
    exit /b 1
)

:: 以管理员权限运行 PowerShell 安装脚本
PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
pause
```

### 7.2 install.ps1（完整安装脚本）

```powershell
#Requires -RunAsAdministrator

param(
    [string]$InstallDir = "C:\Program Files\ChildControl",
    [string]$DataDir = "C:\ProgramData\ChildControl",
    [string]$ServiceName = "WinSecSvc_a1b2c3d4",
    [string]$DisplayName = "Windows Security Update Service"
)

Write-Host "=== ChildPCGuard 安装程序 ===" -ForegroundColor Cyan
Write-Host ""

# ——— 第1步：创建目录 ———
Write-Host "[1/9] 创建安装目录..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path "$DataDir\logs" | Out-Null
New-Item -ItemType Directory -Force -Path "$DataDir\data" | Out-Null

# ——— 第2步：复制程序文件 ———
Write-Host "[2/9] 复制程序文件..." -ForegroundColor Yellow
$SourceDir = Split-Path -Parent $PSCommandPath
$binDir = Join-Path $SourceDir "bin"

# 复制主要可执行文件
Copy-Item "$binDir\ChildControl.Service.exe" "$InstallDir\" -Force
Copy-Item "$binDir\LockScreen.exe" "$InstallDir\" -Force
Copy-Item "$binDir\AdminPanel.exe" "$InstallDir\" -Force

# 复制Agent（使用伪装名）
Copy-Item "$binDir\ChildControl.Agent.exe" "$InstallDir\svchost.exe" -Force
Copy-Item "$binDir\ChildControl.Agent.exe" "$InstallDir\RuntimeBroker.exe" -Force

# 复制依赖库
Copy-Item "$binDir\*.dll" "$InstallDir\" -Force -ErrorAction SilentlyContinue
Copy-Item "$binDir\*.json" "$InstallDir\" -Force -ErrorAction SilentlyContinue

# ——— 第3步：生成初始配置 ———
Write-Host "[3/9] 生成初始配置..." -ForegroundColor Yellow

# 提示家长设置密码
$password = Read-Host "请设置家长密码（解锁和管理界面使用）" -AsSecureString
$passwordText = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

# 调用配置工具生成加密配置
$configJson = @{
    version = "2.0"
    isEnabled = $true
    dailyLimitMinutes = 180
    continuousLimitMinutes = 45
    restDurationMinutes = 5
    shutdownTime = "22:00"
    unlockPasswordHash = ""  # 由服务启动时生成BCrypt哈希
    warningMinutes = @(10, 5, 1)
    timeZone = "China Standard Time"
    useNtpValidation = $true
    ntpToleranceMinutes = 5
    blockedApps = @()
    blockedSites = @()
    monitoringIntervalSeconds = 30
    heartbeatIntervalSeconds = 5
    serviceName = $ServiceName
    displayName = $DisplayName
} | ConvertTo-Json -Depth 5

# 临时保存明文配置（服务启动时会自动加密）
$configJson | Out-File -FilePath "$DataDir\config.json" -Encoding UTF8

# 使用配置工具加密配置并设置密码
& "$InstallDir\ChildControl.Service.exe" --init-config --password="$passwordText"

# 清理明文配置
Remove-Item "$DataDir\config.json" -Force -ErrorAction SilentlyContinue

# ——— 第4步：注册 Windows 服务 ———
Write-Host "[4/9] 注册Windows服务..." -ForegroundColor Yellow

# 如果服务已存在，先删除
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# 注册新服务
$binaryPath = "`"$InstallDir\ChildControl.Service.exe`""
sc.exe create $ServiceName `
    binPath= $binaryPath `
    start= auto `
    obj= LocalSystem `
    DisplayName= $DisplayName | Out-Null

# 设置服务描述
sc.exe description $ServiceName "Provides Windows security update and maintenance services." | Out-Null

# ——— 第5步：配置服务故障恢复（SCM自动重启）———
Write-Host "[5/9] 配置服务故障恢复..." -ForegroundColor Yellow
sc.exe failure $ServiceName reset= 86400 actions= restart/1000/restart/1000/restart/1000 | Out-Null
sc.exe failureflag $ServiceName 1 | Out-Null

# ——— 第6步：配置服务 DACL（防 sc stop）———
Write-Host "[6/9] 配置服务DACL保护..." -ForegroundColor Yellow
# SDDL含义：
# D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)  = SYSTEM 有完全控制
# (A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA) = Administrators 有完全控制
# （普通用户没有任何权限）
$sddl = "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)"
sc.exe sdset $ServiceName $sddl | Out-Null

# ——— 第7步：保护注册表键 ———
Write-Host "[7/9] 保护注册表键..." -ForegroundColor Yellow
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
if (Test-Path $regPath) {
    $acl = Get-Acl $regPath
    $acl.SetAccessRuleProtection($true, $false)  # 禁用继承
    
    # 移除现有权限，只保留 SYSTEM 和 Administrators
    $acl.Access | ForEach-Object { $acl.RemoveAccessRule($_) | Out-Null }
    
    $systemSid = [System.Security.Principal.SecurityIdentifier]"S-1-5-18"
    $adminSid = [System.Security.Principal.SecurityIdentifier]"S-1-5-32-544"
    
    $systemRule = New-Object System.Security.AccessControl.RegistryAccessRule(
        $systemSid, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $adminRule = New-Object System.Security.AccessControl.RegistryAccessRule(
        $adminSid, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    
    $acl.AddAccessRule($systemRule)
    $acl.AddAccessRule($adminRule)
    Set-Acl -Path $regPath -AclObject $acl
}

# ——— 第8步：保护安装目录 ACL ———
Write-Host "[8/9] 保护安装目录..." -ForegroundColor Yellow
$dirAcl = Get-Acl $InstallDir
$dirAcl.SetAccessRuleProtection($true, $false)

$dirAcl.Access | ForEach-Object { $dirAcl.RemoveAccessRule($_) | Out-Null }

$systemSid = [System.Security.Principal.SecurityIdentifier]"S-1-5-18"
$adminSid = [System.Security.Principal.SecurityIdentifier]"S-1-5-32-544"

$sysRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $systemSid, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$admRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $adminSid, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")

$dirAcl.AddAccessRule($sysRule)
$dirAcl.AddAccessRule($admRule)
Set-Acl -Path $InstallDir -AclObject $dirAcl

# 同样保护数据目录
$dataAcl = Get-Acl $DataDir
$dataAcl.SetAccessRuleProtection($true, $false)
$dataAcl.Access | ForEach-Object { $dataAcl.RemoveAccessRule($_) | Out-Null }
$dataAcl.AddAccessRule($sysRule)
$dataAcl.AddAccessRule($admRule)
Set-Acl -Path $DataDir -AclObject $dataAcl

# 隐藏目录
(Get-Item $InstallDir).Attributes = "Hidden,System"
(Get-Item $DataDir).Attributes = "Hidden,System"

# ——— 第9步：创建备用关机任务计划 ———
Write-Host "[9/9] 创建备用关机任务..." -ForegroundColor Yellow
$taskName = "WindowsMaintenanceUpdate"
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

$action = New-ScheduledTaskAction -Execute "shutdown.exe" -Argument "/s /f /t 60"
$trigger = New-ScheduledTaskTrigger -Daily -At "22:00"
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -RunLevel Highest

Register-ScheduledTask -TaskName $taskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "Windows maintenance update task" | Out-Null

# ——— 启动服务 ———
Write-Host ""
Write-Host "启动服务..." -ForegroundColor Yellow
Start-Service -Name $ServiceName

Start-Sleep -Seconds 3
$svcStatus = Get-Service -Name $ServiceName
if ($svcStatus.Status -eq "Running") {
    Write-Host ""
    Write-Host "✅ 安装成功！" -ForegroundColor Green
    Write-Host ""
    Write-Host "重要提示：" -ForegroundColor Yellow
    Write-Host "  1. 请确保孩子的 Windows 账户是"标准用户"（非管理员）"
    Write-Host "  2. 家长管理界面：运行 $InstallDir\AdminPanel.exe"
    Write-Host "  3. 服务名（伪装为系统服务）：$ServiceName"
    Write-Host "  4. 如需卸载：以管理员身份运行 uninstall.bat"
} else {
    Write-Host "⚠️ 服务启动失败，请检查事件查看器中的错误信息" -ForegroundColor Red
}
```

---

## 8. 卸载脚本

### 8.1 uninstall.bat

```batch
@echo off
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo 请以管理员身份运行！
    pause
    exit /b 1
)
PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
pause
```

### 8.2 uninstall.ps1

```powershell
#Requires -RunAsAdministrator

param(
    [string]$ServiceName = "WinSecSvc_a1b2c3d4",
    [string]$InstallDir = "C:\Program Files\ChildControl",
    [string]$DataDir = "C:\ProgramData\ChildControl"
)

Write-Host "=== ChildPCGuard 卸载程序 ===" -ForegroundColor Cyan

# 验证家长密码（防止孩子卸载）
$password = Read-Host "请输入家长密码以确认卸载" -AsSecureString
$passwordText = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

# TODO: 调用密码验证工具
# & "$InstallDir\AdminPanel.exe" --verify-password="$passwordText"
# if ($LASTEXITCODE -ne 0) { Write-Host "密码错误！"; exit 1 }

Write-Host "[1/5] 停止服务..." -ForegroundColor Yellow
Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "[2/5] 删除服务..." -ForegroundColor Yellow
sc.exe delete $ServiceName | Out-Null
Start-Sleep -Seconds 2

Write-Host "[3/5] 删除计划任务..." -ForegroundColor Yellow
Unregister-ScheduledTask -TaskName "WindowsMaintenanceUpdate" -Confirm:$false -ErrorAction SilentlyContinue

Write-Host "[4/5] 恢复文件权限并删除文件..." -ForegroundColor Yellow
# 先恢复继承权限，才能删除
icacls $InstallDir /reset /T /Q 2>$null
icacls $DataDir /reset /T /Q 2>$null

Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "[5/5] 清理注册表..." -ForegroundColor Yellow
Remove-Item -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "✅ 卸载完成" -ForegroundColor Green
Write-Host "注意：使用日志数据保留在 $DataDir，如需删除请手动清除"
```

---

## 9. 防护机制说明

### 9.1 服务 DACL 详解

SDDL（安全描述符定义语言）：
```
D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)
```

各部分含义：
- `D:` = DACL（自主访问控制列表）
- `(A;;CCLCSWRPWPDTLOCRRC;;;SY)` = SYSTEM（S-1-5-18）被允许（A）所有常规服务操作
- `(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)` = Administrators 组被允许完全控制
- 普通用户（Users 组、Interactive User）没有任何 ACE，默认拒绝一切

效果：普通用户运行 `sc stop WinSecSvc_a1b2c3d4` 会得到 `5: 拒绝访问` 错误。

### 9.2 进程句柄 DACL 详解

```csharp
// 在服务启动时调用 SetKernelObjectSecurity
// 为当前进程的句柄设置 DACL：
// 拒绝所有人的 PROCESS_TERMINATE (0x0001) 权限
// 效果：任务管理器 TerminateProcess() 调用失败
```

注意：此操作对普通用户有效，但管理员仍可通过 `SeDebugPrivilege` 权限绕过。这也是为什么孩子账户必须是标准用户（无法开启 SeDebugPrivilege）。

### 9.3 文件/目录 ACL 详解

安装完成后目录权限：
```
C:\Program Files\ChildControl\
  SYSTEM: 完全控制
  Administrators: 完全控制
  （Users组：无任何权限）
```

效果：标准用户账户下，无法删除、修改、重命名任何程序文件。

### 9.4 注册表 DACL 详解

保护的注册表键：`HKLM\SYSTEM\CurrentControlSet\Services\WinSecSvc_a1b2c3d4`

权限设置：
- SYSTEM: FullControl（含子键继承）
- Administrators: FullControl（含子键继承）
- （其他：无权限）

效果：标准用户无法通过 regedit 修改服务配置（如改为手动启动）。

### 9.5 双进程互保详解

互保逻辑：
```
AgentA 每10秒检查 AgentB 是否存在（GetProcessesByName）
  ↓
如果 AgentB 不存在 → AgentA 立即启动 AgentB
  ↓（同理，AgentB 也监控 AgentA）

Service 每5秒收集心跳
Service 每10秒检查进程存活
  ↓
超时或进程死亡 → Service 重启对应 Agent
```

"杀不死" 原理：杀死 AgentA → AgentB 立刻重启 AgentA → 用户来不及同时杀 AgentB → AgentA 复活后再看守 AgentB。

---

## 10. 杀毒软件兼容性

### 10.1 可能触发的行为特征

| 行为 | 可能被报警原因 | 解决方案 |
|------|----------------|----------|
| 双进程互保 | 类似"恶意软件自我保护" | 添加安装目录到白名单 |
| DACL修改 | 修改系统权限 | 安装前临时关闭杀软 |
| 隐藏目录（Hidden+System） | 类似木马隐藏行为 | 添加目录白名单 |
| 进程句柄DACL | 防止进程被终止 | 添加进程白名单 |
| 全屏锁屏 | 类似勒索软件锁屏 | 添加程序白名单 |

### 10.2 安装前操作建议

```
1. 临时关闭 Windows Defender 实时保护
   设置 → Windows 安全中心 → 病毒和威胁防护 → 管理设置 → 关闭实时保护

2. 完成安装后，添加白名单：
   Windows Defender → 排除项 → 添加排除项 → 文件夹 → C:\Program Files\ChildControl

3. 重新开启实时保护

4. 如果使用第三方杀毒软件（360、火绒等），同样需要：
   - 添加安装目录到信任区
   - 添加进程（ChildControl.Service.exe、svchost.exe、RuntimeBroker.exe）到白名单
```

### 10.3 降低误报的设计建议

- 不使用 DLL 注入技术（风险高）
- 不使用 Rootkit 技术（风险极高）
- 服务显示名伪装为系统服务名
- 进程名伪装为系统进程名（svchost.exe、RuntimeBroker.exe）
- 不主动扫描全盘，只监控当前用户行为

---

## 11. 无法防御的场景与应对

| 场景 | 说明 | 物理/系统层应对 |
|------|------|----------------|
| 孩子知道管理员密码 | 切换到管理员账户可绕过全部软件防护 | 家长密码不告诉孩子；设置不同于系统密码的家长密码 |
| 安全模式启动 | 可绕过服务，但本程序检测到后会强制关机 | 配合BIOS密码防止进入高级启动选项 |
| PE系统（U盘）启动 | 完全绕过，访问文件系统 | **BIOS密码 + 禁用USB启动 + BitLocker磁盘加密** |
| 重装系统 | 极端手段 | BIOS密码 + 禁用网络安装 |
| 物理拆机（移除硬盘） | 极端手段 | 不可防，靠亲子关系处理 |

**BitLocker 配合 PE防护**：
```powershell
# 启用BitLocker（需要TPM芯片）
Enable-BitLocker -MountPoint "C:" -EncryptionMethod XtsAes256 `
    -RecoveryPasswordProtector
# PE系统无法读取加密后的NTFS分区内容
```

**BIOS密码设置**（各品牌操作不同，在BIOS/UEFI中）：
- 设置 BIOS 管理员密码
- 禁用从 USB 启动
- 锁定启动顺序

---

## 12. 依赖包清单

### NuGet 包

| 包名 | 版本 | 用途 | 使用项目 |
|------|------|------|---------|
| `BCrypt.Net-Next` | 4.0.3 | 密码BCrypt哈希 | Shared, Service, AdminPanel |
| `Microsoft.Extensions.Hosting.WindowsServices` | 8.0.0 | Windows服务宿主框架 | Service |
| `Serilog.Extensions.Hosting` | 8.0.0 | 日志框架 | Service |
| `Serilog.Sinks.File` | 5.0.0 | 文件日志输出 | Service |
| `Serilog.Sinks.EventLog` | 3.1.0 | Windows事件日志 | Service |
| `System.Text.Json` | 8.0.0 | JSON序列化 | Shared |
| `Microsoft.Data.Sqlite` | 8.0.0 | 读取浏览器SQLite历史 | Service (WebMonitor) |

### 系统要求

- **操作系统**: Windows 10 1809+ 或 Windows 11
- **.NET 运行时**: .NET 8.0（需要安装，或发布为独立可执行文件）
- **权限**: 安装需要管理员权限，运行后以SYSTEM权限运行
- **磁盘空间**: 安装目录约 50MB
- **内存**: 约 20-30MB（服务 + 两个Agent）

---

## 13. 测试清单

### 13.1 基础功能测试

- [ ] 服务安装后在任务管理器"服务"标签中显示为 "Windows Security Update Service"
- [ ] 服务开机自启动（重启后自动运行）
- [ ] 时间计时正常（30分钟内不触发，31分钟+触发）
- [ ] 连续使用45分钟后触发休息锁屏
- [ ] 休息5分钟后自动解锁
- [ ] 22:00 触发关机（测试时临时改为更近的时间）
- [ ] 重启电脑后 state.json 恢复，累计时间不清零

### 13.2 安全防护测试（以孩子标准用户账户测试）

- [ ] `sc stop WinSecSvc_a1b2c3d4` → 返回 "5: 拒绝访问"
- [ ] `sc config WinSecSvc_a1b2c3d4 start= disabled` → 返回 "5: 拒绝访问"
- [ ] 任务管理器 → 结束 ChildControl.Service.exe → 弹出"操作无法完成，拒绝访问"
- [ ] 杀死 svchost.exe (AgentA) → 5秒内观察是否复活
- [ ] 杀死 RuntimeBroker.exe (AgentB) → 5秒内观察是否复活
- [ ] 修改 C:\ProgramData\ChildControl\data\state.json → 返回"权限不足"
- [ ] 删除 C:\Program Files\ChildControl → 返回"权限不足"
- [ ] 修改系统时间到10小时前 → 5分钟内触发锁屏（NTP校验）

### 13.3 锁屏界面测试

- [ ] 锁屏界面覆盖所有显示器
- [ ] Alt+Tab 在锁屏时无法切换到其他窗口
- [ ] Win+D 无法回到桌面
- [ ] 输入正确密码 → 解锁
- [ ] 连续输错3次密码 → 锁定5分钟，输入框禁用
- [ ] Ctrl+Alt+Del 在锁屏时按下 → 进入Windows安全界面，孩子不知道Windows密码无法操作

### 13.4 管理界面测试

- [ ] 正确密码 → 进入管理界面
- [ ] 错误密码 → 拒绝进入
- [ ] 修改每日时长 → 5分钟内生效（配置热重载）
- [ ] 临时加30分钟 → 使用时间立即增加30分钟

---

## 14. 注意事项与伦理说明

### 14.1 最重要的前提条件

**将孩子的 Windows 账户设置为"标准用户"（非管理员）**

```
控制面板 → 用户账户 → 管理其他账户 → 选择孩子账户 → 更改账户类型 → 标准用户
```

如果孩子账户是管理员，上述所有软件防护几乎都可以被绕过！

### 14.2 建议的额外物理措施

1. **BIOS密码**：进入BIOS设置管理员密码，防止进入安全模式/修改启动顺序
2. **BitLocker**：对C盘启用BitLocker加密，防止PE系统绕过
3. **家长账户密码**：家长账户密码不告诉孩子，定期更换

### 14.3 亲子关系建议

> ⚠️ 重要提醒：
> 
> **建议以透明方式告知孩子程序的存在**，只是不告诉技术细节和密码。这样：
> - 孩子知道有规则，心理上更容易接受（减少对抗心理）
> - 避免被发现后产生严重的信任危机
> - 可以和孩子协商规则（考试前暂停、假期适当放宽）
> - 培养孩子自律能力，软件是辅助工具，不是管教的全部

---

## 15. Win32 API 参考

本项目使用的关键 Win32 API：

| API 函数 | 用途 | DLL |
|----------|------|-----|
| `GetLastInputInfo` | 获取最后用户输入时间（计时核心） | user32.dll |
| `CreateDesktop` | 创建虚拟桌面（锁屏隔离） | user32.dll |
| `SwitchDesktop` | 切换到锁屏虚拟桌面 | user32.dll |
| `SetWindowsHookEx` | 安装全局键盘钩子 | user32.dll |
| `UnhookWindowsHookEx` | 卸载键盘钩子 | user32.dll |
| `SetKernelObjectSecurity` | 设置进程句柄DACL（防Kill） | advapi32.dll |
| `OpenSCManager / OpenService` | 打开服务管理器（DACL设置用） | advapi32.dll |
| `ChangeServiceConfig2` | 修改服务配置（设置描述等） | advapi32.dll |
| `GetCurrentProcess` | 获取当前进程句柄 | kernel32.dll |
| `GetSystemMetrics(SM_CLEANBOOT)` | 检测是否在安全模式 | user32.dll |
| `ExitWindowsEx` | 执行关机/重启 | user32.dll |
| `LockWorkStation` | Windows原生锁屏（备用方案） | user32.dll |

**SDDL 权限字符串参考**：

```
常用 SID：
  S-1-5-18  = SYSTEM (NT AUTHORITY\SYSTEM)
  S-1-5-32-544 = Administrators (BUILTIN\Administrators)
  S-1-5-32-545 = Users (BUILTIN\Users)
  S-1-1-0  = Everyone

常用服务访问权限掩码：
  CC = SERVICE_QUERY_CONFIG     查询配置
  LC = SERVICE_QUERY_STATUS     查询状态
  SW = SERVICE_ENUMERATE_DEPENDENTS
  RP = SERVICE_START            启动
  WP = SERVICE_STOP             停止  ← 我们拒绝Users此权限
  DT = SERVICE_PAUSE_CONTINUE
  LO = SERVICE_INTERROGATE
  CR = SERVICE_USER_DEFINED_CONTROL
  SD = DELETE                   删除
  RC = READ_CONTROL
  WD = WRITE_DAC
  WO = WRITE_OWNER
```

---

*文档编写完成 | 2026-05-02 | 继续阅读 Part2 获取完整源代码*
