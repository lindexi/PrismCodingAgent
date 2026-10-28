[assembly: DoNotParallelize]

namespace PrismCodingAgent.WpfShell.Tests;

/// <summary>释放测试程序集共享的 WPF UI 会话。</summary>
[TestClass]
public sealed class WpfTestAssembly
{
    /// <summary>在测试线程停止共享 Dispatcher。</summary>
    [AssemblyCleanup]
    public static void Cleanup() => WpfTestClassAttribute.Shutdown();
}
