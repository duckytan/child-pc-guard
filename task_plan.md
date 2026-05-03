# Task Plan: ChildPCGuard 项目开发规划

## Goal
开发一款运行在 Windows 10/11 上的儿童电脑使用时间管控程序，具备企业级自我保护能力，能有效对抗有一定电脑基础的初中生绕过。

## ⚠️ 铁律
- **未收到"开始开发"指令前，禁止编写任何项目代码**
- **每次修改文件后，必须 push 到 https://github.com/duckytan/child-pc-guard.git**

---

## 阶段规划

### 讨论/方案阶段（✅ 已完成）
- [x] 调研现有方案，输出调研报告
- [x] 对比方案，提出融合建议
- [x] 补充深度调研技术细节
- [x] 形成终极防护增强方案
- [x] 整合为唯一完整项目方案文档（`ChildPCGuard_项目方案.md`）
- [x] 归档过程性调研文档到 `archive/`
- [x] 创建 `CODEBUDDY.md`（含铁律和架构说明）
- [x] 创建 CodeBuddy Rules（`.codebuddy/rules/`）
- [x] 初始化 Git 仓库并推送到 GitHub
- [x] 补充完整工程文档体系（SRS、测试计划、验收标准、安全说明、贡献指南）
- [x] 用户确认方案，说出"开始开发"（2026-05-03）

### Phase 1：MVP（约 2 周）⬅ 当前阶段
> 开发启动时间：2026-05-03
- [x] 创建 .NET 8 解决方案结构（`ChildPCGuard.sln`）
- [x] 实现 GuardService 基础框架（.NET Worker Service）
- [x] 实现 TimeTracker（GetLastInputInfo 计时）
- [x] 实现 RuleEngine（时长/时段判断）
- [x] 每日 22:00 自动关机（双重机制：内部 + 计划任务）
- [x] 超时触发系统原生锁屏（`LockWorkStation()`，LockOverlay 未就绪时的 Phase 1 临时方案）
- [x] state.json 持久化与重启恢复（StateManager）
- [x] 服务注册与开机自启（install.ps1）
- [x] 服务 SCM 故障恢复配置（install.ps1 中 sc failure 命令）
- [x] 安装 PowerShell 脚本（基础版，含 ACL/DACL/计划任务）
- [x] 10/5/1 分钟 Toast 预警通知（NotificationHelper）
- [ ] 本地构建验证（需 .NET 8 SDK 环境）
- [ ] 部署测试（实际机器上运行验证）

### Phase 2：防护加固（约 1 周）✅ 已完成
> 完成时间：2026-05-03
- [x] 服务 DACL 配置（`sc.exe sdset`，仅 SYSTEM/Admins 可修改）
- [x] 服务注册表键 DACL 保护（Admins 只读，不能修改服务配置）
- [x] 安装目录 + 配置文件 ACL 保护（Users 只读，不能写入/删除）
- [x] 进程句柄 DACL（防任务管理器 Kill，ProcessSecurity.cs）
- [x] NTP 时间校验（防修改系统时间，已在 Phase 1 集成到 GuardWorker）
- [x] 安全模式检测（检测到即关机，已在 Phase 1 集成到 GuardWorker）
- [x] 配置文件 AES-256 加密（ConfigManager.cs 已实现，密钥派生自机器 GUID）

### Phase 3：自定义锁屏（约 1 周）✅ 已完成
> 完成时间：2026-05-03
- [x] LockOverlay 基础框架（WPF 全屏窗口，LockWindow.xaml）
- [x] 全局键盘钩子（拦截 Alt+Tab、Win 键等，KeyboardHook.cs）
- [x] 虚拟桌面方案（CreateDesktop / SwitchDesktop，VirtualDesktopManager.cs）
- [x] PIN 密码验证（BCrypt 哈希对比，PasswordValidator.cs）
- [x] 密码错误 3 次锁定 5 分钟（PasswordValidator 实现）
- [ ] 紧急解锁快捷键（Ctrl+Alt+Shift+F12 × 5）- 可选功能
- [ ] 多屏幕支持（当前仅单屏）

### Phase 4：双进程互保（约 3-5 天）
- [ ] AgentA / AgentB 基础框架
- [ ] 心跳机制（Named Pipe 或共享内存）
- [ ] 互相监控 + 复活逻辑
- [ ] 进程名伪装

### Phase 5：管理员界面（约 1 周）
- [ ] AdminPanel 基础框架（WPF）
- [ ] 密码登录页（BCrypt 验证）
- [ ] 状态首页 + 规则设置页
- [ ] 临时操作（追加时间/暂停/立即锁屏）
- [ ] 日志查看页
- [ ] Named Pipe 与 GuardService 通信

### Phase 6：可选增强（约 1-2 周）
- [ ] 应用程序黑名单
- [ ] 连续使用时长限制 + 强制休息
- [ ] 虚拟机检测
- [ ] 使用统计图表

---

## 待讨论/待决策事项

| 编号 | 问题 | 状态 |
|------|------|------|
| Q-01 | LockOverlay 优先用虚拟桌面方案还是全屏窗体方案？ | 已定：虚拟桌面优先，全屏窗体备选 |
| Q-02 | AdminPanel 使用 WPF 还是 WinForms？ | 已定：WPF |
| Q-03 | AgentA/AgentB 心跳用 Named Pipe 还是共享内存？ | 已定：Named Pipe（复用现有基础设施，Pipe 断开即感知对方死亡） |
| Q-04 | 是否需要 Phase 6 的网站访问记录功能？ | 已定：暂缓，Phase 1-5 完成后再决定 |
| Q-05 | install.ps1 是否需要支持静默安装（无 UI 模式）？ | 已定：支持，通过 `-Silent` 可选参数实现，不影响默认交互模式 |

---

## 已确定决策

- **语言**：C# / .NET 8（LTS）
- **服务框架**：.NET Worker Service（BackgroundService）
- **锁屏 UI**：WPF + 虚拟桌面（CreateDesktop/SwitchDesktop）
- **密码哈希**：BCrypt.Net-Next（不可逆）
- **配置加密**：AES-256-CBC，密钥存注册表（SYSTEM 保护）
- **IPC**：Named Pipe（JSON 消息协议）
- **日志**：Serilog + 滚动文件
- **AgentA/AgentB 心跳**：Named Pipe（复用现有基础设施，Pipe 断开天然感知对方死亡）
- **网站访问记录**：暂缓，Phase 1-5 完成后再决定
- **install.ps1 静默模式**：支持 `-Silent` 可选参数，不影响默认交互模式

---

## 错误记录

_暂无_

---

## Status
**当前处于：Phase 4 开发阶段** — Phase 1/2/3 已完成，进入 Phase 4 双进程互保开发
