# GitHub Actions

本项目使用 GitHub Actions 进行持续集成和持续部署 (CI/CD)。

## 工作流

### 1. Build and Test (build.yml)

触发条件:
- 推送到 `main` 或 `develop` 分支
- 针对 `main` 或 `develop` 分支的 Pull Request

执行步骤:
1. 检出代码
2. 设置 .NET 8.0 环境
3. 还原 NuGet 依赖
4. 编译解决方案 (Release 配置)
5. 运行单元测试
6. 发布各组件 (GuardService, AdminPanel, AgentA, AgentB) 为独立可执行文件
7. 上传构建产物
8. (仅 main 分支) 创建发布包并上传

### 2. Code Quality (lint.yml)

触发条件:
- 推送到 `main` 或 `develop` 分支
- 针对 `main` 或 `develop` 分支的 Pull Request

执行步骤:
1. 检出代码
2. 设置 .NET 8.0 环境
3. 安装 dotnet-format 工具
4. 检查代码格式是否符合规范
5. 运行编译器分析器 (警告视为错误)

## 使用说明

### 查看构建状态

每次推送后,可以在仓库的 "Actions" 标签页查看构建状态和日志。

### 下载构建产物

构建成功后,可以从 Actions 运行详情中下载以下产物:
- `ChildPCGuard-Build-{commit-sha}` - 包含所有组件的构建产物
- `Release-Package` - 发布压缩包 (仅 main 分支)

### 本地测试工作流

使用 [act](https://github.com/nektos/act) 在本地测试 GitHub Actions:

```powershell
# 安装 act (chocolatey)
choco install act-cli

# 运行所有工作流
act push

# 运行特定工作流
act -W .github/workflows/build.yml
```

## 构建产物说明

构建产物包含以下独立可执行程序:

| 组件 | 目录 | 说明 |
|------|------|------|
| GuardService | `artifacts/GuardService/` | 守护服务 (作为 Windows Service 运行) |
| AdminPanel | `artifacts/AdminPanel/` | 管理员控制面板 (WPF 应用) |
| AgentA | `artifacts/AgentA/` | 守护代理 A (伪装为 WinSecHelperA.exe) |
| AgentB | `artifacts/AgentB/` | 守护代理 B (伪装为 WinSecHelperB.exe) |

所有构建产物均:
- 使用 Release 配置编译
- 为 `win-x64` 平台构建
- 包含所有运行时依赖 (self-contained)
- 可直接在 Windows 10/11 上运行

## 注意事项

- 构建需要 Windows 环境 (runs-on: windows-latest)
- .NET 8.0 SDK 必须已安装
- 所有测试必须通过才能构建成功
- 代码格式检查失败会导致工作流失败
