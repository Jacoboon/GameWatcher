using CommunityToolkit.Mvvm.ComponentModel;

namespace GameWatcher.AuthorStudio.Models;

/// <summary>
/// Represents an OCR correction rule (From → To).
/// </summary>
public partial class OcrFixEntry : ObservableObject
{
    [ObservableProperty]
    private string _from = string.Empty;

    [ObservableProperty]
    private string _to = string.Empty;

    public OcrFixEntry()
    {
    }

    public OcrFixEntry(string from, string to)
    {
        From = from;
        To = to;
    }
}
