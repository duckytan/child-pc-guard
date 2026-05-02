# Task Plan: ChildPCGuard 项目开发规划

## Goal
开发一款运行在 Windows 10/11 上的儿童电脑使用时间管控程序，具备企业级自我保护能力，能有效对抗有一定电脑基础的初中生绕过。

## ⚠️ 铁律
- **未收到"开始开发"指令前，禁止编写任何项目代码**
- **每次修改文件后，必须 push 到 https://github.com/duckytan/child-pc-guard.git**

---

## 阶段规划

### 讨论/方案阶段（当前阶段）
- [x] 调研现有方案，输出调研报告
- [x] 对比方案，提出融合建议
- [x] 补充深度调研技术细节
- [x] 形成终极防护增强方案
- [x] 整合为唯一完整项目方案文档（`ChildPCGuard_项目方案.md`）
- [x] 归档过程性调研文档到 `archive/`
- [x] 创建 `CODEBUDDY.md`（含铁律和架构说明）
- [x] 创建 CodeBuddy Rules（`.codebuddy/rules/`）
- [x] 初始化 Git 仓库并推送到 GitHub
- [ ] 方案评审与完善（进行中）
- [ ] 用户确认方案，说出"开始开发"

### Phase 1：MVP（约 2 周）
> 等待"开始开发"指令后启动
- [ ] 创建 .NET 8 解决方案结构（`ChildPCGuard.sln`）
- [ ] 实现 GuardService 基础框架（.NET Worker Service）
- [ ] 实现 TimeTracker（GetLastInputInfo 计时）
- [ ] 实现 RuleEngine（时长/时段判断）
- [ ] 每日 22:00 自动关机（双重机制）
- [ ] 超时触发系统原生锁屏（`LockWorkStation()`）
- [ ] state.json 持久化与重启恢复
- [ ] 服务注册与开机自启
- [ ] 服务 SCM 故障恢复配置
- [ ] 安装 PowerShell 脚本（基础版）
- [ ] 10/5/1 分钟 Toast 预警通知

### Phase 2：防护加固（约 1 周）
> 等待 Phase 1 完成后启动
- [ ] 服务 DACL 配置（`sc.exe sdset`）
- [ ] 服务注册表键 DACL 保护
- [ ] 安装目录 + 配置文件 ACL 保护
- [ ] 进程句柄 DACL（防任务管理器 Kill）
- [ ] NTP 时间校验（防修改系统时间）
- [ ] 安全模式检测（检测到即关机）
- [ ] 配置文件 AES-256 加密

### Phase 3：自定义锁屏（约 1 周）
- [ ] LockOverlay 基础框架（WPF 全屏窗口）
- [ ] 全局键盘钩子（拦截 Alt+Tab、Win 键等）
- [ ] 虚拟桌面方案（CreateDesktop / SwitchDesktop）
- [ ] PIN 密码验证（BCrypt 哈希对比）
- [ ] 密码错误 3 次锁定 5 分钟
- [ ] 紧急解锁快捷键（Ctrl+Alt+Shift+F12 × 5）
- [ ] 多屏幕支持

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
| Q-03 | AgentA/AgentB 心跳用 Named Pipe 还是共享内存？ | 待讨论 |
| Q-04 | 是否需要 Phase 6 的网站访问记录功能？ | 待用户决策 |
| Q-05 | install.ps1 是否需要支持静默安装（无 UI 模式）？ | 待讨论 |

---

## 已确定决策

- **语言**：C# / .NET 8（LTS）
- **服务框架**：.NET Worker Service（BackgroundService）
- **锁屏 UI**：WPF + 虚拟桌面（CreateDesktop/SwitchDesktop）
- **密码哈希**：BCrypt.Net-Next（不可逆）
- **配置加密**：AES-256-CBC，密钥存注册表（SYSTEM 保护）
- **IPC**：Named Pipe（JSON 消息协议）
- **日志**：Serilog + 滚动文件
- **安装方式**：PowerShell 脚本（无需额外依赖）
- **关机双保险**：GuardService 定时器 + Windows 任务计划程序

---

## 错误记录

_暂无_

---

## Status
**当前处于：讨论/方案阶段** — 方案已基本成型，等待用户确认并说出"开始开发"后进入 Phase 1
