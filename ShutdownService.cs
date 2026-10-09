using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Text.Json;

namespace ShutdownTimerExtension;

internal record ProcessResult(int ExitCode, string Output);
internal record ShutdownRecord(DateTimeOffset Target, DateTimeOffset BootTime);
internal record OperationResult(bool Success, string Message);

internal static class ScheduleInput
{
    public static int Minutes(string text)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes < 1 || minutes > 1440)
            throw new ArgumentException("请输入 1～1440 之间的整数分钟数。");
        return minutes * 60;
    }

    public static int AtTime(string text, DateTimeOffset now)
    {
        if (!TimeOnly.TryParseExact(text, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new ArgumentException("请输入 24 小时时间，例如 23:30。");
        var local = now.LocalDateTime.Date.Add(time.ToTimeSpan());
        if (local <= now.LocalDateTime) local = local.AddDays(1);
        if (TimeZoneInfo.Local.IsInvalidTime(local)) throw new ArgumentException("该时间因夏令时调整不存在，请换一个时间。");
        var target = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        return checked((int)Math.Ceiling((target - now).TotalSeconds));
    }
}

internal sealed class ShutdownService
{
    private readonly object _gate = new();
    private readonly Func<string[], ProcessResult> _run;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<DateTimeOffset> _boot;
    private readonly string _directory;
    private ShutdownRecord? _record;
    public event Action? Changed;

    public ShutdownService(string? directory = null, Func<string[], ProcessResult>? run = null,
        Func<DateTimeOffset>? now = null, Func<DateTimeOffset>? boot = null)
    {
        _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LittleTools", "CmdPalShutdownTimer");
        _run = run ?? RunShutdown;
        _now = now ?? (() => DateTimeOffset.Now);
        _boot = boot ?? (() => DateTimeOffset.Now.AddMilliseconds(-Environment.TickCount64));
        try { _record = JsonSerializer.Deserialize<ShutdownRecord>(File.ReadAllText(Path.Combine(_directory, "state.json"))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public string Status
    {
        get
        {
            lock (_gate)
            {
                if (_record is null || (_boot() - _record.BootTime).Duration() > TimeSpan.FromMinutes(1)) return "本扩展没有当前关机记录";
                var remaining = _record.Target - _now();
                if (remaining <= TimeSpan.Zero) return "关机记录已到期";
                return $"预计 {_record.Target.LocalDateTime:MM-dd HH:mm:ss} 关机 · 剩余 {(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            }
        }
    }

    public OperationResult Schedule(int seconds)
    {
        if (seconds < 1 || seconds > 90000) return new(false, "关机时间必须在未来 25 小时以内。");
        OperationResult result;
        lock (_gate)
        {
            try
            {
                var target = _now().AddSeconds(seconds);
                var process = _run(["/s", "/t", seconds.ToString(CultureInfo.InvariantCulture), "/d", "p:0:0", "/c", "PowerToys 命令面板：定时关机"]);
                if (process.ExitCode != 0) return new(false, Error(process));
                _record = new(target, _boot());
                var warning = Save($"SET {target:O} ({seconds}s)");
                result = new(true, $"已设置 {target.LocalDateTime:MM-dd HH:mm:ss} 关机。{warning}");
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
            { return new(false, $"无法调用 Windows 关机程序：{ex.Message}"); }
        }
        Changed?.Invoke();
        return result;
    }

    public OperationResult Cancel()
    {
        OperationResult result;
        lock (_gate)
        {
            try
            {
                var process = _run(["/a"]);
                if (process.ExitCode != 0 && process.ExitCode != 1116) return new(false, Error(process));
                _record = null;
                var warning = Save("CANCEL");
                result = new(true, (process.ExitCode == 1116 ? "Windows 当前没有等待中的关机。" : "已取消 Windows 定时关机。") + warning);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
            { return new(false, $"取消失败：{ex.Message}"); }
        }
        Changed?.Invoke();
        return result;
    }

    private string Save(string entry)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var state = Path.Combine(_directory, "state.json");
            File.WriteAllText(state + ".tmp", JsonSerializer.Serialize(_record));
            File.Move(state + ".tmp", state, true);
            var log = Path.Combine(_directory, "operations.log");
            if (File.Exists(log) && new FileInfo(log).Length > 1024 * 1024) File.Move(log, log + ".old", true);
            File.AppendAllText(log, $"[{_now():O}] {entry}{Environment.NewLine}");
            return "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return "Windows 操作已成功，但本地记录保存失败。"; }
    }

    private static string Error(ProcessResult result) => result.ExitCode switch
    {
        1190 or 1115 => "Windows 已有等待中的关机或重启。请先取消，再设置新的时间。",
        5 or 1314 => "当前账户没有关闭系统的权限。请检查 Windows 账户权限。",
        _ => $"Windows 操作失败（{result.ExitCode}）：{result.Output.Trim()}",
    };

    private static ProcessResult RunShutdown(string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动 shutdown.exe。");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new(process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
