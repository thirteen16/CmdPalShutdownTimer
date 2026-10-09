using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ShutdownTimerExtension;

internal static class FeedbackPopup
{
    private static readonly object Gate = new();
    private static Dispatcher? _dispatcher;
    private static Window? _window;

    internal static void Show(string message, bool success)
    {
        lock (Gate)
        {
            if (_dispatcher is null)
            {
                using var ready = new ManualResetEvent(false);
                var thread = new Thread(() =>
                {
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    ready.Set();
                    Dispatcher.Run();
                }) { IsBackground = true, Name = "Shutdown feedback" };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                ready.WaitOne();
            }
            _dispatcher!.BeginInvoke(() => ShowWindow(message, success));
        }
    }

    private static bool IsDark => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0;

    private static Border CreateCard(string message, bool success, double width, Action close)
    {
        var dark = IsDark;
        var foreground = dark ? Brushes.White : Brushes.Black;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var closeButton = new Button
        {
            Content = "×", Width = 28, Height = 28, FontSize = 20,
            ToolTip = "关闭提示", Background = Brushes.Transparent,
            Foreground = foreground, BorderThickness = new Thickness(0),
        };
        closeButton.Click += (_, _) => close();
        DockPanel.SetDock(closeButton, Dock.Right);
        header.Children.Add(closeButton);
        header.Children.Add(new TextBlock
        {
            Text = success ? "定时关机" : "操作未完成", FontSize = 16,
            FontWeight = FontWeights.SemiBold, Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(new TextBlock
        {
            Text = message, FontSize = 16, Foreground = foreground,
            TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None,
        });
        return new Border
        {
            Width = width, Padding = new Thickness(22, 16, 22, 20), CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(dark ? Color.FromRgb(32, 32, 32) : Color.FromRgb(250, 250, 250)),
            BorderBrush = new SolidColorBrush(dark ? Color.FromRgb(80, 80, 80) : Color.FromRgb(190, 190, 190)),
            BorderThickness = new Thickness(1), Child = stack,
        };
    }

    private static void ShowWindow(string message, bool success)
    {
        _window?.Close();
        var workArea = SystemParameters.WorkArea;
        var window = new Window
        {
            Title = "定时关机提示", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true, Background = Brushes.Transparent,
            ShowInTaskbar = false, ShowActivated = false, Topmost = true,
            SizeToContent = SizeToContent.WidthAndHeight,
        };
        var card = CreateCard(message, success, Math.Min(520, workArea.Width - 32), window.Close);
        // Normal messages grow to their full wrapped height. Very long system errors remain scrollable.
        window.Content = new ScrollViewer
        {
            Content = card, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = workArea.Height - 64,
        };
        window.ContentRendered += (_, _) =>
        {
            window.Left = workArea.Left + (workArea.Width - window.ActualWidth) / 2;
            window.Top = workArea.Bottom - window.ActualHeight - 48;
        };
        _window = window;
        if (success)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += (_, _) => { timer.Stop(); window.Close(); };
            window.MouseEnter += (_, _) => timer.Stop();
            window.MouseLeave += (_, _) => timer.Start();
            window.Closed += (_, _) => timer.Stop();
            timer.Start();
        }
        window.Show();
    }

}
