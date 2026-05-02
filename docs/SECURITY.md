# 安全设计说明

**项目**：ChildPCGuard  
**文档编号**：SEC-001  
**版本**：v1.0  
**日期**：2026-05-03  
**密级**：内部文档

---

## 1. 威胁建模

### 1.1 攻击者画像

| 攻击者 | 技术能力 | 典型手段 |
|--------|----------|----------|
| **目标用户（初中生）** | 会用任务管理器；会搜索"如何停止服务"；可能看过 YouTube 教程 | sc stop/delete、任务管理器 Kill、修改系统时间、删除程序文件 |
| **进阶用户** | 会使用命令行；了解注册表；可能会用 Cheat Engine 等工具 | 修改注册表启动项、使用进程注入工具 |
| **超级用户（排除范围）** | 会编程、会内核调试、知道管理员密码 | 超出本程序防护范围，建议配合 BIOS 密码 |

### 1.2 攻击面分析（STRIDE）

| 威胁类型 | 攻击面 | 缓解措施 |
|----------|--------|----------|
| **S** 欺骗（Spoofing） | 伪造 IPC 消息 | Named Pipe ACL 仅允许 SYSTEM/Admins |
| **T** 篡改（Tampering） | 修改 config.bin / state.json | AES-256-GCM 加密 + ACL 文件保护 |
| **R** 抵赖（Repudiation） | 否认使用记录 | 日志文件只追加，有完整性校验 |
| **I** 信息泄露（Information Disclosure） | 读取配置文件获取密码哈希 | 文件 ACL + 密文存储 |
| **D** 拒绝服务（Denial of Service） | Kill 进程停止计时 | DACL 保护 + 双进程互保 |
| **E** 提权（Elevation of Privilege） | 利用服务漏洞提权 | 服务以 SYSTEM 运行，无对外网络暴露 |

---

## 2. 12 层防护体系

| 层级 | 防护措施 | 对抗手段 |
|------|----------|----------|
| L1 | 服务 DACL（`sc sdset`） | `sc stop/delete` |
| L2 | 进程句柄 DACL（`SetSecurityInfo`） | 任务管理器 Kill |
| L3 | 安装目录 ACL（仅 SYSTEM/Admins 写） | 删除/修改程序文件 |
| L4 | 注册表服务键 DACL | 修改 `HKLM\SYSTEM\...\Services\ChildPCGuard` |
| L5 | 配置文件 AES-256-GCM 加密 | 读取/篡改 config.bin |
| L6 | 双进程互保（AgentA ↔ AgentB） | 单独 Kill 某个进程 |
| L7 | SCM 故障恢复（重启服务） | 进程意外崩溃 |
| L8 | NTP 时间校验（偏差 > 5 分钟触发锁屏） | 修改系统时间 |
| L9 | 安全模式检测（检测到则关机） | F8 进入安全模式 |
| L10 | 自定义虚拟桌面锁屏（全局键盘钩子） | Alt+Tab / Win+D 绕过锁屏 |
| L11 | 任务计划程序关机（双重保障） | 单点故障绕过关机 |
| L12 | 进程名伪装 | 搜索进程名定位程序 |

---

## 3. 密码安全

### 3.1 密码哈希

- 算法：**BCrypt**（`BCrypt.Net-Next` 库）
- Cost Factor：**12**（在普通 PC 上约 250ms/次，暴力破解成本极高）
- 不存储明文密码，只存储哈希值

```csharp
// 哈希生成
string hash = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);

// 哈希验证
bool isValid = BCrypt.Net.BCrypt.Verify(plainPassword, storedHash);
```

### 3.2 错误密码保护

- 连续 3 次错误 → 锁定 5 分钟
- 锁定状态持久化（重启后不重置）
- 锁定时间显示倒计时，不泄露剩余尝试次数

### 3.3 密码复杂度建议（安装时提示）

- 最少 8 位
- 包含字母 + 数字
- 不使用生日、孩子姓名等易猜信息

---

## 4. 配置文件加密

### 4.1 加密方案

- 算法：**AES-256-GCM**（提供加密 + 完整性验证）
- 密钥派生：**PBKDF2**（SHA-256，100,000 次迭代，随机 Salt）
- 每次写入生成新 IV（12 字节随机）

### 4.2 密钥存储

密钥不直接存储，通过以下方式派生：
- 主密钥 = PBKDF2(机器唯一标识 + 编译时常量, Salt, 100000)
- 机器唯一标识 = `Win32_ComputerSystemProduct.UUID`（硬件绑定）

> **安全性说明**：此方案的目标是防止孩子直接读取配置文件，不是防止专业逆向工程。对于有管理员权限的攻击者，所有软件保护均可绕过。

### 4.3 配置文件结构

```
config.bin（二进制，加密）
├── [4 bytes] Magic Number: 0x43504700
├── [4 bytes] Version: 0x00000001
├── [16 bytes] Salt
├── [12 bytes] IV
├── [4 bytes] CipherText Length
├── [N bytes] CipherText（AES-256-GCM 加密的 JSON）
└── [16 bytes] Authentication Tag（GCM Tag）
```

---

## 5. IPC 安全

### 5.1 Named Pipe 访问控制

```csharp
// 只允许 SYSTEM 账户和本地管理员组访问
var pipeSecurity = new PipeSecurity();
pipeSecurity.AddAccessRule(new PipeAccessRule(
    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
    PipeAccessRights.FullControl,
    AccessControlType.Allow
));
pipeSecurity.AddAccessRule(new PipeAccessRule(
    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
    PipeAccessRights.ReadWrite,
    AccessControlType.Allow
));
```

### 5.2 IPC 消息验证

- 所有 IPC 消息包含 HMAC-SHA256 签名，防止伪造
- 消息包含序列号和时间戳，防止重放攻击
- 连接来源验证：只接受来自已知进程（通过 PID + 进程路径验证）的连接

---

## 6. 进程权限设计

| 进程 | 运行账户 | 最小权限原则说明 |
|------|----------|----------------|
| GuardService | SYSTEM | 需要 SYSTEM 权限设置 DACL、操作所有会话 |
| AgentA / AgentB | SYSTEM | 需要监控并重启 SYSTEM 级别进程 |
| LockOverlay | SYSTEM（交互式） | 需要在所有桌面上显示覆盖层 |
| AdminPanel | Administrators（家长） | 不需要 SYSTEM；通过 IPC 与服务通信 |
| install.ps1 | Administrators | 仅安装时使用 |

---

## 7. 日志安全

### 7.1 禁止记录的内容

- 管理员密码（明文或哈希）
- 加密密钥或 Salt
- 完整的配置内容

### 7.2 必须记录的内容（用于审计）

```
[INFO] 服务启动，版本 X.X.X
[INFO] 今日已用时长恢复：XX 分钟
[WARN] 检测到时间篡改：本地时间 vs NTP 偏差 X 分钟
[WARN] 密码错误，剩余尝试次数：X
[WARN] 密码连续错误 3 次，锁定 5 分钟
[INFO] 管理员登录成功
[INFO] 规则已更新：工作日配额改为 X 分钟
[ERROR] AgentA 意外退出，正在重启...
[INFO] AgentA 已重启，PID: XXXXX
```

### 7.3 日志文件保护

- 路径：`%ProgramData%\ChildPCGuard\logs\`
- ACL：SYSTEM/Admins 可读写；标准用户仅可读（不可删除）
- 轮转：按日轮转，保留最近 30 天

---

## 8. 无法防御的场景（已知局限）

以下场景超出本程序的防护能力，需配合其他措施：

| 场景 | 建议缓解措施 |
|------|-------------|
| 孩子知道管理员账户密码 | 家长保管好密码，不告知孩子 |
| BIOS 未设密码 + U 盘启动 PE 系统 | 设置 BIOS 密码，禁用 U 盘启动 |
| 重装操作系统 | BIOS 密码 + 家长与孩子沟通 |
| 物理破坏硬件 | 超出软件防护范围 |
| 使用其他设备（手机、平板） | 需配合路由器时间管控 |

---

## 9. 安全更新策略

- 发现安全漏洞后，在 24 小时内评估影响范围
- P0 安全漏洞（保护失效）优先于一切功能开发修复
- 修复发布后，在 `CHANGELOG.md` 中标注 `[security]`

---

*文档维护人：项目负责人*  
*最后更新：2026-05-03*
