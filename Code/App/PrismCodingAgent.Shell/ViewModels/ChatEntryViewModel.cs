using System;

namespace PrismCodingAgent.CodingShell.ViewModels;

/// <summary>时间线显示项的公共基类。</summary>
public abstract class ChatEntryViewModel : ViewModelBase, IDisposable
{
    /// <summary>条目的显示正文。</summary>
    public abstract string Content { get; }
    /// <inheritdoc />
    public abstract void Dispose();
}