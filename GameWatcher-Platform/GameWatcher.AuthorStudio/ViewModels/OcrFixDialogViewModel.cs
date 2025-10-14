using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using GameWatcher.AuthorStudio.Models;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// ViewModel for OCR Fix Creation Dialog.
/// Handles validation, duplicate detection, and impact preview.
/// </summary>
public partial class OcrFixDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fromText = string.Empty;

    [ObservableProperty]
    private string _toText = string.Empty;

    [ObservableProperty]
    private bool _caseInsensitive = true;

    [ObservableProperty]
    private bool _wholeWordOnly = false;

    [ObservableProperty]
    private ObservableCollection<OcrFixEntry> _existingFixes = new();

    [ObservableProperty]
    private string _targetFilePath = string.Empty;

    [ObservableProperty]
    private string _impactSummary = "0 other lines will be updated";

    [ObservableProperty]
    private bool _hasDuplicate;

    [ObservableProperty]
    private bool _hasValidationError;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isValid;

    public OcrFixDialogViewModel()
    {
        // Watch for changes to validate
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FromText) || e.PropertyName == nameof(ToText))
            {
                Validate();
                CheckForDuplicates();
            }
        };
    }

    /// <summary>
    /// Initialize the dialog with context from the discovery session.
    /// </summary>
    public void Initialize(
        string fromText, 
        string toText, 
        IEnumerable<OcrFixEntry> existingFixes, 
        string targetFilePath,
        int discoveredLinesCount)
    {
        FromText = fromText;
        ToText = toText;
        ExistingFixes = new ObservableCollection<OcrFixEntry>(existingFixes);
        TargetFilePath = targetFilePath;
        
        // Calculate impact (simplified - real version would check actual matches)
        ImpactSummary = $"Approximately {discoveredLinesCount} discovered lines will be re-processed";
        
        Validate();
        CheckForDuplicates();
    }

    /// <summary>
    /// Validate the from/to fields.
    /// </summary>
    private void Validate()
    {
        HasValidationError = false;
        ValidationMessage = string.Empty;
        IsValid = true;

        if (string.IsNullOrWhiteSpace(FromText))
        {
            HasValidationError = true;
            ValidationMessage = "\"From\" field cannot be empty";
            IsValid = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(ToText))
        {
            HasValidationError = true;
            ValidationMessage = "\"To\" field cannot be empty";
            IsValid = false;
            return;
        }

        if (string.Equals(FromText.Trim(), ToText.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            HasValidationError = true;
            ValidationMessage = "\"From\" and \"To\" cannot be the same";
            IsValid = false;
            return;
        }
    }

    /// <summary>
    /// Check if a similar rule already exists.
    /// </summary>
    private void CheckForDuplicates()
    {
        if (string.IsNullOrWhiteSpace(FromText))
        {
            HasDuplicate = false;
            return;
        }

        // Check for exact or similar matches
        HasDuplicate = ExistingFixes.Any(fix => 
            string.Equals(fix.From, FromText.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fix.To, ToText.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Create the OCR fix entry to be saved.
    /// </summary>
    public OcrFixEntry CreateFixEntry()
    {
        return new OcrFixEntry
        {
            From = FromText.Trim(),
            To = ToText.Trim()
        };
    }
}
