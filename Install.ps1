param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$publishDir = Join-Path $projectDir 'publish'
$existing = Get-AppxPackage -Name 'LittleTools.ShutdownTimer'
if (-not $SkipBuild) {
    if ($existing) {
        $extensionExecutable = Join-Path $existing.InstallLocation 'ShutdownTimerExtension.exe'
        Get-Process -Name ShutdownTimerExtension -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -eq $extensionExecutable } | Stop-Process
    }
    $env:MSBuildEnableWorkloadResolver = 'false'
    dotnet publish (Join-Path $projectDir 'CmdPalShutdownTimer.csproj') -c Release -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw '编译失败，未注册扩展。' }
}
if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'ShutdownTimerExtension.exe'))) { throw '没有发布文件，请先编译。' }
New-Item -ItemType Directory -Path (Join-Path $publishDir 'Public') -Force | Out-Null
if ($existing -and $existing.InstallLocation -ne $publishDir) {
    Remove-AppxPackage -Package $existing.PackageFullName
    $existing = $null
}
[xml]$packageManifest = Get-Content -LiteralPath (Join-Path $publishDir 'AppxManifest.xml') -Raw
$packageVersion = [version]$packageManifest.Package.Identity.Version
if (-not $existing -or $existing.Version -ne $packageVersion) {
    Add-AppxPackage -Register (Join-Path $publishDir 'AppxManifest.xml') -ForceApplicationShutdown
    Write-Host '扩展已注册。打开 PowerToys 命令面板，运行 Reload，再搜索“定时关机”。'
} else {
    Write-Host '现有扩展已更新，未重复注册。打开命令面板运行 Reload；如列表有重复项，请退出并重新打开命令面板。'
}
