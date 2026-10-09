$ErrorActionPreference = 'Stop'
Get-AppxPackage -Name 'LittleTools.ShutdownTimer' | Remove-AppxPackage
Write-Host '扩展已卸载。已有 Windows 关机计时不会因卸载停止，需要时请运行 shutdown /a。'
