using System;

namespace PrismCodingAgent.CodingShell.ViewModels;

internal sealed class FavoritePromptAppliedEventArgs(string prompt) : EventArgs
{
    public string Prompt { get; } = prompt;
}

internal sealed class FavoritePromptRow(string text) : ViewModelBase
{
    private string _draft = text;
    private bool _expandedBeforeEditing;

    public string Text { get; private set; } = text;
    public bool IsNew { get; set; }
    public bool IsEditing { get; private set; }
    public bool IsExpanded { get; private set; }
    public bool CanToggle => !IsEditing && GetPreviewLength(Text) < Text.Length;

    public string DisplayText
    {
        get
        {
            if (IsEditing) return _draft;
            int length = IsExpanded ? Text.Length : GetPreviewLength(Text);
            return length == Text.Length ? Text : Text[..length] + "…";
        }
        set
        {
            if (IsEditing) SetField(ref _draft, value ?? string.Empty);
        }
    }

    internal void Toggle()
    {
        IsExpanded = !IsExpanded;
        NotifyPresentation();
    }

    internal void BeginEdit()
    {
        _draft = Text;
        _expandedBeforeEditing = IsExpanded;
        IsEditing = true;
        IsExpanded = true;
        NotifyPresentation();
    }

    internal void EndEdit(string text)
    {
        Text = text;
        IsNew = false;
        IsEditing = false;
        IsExpanded = _expandedBeforeEditing;
        NotifyPresentation();
    }

    private void NotifyPresentation()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(CanToggle));
        OnPropertyChanged(nameof(DisplayText));
    }

    private static int GetPreviewLength(string text)
    {
        int limit = Math.Min(120, text.Length);
        int lines = 1;
        for (int i = 0; i < limit; i++)
        {
            if ((text[i] == '\n' || (text[i] == '\r' && (i + 1 == text.Length || text[i + 1] != '\n'))) && ++lines > 5)
                return i > 0 && text[i - 1] == '\r' ? i - 1 : i;
        }
        return limit < text.Length && char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit;
    }
}
