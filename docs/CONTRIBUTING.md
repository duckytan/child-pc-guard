# 贡献指南

**项目**：ChildPCGuard  
**版本**：v1.0  
**日期**：2026-05-03

---

## 1. 开发环境准备

### 必要软件

| 软件 | 版本要求 | 获取地址 |
|------|----------|----------|
| .NET SDK | 8.0.x | https://dotnet.microsoft.com/download |
| Visual Studio | 2022 17.8+ | 含 .NET 8 和 WPF 工作负载 |
| Git | 2.40+ | https://git-scm.com |
| Windows | 10 1903+ 或 11 | - |

### 推荐扩展（Visual Studio）

- **xUnit Test Runner**：运行单元测试
- **ReSharper** 或 **CodeMaid**：代码质量辅助

### 克隆仓库

```powershell
git clone https://github.com/duckytan/child-pc-guard.git
cd child-pc-guard
```

---

## 2. 项目结构约定

```
src/
├── ChildPCGuard.Shared/        # 共享库（接口、模型、常量）
├── ChildPCGuard.GuardService/  # 核心守护服务
├── ChildPCGuard.AgentA/        # 双进程互保 Agent A
├── ChildPCGuard.AgentB/        # 双进程互保 Agent B
├── ChildPCGuard.LockOverlay/   # 锁屏覆盖层（WPF）
└── ChildPCGuard.AdminPanel/    # 管理员界面（WPF）

tests/
├── ChildPCGuard.Tests.Unit/    # 单元测试
└── ChildPCGuard.Tests.Integration/ # 集成测试

scripts/
├── install.ps1                 # 安装脚本
└── uninstall.ps1               # 卸载脚本

docs/                           # 工程文档
```

---

## 3. 代码规范

### 3.1 命名规范

遵循 [Microsoft C# 编码约定](https://learn.microsoft.com/zh-cn/dotnet/csharp/fundamentals/coding-style/coding-conventions)：

| 场景 | 规范 | 示例 |
|------|------|------|
| 类、接口、枚举 | PascalCase | `TimeTracker`, `IRuleEngine` |
| 方法、属性 | PascalCase | `GetElapsedToday()` |
| 局部变量、参数 | camelCase | `elapsedMinutes` |
| 私有字段 | `_camelCase` | `_ruleEngine` |
| 常量 | PascalCase | `MaxRetryCount` |
| 接口 | `I` 前缀 | `ITimeTracker` |

### 3.2 注释规范

- 所有 `public` 方法和类必须有 XML 文档注释（`///`）
- 复杂逻辑使用行内注释说明 **"为什么"**，而非 **"是什么"**
- 安全相关代码（ACL 设置、加密）必须注释说明原因和影响

```csharp
/// <summary>
/// 获取今日已使用的电脑时长（分钟），空闲时间不计入。
/// </summary>
/// <returns>今日活跃使用时长（分钟）</returns>
public int GetElapsedToday() { ... }
```

### 3.3 文件组织

- 一个文件只包含一个公共类
- 文件名与类名相同
- 相关接口放在 `Shared` 项目的对应命名空间下

### 3.4 错误处理

- 所有外部调用（P/Invoke、文件 IO、注册表）必须有 try-catch
- 捕获到异常时必须写入日志（不得静默吞掉）
- 不使用裸 `Exception` 捕获，使用具体异常类型

---

## 4. 分支策略

采用简化的 GitFlow：

```
main          ← 始终可发布的稳定代码
├── phase/1   ← Phase 1 开发分支
├── phase/2   ← Phase 2 开发分支
├── fix/xxx   ← 缺陷修复分支（从 main 创建）
└── docs/xxx  ← 文档更新分支
```

### 分支命名规范

| 类型 | 格式 | 示例 |
|------|------|------|
| Phase 开发 | `phase/<N>` | `phase/1` |
| 功能开发 | `feat/<功能名>` | `feat/lock-overlay` |
| 缺陷修复 | `fix/<BUG-ID>` | `fix/bug-001` |
| 文档更新 | `docs/<描述>` | `docs/add-srs` |

### 合并策略

- Phase 分支 → `main`：使用 **Squash Merge**（保持 main 历史清晰）
- 合并前必须确保：构建通过 + 所有测试通过 + Code Review

---

## 5. Commit Message 规范

遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/v1.0.0/)：

```
<type>(<scope>): <description>

[可选 body]

[可选 footer]
```

| type | 用途 |
|------|------|
| `feat` | 新功能 |
| `fix` | 缺陷修复 |
| `docs` | 文档变更 |
| `test` | 测试相关 |
| `refactor` | 重构（不改变功能） |
| `chore` | 构建脚本、依赖更新等 |
| `security` | 安全相关修复 |

**示例：**

```
feat(guard-service): implement TimeTracker with idle detection

- Uses GetLastInputInfo() Win32 API
- Idle threshold configurable (default 5 min)
- Persists state to state.json every 60s

Closes #12
```

---

## 6. Pull Request 规范

PR 描述模板（创建 PR 时使用）：

```markdown
## 变更说明
<!-- 简要描述本次 PR 的目的和主要改动 -->

## 测试情况
- [ ] 单元测试通过（`dotnet test`）
- [ ] 手动测试通过（描述测试场景）
- [ ] 防护测试通过（如涉及安全功能）

## 关联事项
- 关联 Phase：Phase X
- 关联需求：FR-XX

## 截图（如有 UI 变更）
```

---

## 7. 安全注意事项

- **禁止**在代码中硬编码任何密钥、密码或敏感字符串
- **禁止**在日志中输出密码哈希或加密密钥
- 所有 P/Invoke 导入必须注明安全影响
- 修改 ACL/DACL 的代码必须经过 Code Review
- 测试用密码（如 `TestPassword123`）只能出现在测试项目中，不得出现在生产代码

---

## 8. 调试与测试

### 运行单元测试

```powershell
cd src
dotnet test ..\tests\ChildPCGuard.Tests.Unit\
```

### 以调试模式运行 GuardService

```powershell
# 无需注册为服务，直接运行（--console 参数）
dotnet run --project src\ChildPCGuard.GuardService -- --console
```

### 安装到本地测试

```powershell
# 以管理员运行
.\scripts\install.ps1 -Silent -AdminPassword "Dev@Test2026" -WeekdayLimit 5 -WeekendLimit 10 -ShutdownTime "23:59"
```

### 查看服务日志

```powershell
Get-Content "$env:ProgramData\ChildPCGuard\logs\guard.log" -Tail 50 -Wait
```

---

*文档维护人：项目负责人*  
*最后更新：2026-05-03*
