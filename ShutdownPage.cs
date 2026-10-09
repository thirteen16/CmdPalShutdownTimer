using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ShutdownTimerExtension;

internal sealed partial class ShutdownPage : ListPage, IDisposable
{
    internal ShutdownService Service { get; }
    private readonly ListItem _status;
    private readonly Timer _timer;

    internal ShutdownPage(ShutdownService service)
    {
        Service = service;
        Name = "打开";
        Title = "定时关机";
        Icon = new IconInfo("\uE7E8");
        _status = new ListItem(new NoOpCommand()) { Title = "关机倒计时", Subtitle = service.Status };
        service.Changed += Refresh;
        _timer = new Timer(_ => Refresh(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void Refresh() => _status.Subtitle = Service.Status;

    public override IListItem[] GetItems() => [
        _status,
        new ListItem(new CancelShutdownCommand(Service)) { Title = "取消定时关机", Subtitle = "取消 Windows 当前等待中的关机或重启" },
        new ListItem(new ScheduleFormPage(Service, false, 60)) { Title = "自定义时长", Subtitle = "输入 1～1440 分钟" },
        new ListItem(new ScheduleFormPage(Service, true)) { Title = "指定时间关机", Subtitle = "输入 23:30 等时间；已过的时间按明天计算" },
        new ListItem(new ScheduleFormPage(Service, false, 30)) { Title = "30 分钟后关机", Subtitle = "打开设置页确认" },
        new ListItem(new ScheduleFormPage(Service, false, 60)) { Title = "1 小时后关机", Subtitle = "打开设置页确认" },
        new ListItem(new ScheduleFormPage(Service, false, 120)) { Title = "2 小时后关机", Subtitle = "打开设置页确认" },
        new ListItem(new NoOpCommand()) { Title = "关于状态记录", Subtitle = "外部工具取消或更改任务后，倒计时记录可能不准确；重启后记录失效" },
    ];

    public void Dispose() { Service.Changed -= Refresh; _timer.Dispose(); }
}

internal sealed partial class CancelShutdownCommand : InvokableCommand
{
    private readonly ShutdownService _service;
    internal CancelShutdownCommand(ShutdownService service)
    { _service = service; Name = "取消关机"; Icon = new IconInfo("\uE711"); }
    public override ICommandResult Invoke()
    {
        var result = _service.Cancel();
        FeedbackPopup.Show(result.Message, result.Success);
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class ScheduleFormPage : ContentPage
{
    private readonly ShutdownForm _form;
    internal ScheduleFormPage(ShutdownService service, bool atTime, int minutes = 60)
    {
        Name = "设置";
        Title = atTime ? "指定时间关机" : "设置关机时长";
        Icon = new IconInfo("\uE7E8");
        _form = new ShutdownForm(service, atTime, minutes);
    }
    public override IContent[] GetContent() => [_form];
}

internal sealed partial class ShutdownForm : FormContent
{
    private readonly ShutdownService _service;
    private readonly bool _atTime;
    internal ShutdownForm(ShutdownService service, bool atTime, int minutes)
    {
        _service = service;
        _atTime = atTime;
        var input = atTime
            ? new JsonObject { ["type"] = "Input.Text", ["id"] = "value", ["label"] = "关机时间（24 小时制）", ["placeholder"] = "23:30", ["value"] = "23:30", ["isRequired"] = true, ["errorMessage"] = "请输入时间，例如 23:30" }
            : new JsonObject { ["type"] = "Input.Number", ["id"] = "value", ["label"] = "多少分钟后关机", ["value"] = minutes, ["min"] = 1, ["max"] = 1440, ["isRequired"] = true, ["errorMessage"] = "请输入 1～1440 的整数" };
        TemplateJson = new JsonObject
        {
            ["type"] = "AdaptiveCard", ["version"] = "1.5",
            ["body"] = new JsonArray(
                new JsonObject { ["type"] = "TextBlock", ["text"] = atTime ? "已过的时间按明天计算。" : "范围：1～1440 分钟（最长 24 小时）。", ["wrap"] = true },
                input,
                new JsonObject { ["type"] = "TextBlock", ["text"] = "${feedback}", ["wrap"] = true, ["isVisible"] = "${hasFeedback}", ["separator"] = true },
                new JsonObject { ["type"] = "TextBlock", ["text"] = "关闭命令面板后计时仍有效。Windows 到点会强制关闭应用，请先保存文件。已有任务时请先取消，再重新设置。", ["wrap"] = true }
            ),
            ["actions"] = new JsonArray(new JsonObject { ["type"] = "Action.Submit", ["title"] = "开始定时关机" }),
        }.ToJsonString();
        DataJson = new JsonObject { ["feedback"] = "", ["hasFeedback"] = false }.ToJsonString();
    }

    private ICommandResult ShowFeedback(string message, bool success)
    {
        DataJson = new JsonObject { ["feedback"] = message, ["hasFeedback"] = true }.ToJsonString();
        FeedbackPopup.Show(message, success);
        return CommandResult.KeepOpen();
    }

    public override ICommandResult SubmitForm(string inputs)
    {
        try
        {
            var payload = JsonNode.Parse(inputs)?.AsObject() ?? throw new ArgumentException("无法读取表单。");
            var value = payload["value"]?.ToString().Trim() ?? "";
            var seconds = _atTime ? ScheduleInput.AtTime(value, DateTimeOffset.Now) : ScheduleInput.Minutes(value);
            var result = _service.Schedule(seconds);
            return ShowFeedback(result.Message, result.Success);
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException or InvalidOperationException or OverflowException)
        { return ShowFeedback(ex.Message, false); }
    }
}
