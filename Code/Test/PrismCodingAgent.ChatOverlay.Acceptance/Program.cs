using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AgentLib.Model;
using Microsoft.Extensions.AI;
using PrismCodingAgent.CodingShell;
using PrismCodingAgent.CodingShell.ViewModels;
using PrismCodingAgent.CodingShell.Views;

internal static class Program
{
    private static readonly List<string> Results = [];

    [STAThread]
    private static int Main()
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        var app = new App(false) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.InitializeComponent();
        int code = 1;
        app.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await VerifyAsync(app);
                code = 0;
            }
            catch (Exception exception)
            {
                Results.Add("FAIL " + exception);
            }
            finally
            {
                File.WriteAllLines("overlay-evidence.txt", Results);
                foreach (string result in Results) Console.WriteLine(result);
                app.Shutdown(code);
            }
        });
        app.Run();
        return code;
    }

    private static async Task VerifyAsync(App app)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "Data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "AgentConfiguration.json"), """{"PrimaryModel":"Offline","OpenAIConfigurationList":[{"EndPoint":"http://127.0.0.1:1/v1","Key":"offline","ModelDefinitions":[{"ModelName":"Offline","ModelId":"offline","Provider":"openai"}]}]}""");
        var window = (MainWindow)await app.OpenAsync(["--data-directory", root]);
        var model = (MainViewModel)window.DataContext;
        var chat = model.ChatViewModel;
        var layer = (Grid)window.FindName("ChatOverlayLayer");
        var panel = (ChatSettingsView)window.FindName("ChatSettingsPanel");
        var composer = (ChatView)window.FindName("ChatComposer");
        var input = (TextBox)composer.FindName("MessageInputTextBox");
        var toggle = (CheckBox)panel.FindName("ChatAutomaticCompressionToggle");
        window.Width = 1280;
        window.Height = 800;
        window.Activate();
        SetForegroundWindow(new WindowInteropHelper(window).Handle);
        await PumpAsync();
        Results.Add($"Initial layer visibility={layer.Visibility}/{layer.IsVisible}; background={layer.Background}; panel={panel.Visibility}; models={chat.AvailableModels.Count}; active={window.IsActive}");
        Check(layer.IsVisible && layer.Background is null && panel.Visibility == Visibility.Collapsed,
            "Fixed empty layer has no background; panel alone is collapsed");
        Point center = input.TranslatePoint(new Point(input.ActualWidth / 2, input.ActualHeight / 2), window);
        Check(IsWithin(window.InputHitTest(center) as DependencyObject, input), "Empty layer hit reaches underlying input");
        await ClickAsync(input);
        Text("overlay draft");
        await PumpAsync();
        Check(input.IsKeyboardFocused && input.Text == "overlay draft", "Native click and typing reach input");
        Key(0x25);
        await PumpAsync();
        Check(input.CaretIndex == input.Text.Length - 1, "Native Left moves caret");
        keybd_event(0x11, 0, 0, 0); Key(0x41); keybd_event(0x11, 0, 2, 0);
        await PumpAsync();
        Check(input.SelectedText == "overlay draft", "Native Ctrl+A selects input text");
        for (int i = 0; i < 40; i++)
        {
            Key(0x09);
            await PumpAsync();
            Check(!IsWithin(Keyboard.FocusedElement as DependencyObject, panel), $"Hidden panel excluded from Tab {i + 1}");
        }
        Check(!toggle.Focus(), "Hidden settings cannot receive focus");
        var hiddenPeer = UIElementAutomationPeer.CreatePeerForElement(toggle)!;
        Check(hiddenPeer.IsOffscreen() && !hiddenPeer.IsKeyboardFocusable(), "Hidden settings AutomationPeer is offscreen and not keyboard focusable");
        AutomationProperties.SetAutomationId(input, "OverlayAcceptanceInput");
        AutomationProperties.SetAutomationId(toggle, "OverlayAcceptanceHiddenToggle");
        nint handle = new WindowInteropHelper(window).Handle;
        await Task.Run(() =>
        {
            var automationWindow = AutomationElement.FromHandle(handle);
            var found = automationWindow.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "OverlayAcceptanceInput"));
            Check(found is not null && found.Current.IsEnabled && !found.Current.IsOffscreen, "UIA locates underlying editable input");
            var hidden = automationWindow.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "OverlayAcceptanceHiddenToggle"));
            Check(hidden is null || (hidden.Current.IsOffscreen && !hidden.Current.IsKeyboardFocusable), "UIA hidden settings absent or offscreen and not focusable");
            if (hidden is not null)
            {
                bool rejected = false;
                try { hidden.SetFocus(); }
                catch (InvalidOperationException) { rejected = true; }
                catch (ElementNotAvailableException) { rejected = true; }
                Check(rejected, "UIA rejects focus on hidden settings");
            }
        });
        await ClickAsync((Button)composer.FindName("OpenChatSettingsButton"));
        Check(chat.IsChatSettingsOpen && panel.IsVisible && layer.IsVisible, "Existing command opens panel without hiding layer");
        Check(panel.Background is SolidColorBrush brush && brush.Color.A == 255 && brush.Opacity == 1,
            "Shown settings background is opaque");
        Check(IsWithin(window.InputHitTest(center) as DependencyObject, panel), "Shown panel intercepts underlying input hit");
        string before = input.Text;
        await ClickAtAsync(window.PointToScreen(center));
        Text("blocked");
        await PumpAsync();
        Check(input.Text == before && !input.IsKeyboardFocused, "Shown settings prevents pointer typing into underlying input");
        int previousCount = chat.Messages.Count;
        chat.StopLanguageServerCommand.Execute(null);
        for (int i = 0; i < 100 && chat.Messages.Count == previousCount; i++) await PumpAsync();
        await PumpAsync();
        var history = (ItemsControl)window.FindName("HistoryMessages");
        Check(history.Items.Count > previousCount && history.ItemContainerGenerator.ContainerFromIndex(history.Items.Count - 1) is not null,
            "Messages continue projecting while settings covers history");
        await ClickAsync((Button)panel.FindName("CloseChatSettingsButton"));
        await ClickAsync(input);
        Text("reopened");
        await PumpAsync();
        Check(input.IsKeyboardFocused && input.Text.Contains("reopened"), "Closing settings allows native refocus and typing");
        Check(IsWithin(window.InputHitTest(center) as DependencyObject, input), "Closed panel restores input hit");
        await model.DisposeAsync();
    }

    private static bool IsWithin(DependencyObject? target, DependencyObject ancestor)
    {
        while (target is not null)
        {
            if (ReferenceEquals(target, ancestor)) return true;
            target = VisualTreeHelper.GetParent(target);
        }
        return false;
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        Results.Add("PASS " + name);
    }

    private static async Task PumpAsync()
    {
        await Task.Delay(60);
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static Task ClickAsync(FrameworkElement element) => ClickAtAsync(element.PointToScreen(new Point(element.ActualWidth / 2, element.ActualHeight / 2)));

    private static async Task ClickAtAsync(Point point)
    {
        if (!SetCursorPos((int)point.X, (int)point.Y)) throw new InvalidOperationException("SetCursorPos failed");
        mouse_event(2, 0, 0, 0, 0);
        mouse_event(4, 0, 0, 0, 0);
        await PumpAsync();
    }

    private static void Key(byte key) { keybd_event(key, 0, 0, 0); keybd_event(key, 0, 2, 0); }
    private static void Text(string text)
    {
        Clipboard.SetText(text);
        keybd_event(0x11, 0, 0, 0); Key(0x56); keybd_event(0x11, 0, 2, 0);
    }

    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint handle);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, nuint extra);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extra);
}
