using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AgentLib.Model;

/// <summary>
/// 为记录类型提供属性变更通知。
/// </summary>
public abstract record RecordNotifyBase : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>触发属性变更通知。</summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>在字段值发生变化时更新字段并发出通知。</summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
