using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ShutdownTimerExtension;

[Guid("C5020CE9-650D-4379-A87D-BB16BE2A79D5")]
public sealed partial class ShutdownTimerExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _disposed;
    private readonly ShutdownCommandsProvider _provider = new();
    public ShutdownTimerExtension(ManualResetEvent disposed) => _disposed = disposed;
    public object? GetProvider(ProviderType type) => type == ProviderType.Commands ? _provider : null;
    public void Dispose() { _provider.Dispose(); _disposed.Set(); }
}

public sealed partial class ShutdownCommandsProvider : CommandProvider, IDisposable
{
    private readonly ShutdownPage _page = new(new ShutdownService());
    public ShutdownCommandsProvider()
    {
        Id = "LittleTools.ShutdownTimer";
        DisplayName = "定时关机";
        Icon = new IconInfo("\uE7E8");
    }
    public override ICommandItem[] TopLevelCommands() => [
        new CommandItem(_page) { Title = "定时关机", Subtitle = "设置时长或指定时间 · shutdown timer" },
        new CommandItem(new CancelShutdownCommand(_page.Service)) { Title = "取消定时关机", Subtitle = "取消 Windows 当前等待中的关机或重启" },
    ];
    public override void Dispose() { _page.Dispose(); base.Dispose(); }
}
