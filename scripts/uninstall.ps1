#Requires -RunAsAdministrator
<#
.SYNOPSIS
    ChildPCGuard 卸载脚本
.DESCRIPTION
    完整移除 ChildPCGuard：停止服务、删除服务、删除计划任务、删除程序文件
    需要输入管理员密码验证后方可执行
.PARAMETER Force
    跳过密码验证（仅用于紧急情况，需管理员确认）
#>
param([switch]$Force)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$InstallDir  = "C:\Program Files\ChildPCGuard"
$DataDir     = "C:\ProgramData\ChildPCGuard"
$ServiceName = "WinSecSvc"
$TaskName    = "ChildPCGuard_AutoShutdown"

function Write-Step { param($msg) Write-Host "  ● $msg" -ForegroundColor Cyan }
function Write-OK   { param($msg) Write-Host "    ✓ $msg" -ForegroundColor Green }
function Write-Warn { param($msg) Write-Host "    ⚠ $msg" -ForegroundColor Yellow }

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Red
Write-Host "   ChildPCGuard 卸载程序" -ForegroundColor Red
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Red
Write-Host ""

if (-not $Force) {
    Write-Host "  ⚠️  卸载 ChildPCGuard 将移除所有管控功能。" -ForegroundColor Yellow
    $confirm = Read-Host "  确认卸载？输入 YES 继续"
    if ($confirm -ne "YES") { Write-Host "  已取消卸载。"; exit 0 }
}

# Step 1: 停止并删除服务
Write-Step "停止并删除服务"
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    & sc.exe delete $ServiceName | Out-Null
    Write-OK "服务已删除: $ServiceName"
} else {
    Write-Warn "服务不存在（可能已删除）"
}

# Step 2: 删除计划任务
Write-Step "删除计划任务"
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
Write-OK "计划任务已删除: $TaskName"

# Step 3: 恢复目录 ACL（允许删除）
Write-Step "恢复目录权限"
foreach ($dir in @($InstallDir, $DataDir)) {
    if (Test-Path $dir) {
        $acl = Get-Acl $dir
        $acl.SetAccessRuleProtection($false, $true)  # 恢复继承
        Set-Acl -Path $dir -AclObject $acl -ErrorAction SilentlyContinue
        Write-OK "权限已恢复: $dir"
    }
}

# Step 4: 删除程序文件
Write-Step "删除程序文件"
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-OK "已删除: $InstallDir"
}

# 询问是否保留数据（日志和配置）
if (-not $Force) {
    $keepData = Read-Host "  是否保留使用记录和日志？(Y/N)"
    if ($keepData -eq "N" -or $keepData -eq "n") {
        Remove-Item -Path $DataDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-OK "已删除: $DataDir"
    } else {
        Write-OK "已保留数据: $DataDir"
    }
} else {
    Remove-Item -Path $DataDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-OK "已删除: $DataDir"
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "   卸载完成！" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
