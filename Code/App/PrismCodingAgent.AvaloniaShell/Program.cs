using System;
using AtomUI;
using AtomUI.Desktop.Controls;
using Avalonia;
using TKCode.Arc;
using TKCode.Arc.Fonts;

namespace PrismCodingAgent.AvaloniaShell;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .WithArcFonts()
            .UseAtomUI(builder =>
            {
                // Arc 的控件、设计令牌与主题建立在 AtomUI 桌面控件之上，必须先注册桌面控件。
                builder.UseDesktopControls();
                builder.UseArc();
                builder.UseArcSpecial();
            })
            .LogToTrace();
}
