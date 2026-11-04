using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PrismCodingAgent.WpfShell.Tests;

internal static class WpfInput
{
    internal static async Task WaitAsync(Func<bool> condition, string description, TimeSpan? timeout = null)
    {
        var clock = Stopwatch.StartNew();
        TimeSpan deadline = timeout ?? TimeSpan.FromSeconds(5);
        while (!condition())
        {
            WpfTestSession.Cancellation.ThrowIfCancellationRequested();
            if (clock.Elapsed >= deadline)
            {
                throw new TimeoutException(description);
            }
            await Task.Delay(20, WpfTestSession.Cancellation);
        }
    }

    internal static string ReadClipboard(TestContext context)
    {
        Application.Current.Dispatcher.VerifyAccess();
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
        uint before = GetClipboardSequenceNumber();
        nint owner = GetClipboardOwner();
        GetWindowThreadProcessId(owner, out uint ownerPid);
        IDataObject? data = Clipboard.GetDataObject();
        string text = Clipboard.GetText();
        string unicode = Clipboard.GetText(TextDataFormat.UnicodeText);
        string diagnostic = $"Clipboard STA; sequence={before}->{GetClipboardSequenceNumber()}; owner=0x{owner:X}; ownerPid={ownerPid}; testPid={Environment.ProcessId}; formats={string.Join(',', data?.GetFormats(false) ?? [])}; convertedFormats={string.Join(',', data?.GetFormats(true) ?? [])}; defaultLength={text.Length}; unicodeLength={unicode.Length}; dataTextLength={(data?.GetData(DataFormats.Text, false) as string)?.Length}; dataUnicodeLength={(data?.GetData(DataFormats.UnicodeText, false) as string)?.Length}";
        context.WriteLine(diagnostic);
        System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "clipboard-format-diagnostics.txt"), diagnostic + Environment.NewLine);
        return text;
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern nint GetClipboardOwner();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    internal static async Task ClickAsync(FrameworkElement target)
    {
        target.Dispatcher.VerifyAccess();
        Window? window = Window.GetWindow(target);
        if (window is not null)
        {
            window.Activate();
            SetForegroundWindow(new WindowInteropHelper(window).Handle);
            await WaitAsync(() => window.IsActive, "Window did not activate.");
        }
        await target.Dispatcher.InvokeAsync(() => target.UpdateLayout(),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await WaitAsync(() => target.IsVisible && target.ActualWidth > 0 && target.ActualHeight > 0,
            "Input target was not laid out.");
        Point point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
        if (!SetCursorPos((int)point.X, (int)point.Y))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        await target.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var hit = window?.InputHitTest(window.PointFromScreen(point)) as FrameworkElement;
        await WaitAsync(() => target.IsMouseOver,
            $"Cursor did not reach target {target.Name}; screen={point}; hit={hit?.GetType().Name}/{hit?.Name}; target size={target.ActualWidth}x{target.ActualHeight}.");
        Send(new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x0002 } } },
            new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x0004 } } });
    }

    internal static async Task RightClickAsync(FrameworkElement target, Point? position = null)
    {
        target.Dispatcher.VerifyAccess();
        Window? window = Window.GetWindow(target);
        if (window is not null)
        {
            window.Activate();
            SetForegroundWindow(new WindowInteropHelper(window).Handle);
            await WaitAsync(() => window.IsActive, "Window did not activate for right click.");
        }
        await target.Dispatcher.InvokeAsync(target.UpdateLayout,
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Point point = target.PointToScreen(position ?? new Point(target.ActualWidth / 2, target.ActualHeight / 2));
        if (!SetCursorPos((int)point.X, (int)point.Y))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        await target.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await WaitAsync(() => target.IsMouseOver, $"Cursor did not reach right-click target {target.Name}.");
        Send(new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x0008 } } },
            new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x0010 } } });
    }

    internal static async Task HoverAsync(FrameworkElement target)
    {
        Point point = target.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2));
        if (!SetCursorPos((int)point.X, (int)point.Y))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        await target.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await WaitAsync(() => target.IsMouseOver, "Cursor did not reach hover target.");
    }

    internal static async Task WheelAsync(FrameworkElement target, int delta)
    {
        await ClickAsync(target);
        Send(new Input { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = 0x0800, Data = unchecked((uint)delta) } } });
    }

    internal static void Key(ushort virtualKey)
        => Send(Keyboard(virtualKey, 0, 0), Keyboard(virtualKey, 0, 2));

    internal static void ControlKey(ushort virtualKey)
        => Send(Keyboard(0x11, 0, 0), Keyboard(virtualKey, 0, 0), Keyboard(virtualKey, 0, 2), Keyboard(0x11, 0, 2));

    internal static void Text(string text)
    {
        foreach (char character in text)
        {
            Send(Keyboard(0, character, 4), Keyboard(0, character, 6));
        }
    }

    private static Input Keyboard(ushort key, ushort scan, uint flags)
        => new() { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Key = key, Scan = scan, Flags = flags } } };

    private static void Send(params Input[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input { internal uint Type; internal InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] internal MouseInput Mouse;
        [FieldOffset(0)] internal KeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { internal ushort Key, Scan; internal uint Flags, Time; internal nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
}
