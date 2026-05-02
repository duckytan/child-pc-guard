# ChildPCGuard — 儿童电脑使用时间管控程序 · 完整项目方案

> **项目代号**：ChildPCGuard  
> **文档版本**：v1.0  
> **目标平台**：Windows 10 / Windows 11  
> **目标用户**：家长（管理方）+ 初中生（被管控方，有一定电脑操作能力）  
> **文档目的**：本文档为唯一完整参考文档，其他 AI 或开发者只需阅读本文档即可完整开发整个项目，无需任何其他资料。

---

## 目录

1. [项目背景与目标](#1-项目背景与目标)
2. [用户画像与威胁模型](#2-用户画像与威胁模型)
3. [功能需求清单](#3-功能需求清单)
4. [系统整体架构](#4-系统整体架构)
5. [技术选型与依赖](#5-技术选型与依赖)
6. [模块详细设计](#6-模块详细设计)
7. [数据结构设计](#7-数据结构设计)
8. [防护体系设计（12层）](#8-防护体系设计12层)
9. [UI/UX 设计规范](#9-uiux-设计规范)
10. [安装与部署方案](#10-安装与部署方案)
11. [开发路线图](#11-开发路线图)
12. [测试用例与验收标准](#12-测试用例与验收标准)
13. [风险与局限性说明](#13-风险与局限性说明)
14. [参考资料与开源项目](#14-参考资料与开源项目)

---

## 1. 项目背景与目标

### 1.1 背景

市面上现有的家长控制软件（微软家庭安全、Qustodio、Net Nanny 等）存在以下问题：
- 商业软件收费且需联网账号
- 孩子明知是"家长控制软件"，心理抵触强
- 对有一定电脑基础的孩子（会任务管理器、会 `sc stop`）形同虚设
- GitHub 上针对 Windows 且具备自我保护能力的开源项目**几乎为零**

### 1.2 项目目标

开发一款运行在 Windows 10/11 上的**后台守护程序**，实现：
- 每日使用时长限制（工作日/周末可分别配置）
- 超时自动锁屏，需输入家长密码才能解锁
- 每天固定时间自动关机
- **核心亮点**：具备企业级自我保护能力，对抗有一定电脑基础的孩子

### 1.3 设计原则

| 原则 | 说明 |
|------|------|
| 隐秘性 | 程序以系统服务形式运行，外观伪装为系统进程，不在任务栏显示 |
| 健壮性 | 崩溃/重启后自动恢复，双进程互保 |
| 可管理性 | 家长通过图形化界面轻松管理，无需命令行 |
| 最小权限 | 普通用户（孩子账户）为标准用户，无管理员权限 |
| 透明沟通 | 建议家长告知孩子程序存在，但不告知技术细节和密码 |

---

## 2. 用户画像与威胁模型

### 2.1 被管控方画像（孩子）

- 年龄：初中生，13-15 岁
- 电脑经验：使用约 5 年，会以下操作：
  - 任务管理器 → 结束进程
  - `sc stop` / `net stop` 停止服务
  - 搜索"如何绕过家长控制"
  - 修改系统时间
  - 可能尝试安全模式启动

### 2.2 威胁模型（攻击向量 → 防御方案）

| 攻击手段 | 危险等级 | 应对方案 |
|----------|----------|----------|
| 任务管理器结束进程 | ★★★★★ | SYSTEM 权限 + 进程句柄 DACL + 双进程互保 |
| `sc stop` 停止服务 | ★★★★★ | 服务 DACL 拒绝非管理员操作 |
| 删除/修改程序文件 | ★★★★ | 安装目录 ACL，仅 SYSTEM/Admins 可写 |
| 修改系统时间 | ★★★★ | NTP 网络时间校验，差异 > 5 分钟即锁屏 |
| 修改配置文件 | ★★★★ | 配置文件 ACL 保护 + AES-256 加密 |
| 重启电脑 | ★★★ | 开机自启，重启后恢复累计计时 |
| 注册表修改启动项 | ★★★ | 注册表 DACL 保护 |
| 以管理员运行 cmd | ★★★ | **前提**：孩子账户必须是标准用户 |
| 安全模式启动 | ★★★ | 检测安全模式，立即关机 |
| PE 系统启动 | ★★★ | BitLocker + PE 环境检测（配合 BIOS 密码） |
| 虚拟机绕过 | ★★ | 虚拟机特征检测，触发锁屏 |
| 搜索反制工具 | ★★ | 可选：应用黑名单 + 网站过滤 |
| 重装系统 | ★ | BIOS/UEFI 密码 + 禁用 USB 启动（硬件层面） |

### 2.3 最关键的前提条件

> ⚠️ **将孩子的 Windows 账户设置为"标准用户"（非管理员）**  
> 路径：控制面板 → 用户账户 → 管理其他账户 → 更改账户类型 → 标准  
> 这是整个方案的基石，没有这一步，所有软件防护均可被绕过。

---

## 3. 功能需求清单

### 3.1 核心功能（P0，必须实现）

| 需求编号 | 需求描述 | 验收标准 |
|----------|----------|----------|
| R-01 | 每日使用时长限制，工作日/周末分别配置 | 达到时长上限后 60 秒内触发锁屏 |
| R-02 | 超时自动锁屏，全屏覆盖，无法操作桌面 | 锁屏后所有桌面操作被拦截 |
| R-03 | 锁屏界面支持家长密码解锁 | 输入正确密码后恢复正常使用 |
| R-04 | 每天固定时间（默认 22:00）自动关机 | 到点发出 60 秒倒计时警告后关机 |
| R-05 | 程序以 Windows 服务方式后台运行，不显示在任务栏 | 孩子账户任务栏无图标，通知区域无图标 |
| R-06 | 普通用户账户无法通过任务管理器结束进程 | 孩子账户点击"结束任务"后进程仍在运行 |
| R-07 | 普通用户无法通过 `sc stop` 停止服务 | 执行 `sc stop` 返回"拒绝访问" |
| R-08 | 意外崩溃或系统重启后自动恢复并继续计时 | 重启后 30 秒内服务自动启动并读取当日已用时长 |

### 3.2 重要功能（P1，强烈建议实现）

| 需求编号 | 需求描述 | 说明 |
|----------|----------|------|
| R-09 | 家长图形化管理界面（AdminPanel） | 修改规则、查看日志、临时解锁 |
| R-10 | 锁屏前提前 10/5/1 分钟弹出桌面气泡通知 | Toast 通知，显示倒计时 |
| R-11 | 防修改系统时间（NTP 校验） | 时间差 > 5 分钟触发锁屏并记录违规日志 |
| R-12 | 锁屏界面拦截快捷键（Alt+Tab、Win 键、Ctrl+Esc 等） | 全局键盘钩子，捕获并屏蔽以上按键 |
| R-13 | 密码错误 3 次后锁定 5 分钟 | 防止暴力破解密码 |
| R-14 | 记录每日使用日志（使用时长、违规尝试） | 日志存储在受保护目录，家长可查 |
| R-15 | 支持"临时追加时间"（如再玩 30 分钟） | 家长在管理界面一键操作 |

### 3.3 增强功能（P2，可选实现）

| 需求编号 | 需求描述 | 说明 |
|----------|----------|------|
| R-16 | 双进程互保（WatchDog 机制） | 两个 Agent 进程互相监控，被杀则复活 |
| R-17 | 应用程序黑名单 | 检测到黑名单进程立即强制关闭 |
| R-18 | 网站访问记录（浏览器历史读取） | 仅记录，不拦截，供家长查看 |
| R-19 | 连续使用时长限制 + 强制休息 | 如连续使用 45 分钟后强制休息 5 分钟 |
| R-20 | 安全模式防护 | 检测安全模式启动，立即强制关机 |
| R-21 | 虚拟机检测 | 检测 VMware/VirtualBox/Hyper-V 环境 |
| R-22 | 配置文件 AES-256 加密 | 防止孩子直接修改 JSON 配置 |
| R-23 | 紧急解锁快捷键 | 家长在孩子旁边时可用快捷键（如 Ctrl+Alt+Shift+F12 × 5）快速呼出解锁 |
| R-24 | 多屏幕支持 | 锁屏覆盖所有显示器 |
| R-25 | 使用统计图表 | 管理界面显示按周/月的使用趋势图 |

---

## 4. 系统整体架构

### 4.1 架构概览图

```
┌─────────────────────────────────────────────────────────────────┐
│                        ChildPCGuard 系统架构                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                   [家长管理层]                            │    │
│  │   AdminPanel.exe（图形化管理界面，需密码验证）             │    │
│  │   - 修改时间规则  - 查看使用日志  - 临时解锁               │    │
│  └─────────────────────────┬───────────────────────────────┘    │
│                             │ Named Pipe（命名管道 IPC）          │
│  ┌─────────────────────────▼───────────────────────────────┐    │
│  │                   [核心服务层]                            │    │
│  │   GuardService.exe（Windows 服务，SYSTEM 权限运行）        │    │
│  │   - TimeTracker：实际使用时间统计（GetLastInputInfo）      │    │
│  │   - RuleEngine：规则判断（时长/时段/关机时间）              │    │
│  │   - ShutdownScheduler：22:00 自动关机                     │    │
│  │   - NtpValidator：网络时间校验                            │    │
│  │   - NotificationHelper：倒计时气泡提醒                    │    │
│  │   - PipeServer：接收 AdminPanel 指令                      │    │
│  └──────────┬──────────────────────┬────────────────────────┘    │
│             │ 启动/监控              │ 启动                        │
│  ┌──────────▼──────────┐  ┌────────▼──────────────────────┐    │
│  │  [双进程互保层]       │  │  [锁屏覆盖层]                  │    │
│  │  AgentA.exe         │  │  LockOverlay.exe               │    │
│  │  AgentB.exe         │  │  （由 GuardService 启动）        │    │
│  │  - 互相心跳检测      │  │  - 虚拟桌面锁屏                 │    │
│  │  - 任一死亡立即复活  │  │  - 全屏覆盖 + TopMost           │    │
│  │  - 进程名伪装        │  │  - 全局键盘钩子                 │    │
│  └─────────────────────┘  │  - PIN 密码验证                 │    │
│                            │  - 错误 3 次锁定 5 分钟          │    │
│                            └────────────────────────────────┘    │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                   [数据持久化层]                          │    │
│  │   C:\ProgramData\ChildPCGuard\                           │    │
│  │   ├── config.bin      （AES-256 加密配置，含规则+密码哈希）│    │
│  │   ├── state.json      （今日累计使用时长，重启后恢复）     │    │
│  │   └── logs\           （每日使用日志 + 安全违规日志）     │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 进程清单

| 进程名（实际文件名） | 运行身份 | 作用 | 是否显示 |
|---------------------|----------|------|----------|
| `GuardService.exe` | LocalSystem（SYSTEM） | 核心调度服务，Windows 服务宿主 | 不显示（后台服务） |
| `AgentA.exe`（伪装为 `svchost.exe` 副本） | LocalSystem | 双进程互保 Agent A | 不显示 |
| `AgentB.exe`（伪装为 `RuntimeBroker.exe` 副本） | LocalSystem | 双进程互保 Agent B | 不显示 |
| `LockOverlay.exe` | LocalSystem | 全屏锁屏覆盖层，按需启动 | 仅锁屏时全屏显示 |
| `AdminPanel.exe` | 当前登录用户（家长） | 图形化管理界面，密码验证后使用 | 家长主动打开时显示 |

### 4.3 组件交互时序

```
开机 → GuardService 自启动
     → 读取 config.bin + state.json
     → 初始化所有防护（DACL/ACL/NTP校验）
     → 启动 AgentA + AgentB（双进程互保）
     → 进入主循环（每 5 秒 tick）
          │
          ├─ 检查 NTP 时间是否被篡改 → 是：立即锁屏
          ├─ 检查当前是否在允许时段 → 否：立即锁屏
          ├─ 累计 GetLastInputInfo 活跃时间
          ├─ 检查是否达到时长上限 → 是：触发锁屏流程
          ├─ 检查是否到达 22:00 → 是：触发关机流程
          └─ 检查 10/5/1 分钟预警 → 是：发送 Toast 通知

锁屏流程：
  GuardService → 启动 LockOverlay.exe（虚拟桌面）
               → LockOverlay 全屏覆盖 + 键盘钩子
               → 等待密码输入
               → 验证通过 → 通过 Named Pipe 通知 GuardService 解锁
               → GuardService 销毁 LockOverlay 进程
```

---

## 5. 技术选型与依赖

### 5.1 技术栈

| 组件 | 选用技术 | 版本要求 | 理由 |
|------|----------|----------|------|
| 核心语言 | **C#** | .NET 8（LTS） | Windows 原生 API 支持最佳，Windows Service 框架成熟，同类开源项目均采用 |
| 服务框架 | **.NET Worker Service** | .NET 8 | 微软官方 BackgroundService，天然支持 Windows 服务注册 |
| 锁屏 UI | **WPF（XAML）** | .NET 8 | 现代化 UI，支持虚拟桌面（CreateDesktop/SwitchDesktop）方案 |
| 管理界面 | **WinForms 或 WPF** | .NET 8 | 本地桌面 UI，无需 Web 服务器，开发简单 |
| 配置存储 | **System.Text.Json + AES-256** | 内置 | JSON 可读性好，加密后防篡改 |
| 密码哈希 | **BCrypt.Net-Next** | NuGet：4.x | BCrypt 不可逆哈希，比 MD5/SHA 安全得多 |
| 日志 | **Serilog** | NuGet：3.x | 轻量、支持滚动文件、结构化日志 |
| 进程间通信 | **Named Pipe（命名管道）** | 内置 | 稳定、权限可控、跨进程通信标准方案 |
| 时间校验 | **NTP 协议（UDP 123 端口）** | 自实现 | 防系统时间篡改 |
| 安装 | **PowerShell 脚本** | PowerShell 5.1+ | 无需额外依赖，可完整配置 ACL/DACL/服务注册 |

### 5.2 NuGet 包依赖

| 包名 | 版本 | 用途 |
|------|------|------|
| `BCrypt.Net-Next` | 4.0.3 | 密码 BCrypt 哈希 |
| `Serilog` | 3.1.1 | 结构化日志 |
| `Serilog.Sinks.File` | 5.0.0 | 日志写入文件 |
| `Serilog.Extensions.Hosting` | 8.0.0 | 与 .NET Host 集成 |
| `Microsoft.Extensions.Hosting.WindowsServices` | 8.0.0 | Windows 服务支持 |
| `System.ServiceProcess.ServiceController` | 8.0.0 | 服务控制 |

### 5.3 系统 API 依赖（Win32 P/Invoke）

| API 函数 | 用途 | DLL |
|----------|------|-----|
| `GetLastInputInfo` | 获取用户最后输入时间，判断是否活跃 | `user32.dll` |
| `CreateDesktop` / `SwitchDesktop` | 创建虚拟桌面用于隔离锁屏 | `user32.dll` |
| `SetWindowsHookEx` / `UnhookWindowsHookEx` | 全局键盘钩子，拦截 Alt+Tab 等 | `user32.dll` |
| `SetKernelObjectSecurity` | 设置进程/内核对象 DACL | `kernel32.dll` |
| `OpenSCManager` / `OpenService` | 打开服务控制管理器 | `advapi32.dll` |
| `ChangeServiceConfig2` | 修改服务配置（描述/故障恢复） | `advapi32.dll` |
| `LockWorkStation` | 系统原生锁屏（补充方案） | `user32.dll` |
| `GetSystemMetrics(SM_CLEANBOOT)` | 检测是否安全模式 | `user32.dll` |
| `RegisterHotKey` | 注册全局热键（管理员呼出快捷键） | `user32.dll` |

### 5.4 开发环境要求

- Visual Studio 2022（或 JetBrains Rider）
- .NET 8 SDK
- Windows 10/11 开发机
- 管理员权限（调试服务时需要）

---

## 6. 模块详细设计

### 6.1 模块 GuardService（核心守护服务）

**类型**：Windows 服务（.NET Worker Service）  
**运行权限**：LocalSystem（SYSTEM）  
**启动类型**：Automatic（开机自启）

**主循环逻辑**（每 5 秒执行一次）：

```
while 服务运行中:
    1. NTP 校验当前时间（每 5 分钟刷新一次缓存）
       → 时间偏差 > 5 分钟：记录违规日志 + 触发锁屏
    
    2. 检查当前是否在允许时段内（allowedTimeWindows）
       → 不在时段：触发锁屏
    
    3. 读取 GetLastInputInfo，判断用户是否活跃（空闲 < 5 秒）
       → 活跃：累计今日使用时长
       → 空闲：暂停计时
    
    4. 判断是否达到今日时长上限（dailyLimitMinutes）
       → 达到：触发锁屏
    
    5. 检查是否到达 autoShutdownTime（默认 22:00）
       → 到达：发送 60 秒关机警告，然后执行 shutdown.exe /s /f /t 60
    
    6. 检查倒计时预警（warningMinutes: [10, 5, 1]）
       → 剩余时间等于预警点：发送 Toast 通知
    
    7. 持久化 state.json（当前日期 + 今日已用时长）
    
    等待 5 秒
```

**关键子逻辑——计时恢复**：
- 每次服务启动时，读取 `state.json`
- 如果记录的日期 = 今天，则恢复 `usedMinutesToday`
- 如果记录的日期 < 今天，则重置为 0（新的一天）

**锁屏触发逻辑**：
- GuardService 通过 `Process.Start("LockOverlay.exe")` 启动锁屏进程
- 同时将锁屏原因写入日志（时长超限 / 时段外 / 时间篡改）
- 轮询检查 LockOverlay 进程是否存活，若被杀死则立即重新启动

---

### 6.2 模块 TimeTracker（时间追踪器）

**核心算法**：使用 `GetLastInputInfo` API 获取系统最后输入时间

- 每次 tick 时，获取 `idleTime = Environment.TickCount - lastInputInfo.dwTime`
- 如果 `idleTime < idleThresholdMs`（默认 5000ms），则认为用户正在使用
- 将本次 tick 的时间（5 秒）计入今日使用时长
- 优点：孩子去吃饭时不计时，比"登录就计时"更准确

**跨天处理**：
- 每次 tick 时检查日期是否切换（0:00 后）
- 切换则自动重置今日时长为 0

---

### 6.3 模块 RuleEngine（规则引擎）

负责判断"当前状态下是否应该锁屏"，输出 `LockReason` 枚举：

| LockReason | 触发条件 |
|------------|----------|
| `DailyLimitReached` | 今日使用时长 ≥ 每日上限 |
| `OutsideAllowedWindow` | 当前时间不在允许时段列表内 |
| `TimeTampered` | NTP 时间偏差 > 5 分钟 |
| `ManualLock` | 家长通过 AdminPanel 主动锁屏 |
| `AutoShutdown` | 到达关机时间，先锁屏再关机 |

**规则加载**：从 `config.bin` 解密读取，支持热重载（服务运行中修改规则立即生效）

---

### 6.4 模块 LockOverlay（锁屏覆盖层）

**实现方式**：虚拟桌面方案（推荐）或全屏窗体方案（备选）

#### 方案 A：虚拟桌面方案（强烈推荐）

- 调用 `CreateDesktop()` 创建一个新的 Windows 虚拟桌面
- 调用 `SwitchDesktop()` 切换到锁屏桌面
- 在锁屏桌面上渲染锁屏界面（WPF 全屏窗口）
- 效果：原桌面完全不可见，任务管理器也无法从锁屏桌面切换回去
- 即使按下 Ctrl+Alt+Del，也无法回到原桌面

#### 方案 B：全屏窗体方案（备选）

- WPF/WinForms 全屏置顶窗口
- `Topmost = true`，`WindowState = Maximized`，无边框
- 安装全局键盘钩子（`SetWindowsHookEx + WH_KEYBOARD_LL`）拦截：
  - `Alt+F4`
  - `Alt+Tab`
  - `Win 键`（VK_LWIN / VK_RWIN）
  - `Ctrl+Esc`
  - `Ctrl+Alt+Del`（无法拦截，内核级别，但可配合 Windows 账户密码保护）
- 注意：Ctrl+Alt+Del 无法被用户态程序拦截，这是 Windows 安全设计

**锁屏界面内容**：
- 全屏纯色或模糊背景
- 显示当前时间（来自 NTP 校验后的时间）
- 显示锁屏原因（如"今日使用时长已达上限"）
- 密码输入框（PIN，4-8 位数字或字母数字混合）
- 错误次数提示
- 错误 3 次后锁定 5 分钟，显示倒计时

**解锁流程**：
- 用户输入密码 → LockOverlay 验证（对比 BCrypt 哈希）
- 验证通过 → 通过 Named Pipe 发送 `UNLOCK` 指令给 GuardService
- GuardService 收到后：销毁 LockOverlay 进程，切换回原桌面，可选追加宽限时间

---

### 6.5 模块 AdminPanel（管理员界面）

**访问方式**（多种备选）：
- 方式 A：系统托盘隐藏图标，家长注册全局热键（如 `Ctrl+Shift+Alt+P`）呼出
- 方式 B：运行命令 `AdminPanel.exe`（放在任意路径或通过快捷方式）
- 方式 C：内嵌 Web 管理界面（localhost：端口号）
- **推荐**：方式 A + 方式 B 双通道

**界面功能**：

| 界面模块 | 功能 |
|----------|------|
| 登录页 | BCrypt 密码验证，3 次失败锁定 5 分钟 |
| 首页/状态 | 今日已用时长、剩余时长、服务状态、最后解锁时间 |
| 规则设置 | 工作日时长上限（分钟）、周末时长上限、允许时段（支持多个时段）、关机时间 |
| 临时操作 | 追加 N 分钟、暂停管控（需设置恢复时间）、立即锁屏、立即关机 |
| 日志查看 | 按日期查看使用记录，违规尝试（时间篡改/暴力破解）记录 |
| 黑名单管理 | 添加/删除应用黑名单（进程名），添加/删除网站记录（可选） |
| 密码修改 | 修改解锁密码（BCrypt 重新哈希后写入 config.bin） |
| 关于 | 程序版本、服务状态、当前配置摘要 |

---

### 6.6 模块 AgentA/AgentB（双进程互保）

**机制**：两个独立的可执行文件，互相监控对方进程是否存活

- AgentA 每 10 秒检查 AgentB 的进程 ID 是否存在
- AgentB 每 10 秒检查 AgentA 的进程 ID 是否存在
- 任意一方发现对方已死亡，立即通过系统调用重新启动对方
- 两者均由 GuardService（SYSTEM 权限）启动，自带进程保护 DACL

**进程伪装**：
- AgentA 可执行文件编译后重命名为类似系统进程的名称（如 `WinSecHelperA.exe`）
- AgentB 同理
- 可通过 .NET `AssemblyTitle` 属性设置进程描述，使其看起来像系统组件

**心跳通信**：
- 两进程间通过共享内存（MemoryMappedFile）或 Named Pipe 互相发送心跳信号
- 心跳间隔：10 秒
- 超时阈值：30 秒（3 次心跳未收到则判定对方死亡）

---

### 6.7 模块 NtpValidator（网络时间校验）

- 维护一个 NTP 服务器列表（多个备用）：
  - `pool.ntp.org`
  - `time.windows.com`
  - `time.google.com`
  - `cn.pool.ntp.org`（中国优先）
- 每 5 分钟通过 UDP 123 端口请求一次 NTP 时间，缓存结果
- 每次 RuleEngine tick 时，对比 NTP 时间与系统时间
- 偏差 > 5 分钟：认定时间被篡改，写入安全日志，触发 `TimeTampered` 锁屏
- 网络不可达时：回退到系统时间，但记录"NTP 校验失败"日志（不触发锁屏，避免误判）

---

### 6.8 模块 ShutdownScheduler（关机调度）

**双重保障机制**：

- **机制 1**：GuardService 内部定时器，每次 tick 检查当前时间是否到达 `autoShutdownTime`
  - 提前 60 秒发送 Toast 通知（"电脑将在 60 秒后关机"）
  - 到达时执行 `shutdown.exe /s /f /t 60`
  
- **机制 2**：安装时创建 Windows 任务计划程序（Task Scheduler）任务
  - 任务名：`ChildPCGuard_AutoShutdown`
  - 触发器：每日 22:00
  - 操作：`shutdown.exe /s /f /t 0`
  - 即使 GuardService 被意外停止，计划任务仍会执行关机

---

### 6.9 模块 IPC PipeServer（进程间通信）

**角色**：
- GuardService 作为 **Pipe Server**，监听来自 AdminPanel 的指令
- AdminPanel 作为 **Pipe Client**，发送指令并接收响应

**通信协议**（JSON 格式）：

| 指令 | 方向 | 内容 |
|------|------|------|
| `GET_STATUS` | AdminPanel → GuardService | 请求当前状态 |
| `STATUS_RESPONSE` | GuardService → AdminPanel | 返回今日时长、剩余时长、服务状态 |
| `UPDATE_CONFIG` | AdminPanel → GuardService | 更新配置（含新配置 JSON） |
| `ADD_TIME` | AdminPanel → GuardService | 追加 N 分钟宽限时间 |
| `UNLOCK` | AdminPanel → GuardService | 立即解除锁屏 |
| `LOCK_NOW` | AdminPanel → GuardService | 立即触发锁屏 |
| `PAUSE` | AdminPanel → GuardService | 暂停管控 N 分钟 |
| `SHUTDOWN_NOW` | AdminPanel → GuardService | 立即关机 |

**安全性**：Named Pipe 使用 DACL 限制，只允许本机进程连接，防止恶意程序发送指令。

---

## 7. 数据结构设计

### 7.1 配置文件 config.bin

存储路径：`C:\ProgramData\ChildPCGuard\config.bin`  
存储方式：JSON 序列化后 AES-256-CBC 加密，密钥由安装时生成并存储在注册表（SYSTEM 权限保护）

**配置模型（JSON 结构）**：

```
{
  "version": "1.0",
  "isEnabled": true,
  "adminPasswordHash": "[BCrypt hash]",

  "rules": {
    "weekdays": {
      "dailyLimitMinutes": 120,
      "allowedTimeWindows": [
        { "start": "15:00", "end": "20:00" }
      ]
    },
    "weekends": {
      "dailyLimitMinutes": 240,
      "allowedTimeWindows": [
        { "start": "09:00", "end": "21:00" }
      ]
    }
  },

  "autoShutdownTime": "22:00",
  "warningMinutes": [10, 5, 1],
  "idleThresholdMs": 5000,
  "continuousLimitMinutes": 0,
  "restDurationMinutes": 0,

  "blockedApps": [],
  "blockedSites": [],

  "useNtpValidation": true,
  "ntpServers": ["pool.ntp.org", "time.windows.com", "time.google.com", "cn.pool.ntp.org"],
  "ntpToleranceMinutes": 5,

  "serviceName": "WinSecSvc_a1b2c3d4",
  "serviceDisplayName": "Windows Security Update Service",

  "lockScreenMessage": "今天的使用时间已到，休息一下吧！",
  "emergencyUnlockShortcut": "Ctrl+Alt+Shift+F12"
}
```

### 7.2 状态文件 state.json

存储路径：`C:\ProgramData\ChildPCGuard\state.json`  
存储方式：明文 JSON（因为文件 ACL 已保护，只有 SYSTEM 可写）  
更新频率：每次 tick（每 5 秒）

```
{
  "date": "2026-05-02",
  "usedMinutesToday": 87,
  "lastActiveTime": "2026-05-02T16:23:00",
  "isLocked": false,
  "lockReason": null,
  "pausedUntil": null,
  "extraMinutesToday": 0,
  "continuousMinutes": 25,
  "lastNtpCheckTime": "2026-05-02T16:20:00",
  "lastNtpTime": "2026-05-02T16:20:05"
}
```

### 7.3 日志文件

存储路径：`C:\ProgramData\ChildPCGuard\logs\`  
命名规则：`usage-YYYY-MM-DD.log`（日常使用日志）、`security.log`（安全违规日志）

**日志字段**：
- 时间戳
- 事件类型（`SESSION_START`、`SESSION_END`、`LOCK`、`UNLOCK`、`SECURITY_VIOLATION` 等）
- 事件描述
- 今日累计使用时长

---

## 8. 防护体系设计（12层）

本项目采用 12 层纵深防御体系，对抗有一定电脑基础的初中生：

| 层级 | 防护技术 | 防御目标 | 实施时机 |
|------|----------|----------|----------|
| **第 1 层** | 标准用户账户限制 | 孩子无法执行管理员操作 | 安装前手动配置 |
| **第 2 层** | Windows 服务 + SYSTEM 权限 | 任务管理器普通用户无法 Kill | 服务注册时自动生效 |
| **第 3 层** | 双进程互保（AgentA ↔ AgentB） | 杀掉一个，另一个立即复活 | 服务启动时激活 |
| **第 4 层** | 服务 DACL（禁止 sc stop） | `sc stop` / `net stop` 返回"拒绝访问" | 安装脚本配置 |
| **第 5 层** | 服务注册表键 DACL 保护 | 无法通过注册表修改服务配置 | 安装脚本配置 |
| **第 6 层** | 文件/目录 ACL 保护 | 无法删除或修改程序文件和配置 | 安装脚本配置 |
| **第 7 层** | SCM 故障恢复（3 次自动重启） | 服务崩溃后 1 秒内重启 | 安装脚本配置 |
| **第 8 层** | NTP 时间校验 | 检测并应对修改系统时间的行为 | 服务运行时持续检测 |
| **第 9 层** | 进程句柄 DACL | 拒绝非 SYSTEM 账户的 PROCESS_TERMINATE 权限 | 服务启动时自设 |
| **第 10 层** | 安全模式检测 | 检测到安全模式立即强制关机 | 服务启动时检测 |
| **第 11 层** | PE 环境检测（配合 BitLocker） | 检测 PE 启动环境，关机 | 服务启动时检测 |
| **第 12 层** | 配置文件 AES-256 加密 | 无法直接读取和修改配置文件内容 | 写入配置时自动加密 |

### 8.1 服务 DACL 配置（SDDL 字符串）

- 只允许 SYSTEM（SY）和 Administrators（BA）完全控制服务
- 拒绝交互式用户（IU）和普通用户（BU）停止/修改服务
- SDDL：`D:(A;;CCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(D;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;IU)`

### 8.2 SCM 故障恢复配置

- 使用 `sc.exe failure` 命令配置
- 前 3 次失败均执行"重启服务"
- 重启延迟：1000ms（1 秒）
- 重置计数周期：86400 秒（24 小时）

### 8.3 进程伪装策略

- 服务内部显示名称设置为类似系统服务（如 "Windows Security Update Service"）
- AgentA/AgentB 进程名设置为不易引起孩子注意的名称
- 不在任务栏、系统托盘、开始菜单显示
- 可选：通过 `AssemblyProduct`、`AssemblyDescription` 设置进程在任务管理器中显示的描述

---

## 9. UI/UX 设计规范

### 9.1 锁屏界面（LockOverlay）

- **视觉风格**：简洁、清晰，避免过于激进的视觉（不会引发孩子强烈反感）
- **背景**：深色（#1E1E2E 或类似深灰蓝）或模糊的当前壁纸
- **居中显示**：
  - 大号时钟（显示当前时间，实时更新）
  - 锁屏原因文字（柔和语气，如"今天的使用时间到了，好好休息～"）
  - 密码输入框（PIN 输入，圆点显示，现代风格）
  - 错误提示（输入错误时显示，第 3 次显示锁定倒计时）
- **多屏幕**：在所有连接的显示器上均显示锁屏覆盖
- **无任务栏入口**：`ShowInTaskbar = false`

### 9.2 气泡通知（Toast）

- 使用 Windows 10/11 原生 Toast 通知（`Windows.UI.Notifications`）
- 通知内容：
  - 10 分钟预警："还有 10 分钟，记得保存进度哦"
  - 5 分钟预警："还有 5 分钟！"
  - 1 分钟预警："即将锁屏，最后 1 分钟！"
  - 关机预警："电脑将在 60 秒后自动关机"
- 通知可选点击忽略，但不影响锁屏/关机逻辑

### 9.3 管理员界面（AdminPanel）

- **风格**：简洁现代，家长能轻松上手
- **登录页**：白色/浅灰背景，居中密码框，输入错误有清晰提示
- **主界面**：分为左侧导航（状态 / 规则设置 / 临时操作 / 日志 / 设置）+ 右侧内容区
- **状态卡片**：今日使用时长（大数字+进度条）、剩余时长、服务状态指示灯
- **规则设置**：时间段用时间选择器，时长用滑块或数字输入，直观易用
- **日历视图**：日志页可按日历视图展示每天的使用时长（热力图）

---

## 10. 安装与部署方案

### 10.1 安装前提条件（家长操作）

1. **将孩子的 Windows 账户改为标准用户**（最关键步骤）
   - 路径：控制面板 → 用户账户 → 管理其他账户 → 选择孩子账户 → 更改账户类型 → 标准

2. **建议同步配置 BIOS/UEFI**（可选但强烈建议）
   - 设置 BIOS 密码（防止孩子进入 BIOS 修改启动顺序）
   - 禁用从 USB 设备启动（防止 PE 系统绕过）
   - 启用 BitLocker 磁盘加密（防止 PE 读取磁盘数据）

3. **保管好管理员账户密码**，不告知孩子

### 10.2 安装脚本（install.ps1）执行内容

安装脚本以**管理员权限**运行，按顺序执行：

| 步骤 | 操作 |
|------|------|
| 1 | 创建安装目录 `C:\Program Files\ChildPCGuard\` |
| 2 | 复制所有程序文件到安装目录 |
| 3 | 设置安装目录 ACL（拒绝标准用户写入/删除，仅 SYSTEM/Admins 完全控制） |
| 4 | 创建数据目录 `C:\ProgramData\ChildPCGuard\`，设置同等 ACL |
| 5 | 生成并写入默认 `config.bin`（提示家长输入初始管理密码） |
| 6 | 注册 Windows 服务（`sc.exe create`，LocalSystem，auto start） |
| 7 | 配置服务 DACL（`sc.exe sdset`） |
| 8 | 配置服务故障恢复（`sc.exe failure`） |
| 9 | 保护服务注册表键（ACL） |
| 10 | 创建 Windows 任务计划程序任务（每日 22:00 关机备用） |
| 11 | 注册紧急热键（AdminPanel 快捷键） |
| 12 | 启动服务（`sc.exe start`） |
| 13 | 打印安装摘要（服务名、数据目录、初始规则） |

### 10.3 卸载脚本（uninstall.ps1）执行内容

卸载需要：**管理员权限 + 输入管理密码**（防止孩子通过卸载绕过）

| 步骤 | 操作 |
|------|------|
| 1 | 验证管理员密码 |
| 2 | 停止服务（`sc.exe stop`） |
| 3 | 停止 AgentA / AgentB 进程 |
| 4 | 删除 Windows 服务（`sc.exe delete`） |
| 5 | 删除任务计划程序任务 |
| 6 | 删除安装目录（需先移除 ACL 限制） |
| 7 | 删除数据目录（可选保留日志） |
| 8 | 清理注册表 |

### 10.4 项目目录结构

```
ChildPCGuard/
├── src/
│   ├── GuardService/               # 守护服务（.NET Worker Service）
│   │   ├── Program.cs              # 服务注册入口
│   │   ├── Worker.cs               # 主循环（BackgroundService）
│   │   ├── TimeTracker.cs          # GetLastInputInfo 计时
│   │   ├── RuleEngine.cs           # 规则判断引擎
│   │   ├── ShutdownScheduler.cs    # 关机调度
│   │   ├── NtpValidator.cs         # NTP 时间校验
│   │   ├── NotificationHelper.cs   # Toast 气泡通知
│   │   └── appsettings.json
│   │
│   ├── LockOverlay/                # 锁屏覆盖层（WPF）
│   │   ├── Program.cs
│   │   ├── LockWindow.xaml/.cs     # 主锁屏窗口
│   │   ├── KeyboardHook.cs         # 全局键盘钩子（WH_KEYBOARD_LL）
│   │   ├── VirtualDesktop.cs       # CreateDesktop/SwitchDesktop 封装
│   │   └── PinVerifier.cs          # PIN 验证逻辑
│   │
│   ├── AdminPanel/                 # 管理员界面（WinForms 或 WPF）
│   │   ├── Program.cs
│   │   ├── LoginWindow.xaml/.cs    # 密码登录页
│   │   ├── MainWindow.xaml/.cs     # 主界面（左侧导航 + 内容区）
│   │   ├── Pages/
│   │   │   ├── StatusPage.xaml/.cs         # 状态卡片
│   │   │   ├── RulesPage.xaml/.cs          # 规则设置
│   │   │   ├── QuickActionsPage.xaml/.cs   # 临时操作
│   │   │   ├── LogsPage.xaml/.cs           # 使用日志
│   │   │   └── SettingsPage.xaml/.cs       # 密码修改/关于
│   │   └── PipeClient.cs           # Named Pipe 客户端
│   │
│   ├── AgentA/                     # 双进程互保 Agent A
│   │   ├── Program.cs
│   │   └── WatchdogWorker.cs
│   │
│   ├── AgentB/                     # 双进程互保 Agent B
│   │   ├── Program.cs
│   │   └── WatchdogWorker.cs
│   │
│   └── Shared/                     # 共享库
│       ├── Config/
│       │   ├── AppConfig.cs            # 配置数据模型
│       │   └── ConfigManager.cs        # 配置读写（加密/解密）
│       ├── State/
│       │   ├── DailyState.cs           # 今日状态数据模型
│       │   └── StateManager.cs         # 状态读写
│       ├── IPC/
│       │   ├── PipeServer.cs           # Named Pipe 服务端
│       │   ├── PipeClient.cs           # Named Pipe 客户端
│       │   └── IpcMessage.cs           # 消息数据模型
│       ├── Protection/
│       │   ├── ServiceProtector.cs     # 服务 DACL 保护
│       │   ├── RegistryProtector.cs    # 注册表键 DACL 保护
│       │   ├── FileProtector.cs        # 文件/目录 ACL 保护
│       │   ├── ProcessAccessControl.cs # 进程句柄 DACL
│       │   ├── NtpTimeValidator.cs     # NTP 校验
│       │   ├── SafeModeDetector.cs     # 安全模式检测
│       │   ├── PEDetector.cs           # PE 环境检测
│       │   └── VirtualMachineDetector.cs # 虚拟机检测
│       └── Logging/
│           └── Logger.cs               # Serilog 封装
│
├── scripts/
│   ├── install.ps1                 # 安装脚本（管理员运行）
│   └── uninstall.ps1               # 卸载脚本（需管理密码）
│
├── data/
│   └── config.example.json         # 配置文件示例（明文，供参考）
│
├── docs/
│   └── ChildPCGuard_项目方案.md    # 本文档
│
├── ChildPCGuard.sln                # Visual Studio 解决方案
└── README.md
```

---

## 11. 开发路线图

### Phase 1：MVP（最小可用版本）——约 2 周

**目标**：实现核心功能，可以实际部署使用

- [ ] 创建 .NET 8 解决方案结构
- [ ] 实现 GuardService 基础框架（.NET Worker Service）
- [ ] 实现 TimeTracker（GetLastInputInfo 计时）
- [ ] 实现 RuleEngine（时长判断）
- [ ] 每日 22:00 自动关机（双重机制）
- [ ] 超时触发系统原生锁屏（`LockWorkStation()`，先用系统锁屏替代自定义锁屏）
- [ ] state.json 持久化与重启恢复
- [ ] 服务注册与开机自启
- [ ] 服务 SCM 故障恢复配置
- [ ] 安装 PowerShell 脚本（基础版）
- [ ] 10/5/1 分钟 Toast 预警通知

**验收标准**：服务运行后，达到时长上限触发锁屏，22:00 自动关机，重启后计时恢复。

---

### Phase 2：防护加固——约 1 周

**目标**：完善自我保护，达到对抗初中生的防护级别

- [ ] 服务 DACL 配置（`sc.exe sdset`）
- [ ] 服务注册表键 DACL 保护
- [ ] 安装目录 + 配置文件 ACL 保护
- [ ] 进程句柄 DACL（防任务管理器 Kill）
- [ ] NTP 时间校验（防修改系统时间）
- [ ] 安全模式检测（安全模式启动则关机）
- [ ] 配置文件 AES-256 加密
- [ ] 安装脚本完善（自动配置所有 ACL/DACL）

**验收标准**：孩子账户无法通过任务管理器、sc stop、删除文件、修改时间等手段绕过程序。

---

### Phase 3：锁屏模块——约 1 周

**目标**：替换系统原生锁屏为自定义锁屏覆盖层

- [ ] LockOverlay 基础框架（WPF 全屏窗口）
- [ ] 全局键盘钩子（拦截 Alt+Tab、Win 键等）
- [ ] 虚拟桌面方案（CreateDesktop / SwitchDesktop）
- [ ] PIN 密码验证（BCrypt 哈希对比）
- [ ] 密码错误 3 次锁定 5 分钟
- [ ] 紧急解锁快捷键（Ctrl+Alt+Shift+F12 × 5）
- [ ] 多屏幕支持（覆盖所有显示器）
- [ ] GuardService 对 LockOverlay 的存活监控与重启

**验收标准**：锁屏后所有快捷键被拦截，只能通过密码解锁，密码错误有保护机制。

---

### Phase 4：双进程互保——约 3-5 天

**目标**：增加双进程互保机制，进一步提高抗杀能力

- [ ] AgentA / AgentB 基础框架
- [ ] 心跳机制（Named Pipe 或共享内存）
- [ ] 互相监控 + 复活逻辑
- [ ] 进程名伪装（修改 AssemblyTitle）
- [ ] GuardService 对两 Agent 的启动与监控

**验收标准**：在任务管理器中结束 AgentA，10 秒内 AgentB 自动重启 AgentA。

---

### Phase 5：管理员界面——约 1 周

**目标**：实现图形化管理界面，家长可轻松配置

- [ ] AdminPanel 基础框架（WPF/WinForms）
- [ ] 密码登录页（BCrypt 验证）
- [ ] 状态首页（今日时长、服务状态）
- [ ] 规则设置页（时长、时段、关机时间）
- [ ] 临时操作（追加时间、暂停管控、立即锁屏）
- [ ] 日志查看页
- [ ] Named Pipe 与 GuardService 通信
- [ ] 全局热键注册（呼出 AdminPanel）

**验收标准**：家长通过密码进入 AdminPanel，可修改规则并即时生效，可查看使用日志。

---

### Phase 6：可选增强——约 1-2 周（视需求决定）

- [ ] 应用程序黑名单（检测到黑名单进程立即关闭）
- [ ] 连续使用时长限制 + 强制休息
- [ ] 虚拟机检测
- [ ] PE 环境检测（配合 BitLocker 建议）
- [ ] 使用统计图表（按周/月热力图）
- [ ] 网站访问记录（浏览器历史读取）

---

## 12. 测试用例与验收标准

### 12.1 基础功能测试

| 测试 ID | 测试场景 | 操作步骤 | 预期结果 |
|---------|----------|----------|----------|
| T-01 | 时长限制 | 将时长上限设为 5 分钟，使用 5 分钟 | 60 秒内触发锁屏 |
| T-02 | 锁屏解锁 | 触发锁屏后输入正确密码 | 桌面恢复正常 |
| T-03 | 错误密码保护 | 连续输入 3 次错误密码 | 锁定 5 分钟，显示倒计时 |
| T-04 | 自动关机 | 将关机时间设为当前时间 +2 分钟 | 2 分钟后触发 60 秒倒计时，然后关机 |
| T-05 | 重启恢复 | 使用 30 分钟后重启电脑 | 重启后服务启动，读取 30 分钟已用时长 |
| T-06 | 倒计时通知 | 剩余时长接近 10/5/1 分钟 | 弹出对应 Toast 通知 |
| T-07 | 空闲不计时 | 离开电脑 10 分钟（不操作） | 使用时长不增加 |

### 12.2 防护能力测试（孩子账户执行）

| 测试 ID | 攻击手段 | 操作步骤 | 预期结果 |
|---------|----------|----------|----------|
| S-01 | 任务管理器 Kill | 孩子账户打开任务管理器，结束 GuardService 进程 | 操作被拒绝，或进程立即复活 |
| S-02 | sc stop 停止服务 | 孩子账户执行 `sc stop ChildPCGuard` | 返回"拒绝访问"，服务继续运行 |
| S-03 | 删除程序文件 | 孩子账户尝试删除安装目录文件 | 操作被拒绝 |
| S-04 | 修改配置文件 | 孩子账户尝试修改 config.bin | 操作被拒绝（ACL 保护） |
| S-05 | 修改系统时间 | 孩子账户将系统时间改为早上 8 点 | 检测到 NTP 偏差，触发锁屏并记录日志 |
| S-06 | 修改注册表启动项 | 孩子账户尝试修改服务注册表键 | 操作被拒绝（注册表 ACL 保护） |
| S-07 | 杀死 AgentA | 孩子账户结束 AgentA 进程（如果有权限） | AgentB 在 30 秒内重启 AgentA |
| S-08 | 安全模式启动 | 通过 F8 进入安全模式 | 服务检测到安全模式，立即关机 |

### 12.3 管理员界面测试

| 测试 ID | 测试场景 | 操作步骤 | 预期结果 |
|---------|----------|----------|----------|
| A-01 | 密码验证 | 输入正确管理密码 | 进入主界面 |
| A-02 | 修改规则 | 修改时长上限并保存 | 修改立即生效，服务使用新规则 |
| A-03 | 追加时间 | 点击"追加 30 分钟" | 今日可用时长增加 30 分钟 |
| A-04 | 查看日志 | 打开日志页面 | 显示近 7 天使用记录 |
| A-05 | 立即锁屏 | 点击"立即锁屏"按钮 | 锁屏界面立即出现 |

---

## 13. 风险与局限性说明

### 13.1 无法防御的场景

| 场景 | 说明 | 应对建议 |
|------|------|----------|
| 孩子知道管理员账户密码 | 可切换到管理员账户操作，所有保护失效 | 家长密码妥善保管，不告知孩子 |
| BIOS/UEFI 未保护 + U 盘启动 | 可用 PE 系统绕过 Windows 环境 | 设置 BIOS 密码，禁用 U 盘启动 |
| 开启 BitLocker + BIOS 密码 仍无法访问磁盘 | 这是 BitLocker 正常工作 | 提前做好密钥备份 |
| 重装操作系统 | 极端情况，一切软件保护失效 | BIOS 密码 + 家长与孩子沟通 |
| Hyper-V 虚拟机（需要管理员权限创建） | 标准用户无法创建 Hyper-V 虚拟机 | 保持孩子账户为标准用户即可 |
| Windows 恢复环境（WinRE） | 可进行系统修复操作 | BIOS 密码 + BitLocker |

### 13.2 杀毒软件兼容性

以下操作可能被杀毒软件误报：
- 双进程互保（类似恶意软件自我保护行为）
- DACL 修改（修改系统权限）
- 隐藏文件/目录（Rootkit 特征）

**解决方案**：
1. 安装时临时暂停实时防护
2. 安装完成后将安装目录添加到信任区（白名单）
3. 将服务进程和 Agent 进程添加到进程白名单

### 13.3 法律与伦理提示

> ⚠️ 重要提示：
> - 监控未成年子女的电脑使用是家长的合法监护行为，但建议以**透明方式**告知孩子程序的存在（但不告知技术细节和密码）。
> - 避免完全隐秘运行：孩子一旦发现会产生严重的信任危机。
> - 定期与孩子沟通规则，培养自律习惯。软件是辅助工具，不能替代亲子沟通。
> - 规则应合理：学期内适当限制，假期可以适当放宽。

---

## 14. 参考资料与开源项目

### 14.1 同类开源项目（GitHub）

| 项目 | 语言 | 核心亮点 | 链接 |
|------|------|----------|------|
| ScreenTimeGuard | C# / .NET 8 | **防篡改 Windows 服务**，服务 DACL 保护，SCM 自动重启 | https://github.com/saimakhan89/screentimeguard |
| PC-Usage-Timer | C# / .NET 9 | 倒计时锁屏，PIN 保护，键盘钩子拦截快捷键 | https://github.com/vlytvynchyk/PC-Usage-Timer |
| parental-control (vasyaod) | Haskell | 跨平台，Web UI，HTTP API，多用户时间配额 | https://github.com/vasyaod/parental-control |

### 14.2 官方技术文档（Microsoft）

| 文档 | 说明 | 链接 |
|------|------|------|
| Windows Service 安全性 | 服务 DACL 配置官方文档 | https://learn.microsoft.com/zh-cn/windows/win32/services/service-security-and-access-rights |
| .NET Worker Service | Windows 服务开发框架 | https://learn.microsoft.com/zh-cn/dotnet/core/extensions/workers |
| SetWindowsHookEx | 全局键盘钩子 API | https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-setwindowshookexw |
| CreateDesktop | 虚拟桌面 API | https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-createdesktopw |
| GetLastInputInfo | 空闲时间检测 API | https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-getlastinputinfo |
| Windows 任务计划程序 | 计划任务创建 | https://learn.microsoft.com/zh-cn/windows/win32/taskschd/task-scheduler-start-page |

### 14.3 相关工具与库

| 工具/库 | 用途 | 链接 |
|---------|------|------|
| WinSW | Windows 服务封装工具（13.9k Stars），可将任意可执行文件注册为 Windows 服务 | https://github.com/winsw/winsw |
| BCrypt.Net-Next | BCrypt 密码哈希库 | https://github.com/BcryptNet/bcrypt.net |
| Serilog | 结构化日志库 | https://serilog.net |
| Hardcodet.NotifyIcon.Wpf | WPF 系统托盘图标（如需使用托盘）| https://github.com/hardcodet/wpf-notifyicon |

### 14.4 NTP 协议参考

- NTP 时间服务器：UDP 协议，端口 123
- NTP 数据包格式：48 字节，第 40-47 字节为时间戳
- 国内推荐服务器：`cn.pool.ntp.org`、`ntp.aliyun.com`、`ntp.tencent.com`
- RFC 5905：NTP v4 规范文档

---

## 附录 A：关键配置参数默认值

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `weekdays.dailyLimitMinutes` | 120 | 工作日每日限制 2 小时 |
| `weekends.dailyLimitMinutes` | 240 | 周末每日限制 4 小时 |
| `autoShutdownTime` | `"22:00"` | 每天 22:00 关机 |
| `warningMinutes` | `[10, 5, 1]` | 提前 10/5/1 分钟预警 |
| `idleThresholdMs` | `5000` | 空闲超过 5 秒不计时 |
| `ntpToleranceMinutes` | `5` | NTP 偏差超过 5 分钟视为篡改 |
| `lockScreen.maxPasswordAttempts` | `3` | 密码错误 3 次锁定 |
| `lockScreen.lockoutDurationMinutes` | `5` | 锁定 5 分钟 |

---

## 附录 B：安装后验证清单

安装完成后，**以孩子账户登录**，逐一执行以下验证：

- [ ] 任务管理器中无明显"ChildPCGuard"相关进程名
- [ ] 任务栏、系统托盘无异常图标
- [ ] `sc stop [服务名]` 返回"拒绝访问"
- [ ] 尝试删除 `C:\Program Files\ChildPCGuard\` 被拒绝
- [ ] 尝试修改 `C:\ProgramData\ChildPCGuard\config.bin` 被拒绝
- [ ] 修改系统时间 1 小时后，5 分钟内触发锁屏
- [ ] 使用设定时间后，锁屏出现，输入正确密码可解锁
- [ ] 错误密码 3 次后界面锁定 5 分钟
- [ ] 重启电脑后，服务自动启动，今日使用时长已保留

---

*文档编写时间：2026-05-02 | 文档版本：v1.0*  
*本文档为 ChildPCGuard 项目唯一完整参考文档*
