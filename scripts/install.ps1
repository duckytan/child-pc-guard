#Requires -RunAsAdministrator
<#
.SYNOPSIS
    ChildPCGuard 安装脚本（Phase 1 基础版）
.DESCRIPTION
    以管理员权限运行，完成服务注册、目录创建、ACL/DACL 配置、计划任务创建
.PARAMETER Silent
    静默模式，跳过所有用户交互提示，使用默认值
.PARAMETER Password
    管理员密码（静默模式下使用，未提供则生成随机密码）
.PARAMETER DailyLimitWeekday
    工作日每日时长上限（分钟），默认 120
.PARAMETER DailyLimitWeekend
    周末每日时长上限（分钟），默认 240
.PARAMETER ShutdownTime
    自动关机时间，默认 "22:00"
.EXAMPLE
    .\install.ps1
    .\install.ps1 -Silent -Password "MySecret123" -DailyLimitWeekday 90
#>
param(
    [switch]$Silent,
    [string]$Password = "",
    [int]$DailyLimitWeekday = 120,
    [int]$DailyLimitWeekend = 240,
    [string]$ShutdownTime = "22:00"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── 常量 ────────────────────────────────────────────────────────────────────
$InstallDir    = "C:\Program Files\ChildPCGuard"
$DataDir       = "C:\ProgramData\ChildPCGuard"
$LogDir        = "$DataDir\logs"
$ServiceName   = "WinSecSvc"
$ServiceDisplay= "Windows Security Update Service"
$ServiceDesc   = "Provides security update management and system monitoring."
$TaskName      = "ChildPCGuard_AutoShutdown"
$ScriptDir     = Split-Path -Parent $MyInvocation.MyCommand.Path
$BinDir        = Join-Path $ScriptDir ".."

# ── 颜色输出辅助 ────────────────────────────────────────────────────────────
function Write-Step   { param($msg) Write-Host "  ● $msg" -ForegroundColor Cyan }
function Write-OK     { param($msg) Write-Host "    ✓ $msg" -ForegroundColor Green }
function Write-Warn   { param($msg) Write-Host "    ⚠ $msg" -ForegroundColor Yellow }
function Write-Fail   { param($msg) Write-Host "    ✗ $msg" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "   ChildPCGuard 安装程序 v0.1.0" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""

# ── Step 1: 获取管理员密码 ───────────────────────────────────────────────
Write-Step "配置管理员密码"

if (-not $Silent -and -not $Password) {
    do {
        $pwd1 = Read-Host "  请输入管理员密码（解锁时使用）" -AsSecureString
        $pwd2 = Read-Host "  确认密码" -AsSecureString
        $p1 = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
              [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pwd1))
        $p2 = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
              [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pwd2))
        if ($p1 -ne $p2) { Write-Host "  两次密码不一致，请重新输入" -ForegroundColor Red }
        if ($p1.Length -lt 4) { Write-Host "  密码长度至少 4 位" -ForegroundColor Red; $p1 = "" }
    } while ($p1 -ne $p2 -or $p1.Length -lt 4)
    $Password = $p1
} elseif (-not $Password) {
    # 生成随机密码
    $Password = [System.Web.Security.Membership]::GeneratePassword(10, 2)
    Write-Warn "未指定密码，已生成随机密码: $Password （请立即记录！）"
}

Write-OK "密码已配置（BCrypt 哈希将由服务在首次启动时生成）"

# ── Step 2: 创建目录 ──────────────────────────────────────────────────────
Write-Step "创建安装目录"

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path $DataDir    | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir     | Out-Null
Write-OK "目录已创建: $InstallDir"
Write-OK "目录已创建: $DataDir"

# ── Step 3: 复制程序文件 ──────────────────────────────────────────────────
Write-Step "复制程序文件"

$publishDirs = @("GuardService", "LockOverlay", "AgentA", "AgentB", "AdminPanel")
foreach ($dir in $publishDirs) {
    $src = Join-Path $BinDir "publish\$dir"
    $dst = Join-Path $InstallDir $dir
    if (Test-Path $src) {
        Copy-Item -Path "$src\*" -Destination $dst -Recurse -Force
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Write-OK "已复制: $dir"
    } else {
        Write-Warn "未找到发布目录: $src（跳过）"
    }
}

# ── Step 4: 设置目录 ACL（拒绝标准用户写入）────────────────────────────────
Write-Step "配置文件系统 ACL"

foreach ($dir in @($InstallDir, $DataDir)) {
    $acl = Get-Acl $dir
    $acl.SetAccessRuleProtection($true, $false)  # 禁用继承

    # 允许 SYSTEM 完全控制
    $acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new(
        "NT AUTHORITY\SYSTEM", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"))

    # 允许 Administrators 完全控制
    $acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new(
        "BUILTIN\Administrators", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"))

    # 拒绝 Users 写入（可读，但不可修改）
    $acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new(
        "BUILTIN\Users", "Write,Delete,TakeOwnership,ChangePermissions",
        "ContainerInherit,ObjectInherit", "None", "Deny"))

    Set-Acl -Path $dir -AclObject $acl
    Write-OK "ACL 已设置: $dir"
}

# ── Step 5: 注册 Windows 服务 ─────────────────────────────────────────────
Write-Step "注册 Windows 服务"

$guardExe = "$InstallDir\GuardService\GuardService.exe"

# 如果服务已存在，先停止并删除
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Warn "服务已存在，先停止并重新注册"
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

if (Test-Path $guardExe) {
    & sc.exe create $ServiceName `
        binPath= "`"$guardExe`"" `
        start= auto `
        DisplayName= $ServiceDisplay `
        obj= LocalSystem | Out-Null

    & sc.exe description $ServiceName $ServiceDesc | Out-Null
    Write-OK "服务已注册: $ServiceName"

    # Step 6: 配置 SCM 故障恢复（3 次自动重启，间隔 1 秒）
    Write-Step "配置服务故障自动恢复"
    & sc.exe failure $ServiceName reset= 86400 actions= restart/1000/restart/1000/restart/1000 | Out-Null
    Write-OK "故障恢复已配置（1 秒内重启，最多 3 次）"

    # Step 7: 配置服务 DACL（防止普通用户 sc stop）
    Write-Step "配置服务 DACL"
    $sddl = "D:(A;;CCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(D;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;IU)"
    & sc.exe sdset $ServiceName $sddl | Out-Null
    Write-OK "服务 DACL 已设置（普通用户无法停止/删除服务）"
} else {
    Write-Warn "GuardService.exe 未找到（$guardExe），跳过服务注册（构建后重新运行安装脚本）"
}

# ── Step 8: 创建 Windows 任务计划（关机双重保障）────────────────────────────
Write-Step "创建自动关机任务计划"

# 删除已有任务
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

$action  = New-ScheduledTaskAction -Execute "shutdown.exe" -Argument "/s /f /t 0"
$trigger = New-ScheduledTaskTrigger -Daily -At $ShutdownTime
$settings= New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Principal $principal `
    -Description "ChildPCGuard 每日自动关机（双重保障之计划任务）" | Out-Null

Write-OK "计划任务已创建: $TaskName（每日 $ShutdownTime 关机）"

# ── Step 9: 生成初始配置文件（占位，实际配置由服务首次启动时生成）──────────
Write-Step "初始化配置"

$initConfig = @"
{
  "initialPassword": "$Password",
  "weekdayLimitMinutes": $DailyLimitWeekday,
  "weekendLimitMinutes": $DailyLimitWeekend,
  "shutdownTime": "$ShutdownTime"
}
"@
$initConfigPath = "$DataDir\init.json"
$initConfig | Set-Content -Path $initConfigPath -Encoding UTF8
Write-OK "初始化参数已写入: $initConfigPath（首次启动时由服务处理后删除）"

# ── Step 10: 启动服务 ────────────────────────────────────────────────────
if (Test-Path $guardExe) {
    Write-Step "启动服务"
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc.Status -eq "Running") {
        Write-OK "服务已启动"
    } else {
        Write-Warn "服务未能自动启动，请手动检查: sc start $ServiceName"
    }
}

# ── 完成 ─────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "   安装完成！" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "  ⚠️  重要提醒：" -ForegroundColor Yellow
Write-Host "     1. 确保孩子的 Windows 账户为【标准用户】（非管理员）" -ForegroundColor Yellow
Write-Host "     2. 管理员密码请妥善保管，不要告知孩子" -ForegroundColor Yellow
Write-Host "     3. 启动管理界面: $InstallDir\AdminPanel\AdminPanel.exe" -ForegroundColor Yellow
Write-Host ""
