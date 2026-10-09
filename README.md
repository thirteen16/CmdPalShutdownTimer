# PowerToys 命令面板：定时关机

在命令面板搜索“定时关机”。支持 1～1440 分钟、指定时间、实时倒计时和取消关机。已过的指定时间按明天计算。
提示框显示完整结果并自动换行；错误提示可手动关闭。

Windows 负责计时，关闭命令面板后仍有效。到点会强制关闭应用，请先保存文件。
取消操作会取消 Windows 当前等待中的关机或重启。倒计时为本扩展记录，外部工具修改任务后可能不准确。
状态和日志位于 `%LOCALAPPDATA%\LittleTools\CmdPalShutdownTimer`。

## 安装与更新

需要 Windows 11、PowerToys 命令面板和开发者模式；编译需要 .NET 9 SDK。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
```

已安装运行文件时可加 `-SkipBuild`。安装后在命令面板运行 `Reload`。
运行文件保存在本项目的 `publish` 目录，构建输出保存在 `bin` 和 `obj`。这些本地目录由 `.gitignore` 排除，不上传 GitHub。安装后请保持 `publish` 路径不变。

卸载：运行 `Uninstall.ps1`。卸载不会停止已有关机计时，需要时执行 `shutdown /a`。

## 文件

GitHub 只提交源码、配置、脚本、说明和 `Assets` 图标。`publish`、`bin`、`obj` 等本地生成目录已列入 `.gitignore`，无需上传。克隆仓库后运行安装脚本即可生成运行文件。

- 五个 `.cs` 文件：扩展入口、命令、页面、关机逻辑和提示框。
- `.csproj`、`app.manifest`、`AppxManifest.xml`、`Directory.Build.targets`、`nuget.config`：编译和注册配置。
- `Assets`：三个 PNG 用于应用包和开始菜单；`ShutdownTimer.ico` 用于 EXE。采用红底白色经典电源符号。命令面板图标使用 Segoe Fluent 的电源符号。
- `Install.ps1`、`Uninstall.ps1`：安装与卸载；`THIRD-PARTY-NOTICES.md`：微软模板 MIT 许可。

参考：[微软扩展模型](https://learn.microsoft.com/zh-cn/windows/powertoys/command-palette/extensibility-overview)、[ShutdownTimer](https://github.com/thirteen16/ShutdownTimer)。
