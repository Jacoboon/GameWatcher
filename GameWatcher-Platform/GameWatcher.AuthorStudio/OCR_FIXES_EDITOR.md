# OCR Fixes Editor - User Guide

## Overview

The **OCR Correction Rules** editor in the Settings tab allows you to manually manage the OCR fix dictionary. These rules automatically correct common OCR errors during text recognition.

## Location

**Settings Tab → OCR Correction Rules**

## Features

### Automatic Learning
OCR fixes are **automatically generated** when you edit dialogue text in the Discovery tab. For example:
- Edit "VVelcome" → "Welcome"
- System learns: `VVelcome` → `Welcome`
- Future OCR captures of "VVelcome" auto-correct to "Welcome"

### Manual Management
You can also manually add, edit, and delete OCR correction rules.

## User Interface

### Toolbar Buttons

| Button | Action | Description |
|--------|--------|-------------|
| ➕ Add Rule | `AddOcrFixCommand` | Adds a blank rule for manual entry |
| 💾 Save All | `SaveOcrFixesCommand` | Saves all rules to `ocr_fixes.json` |
| 🔄 Refresh | `RefreshOcrFixesCommand` | Reloads rules from file |

### DataGrid Columns

| Column | Description | Editable |
|--------|-------------|----------|
| **From (OCR Error)** | The incorrect text from OCR | ✅ Yes |
| **To (Correct Text)** | The corrected text | ✅ Yes |
| **🗑️** | Delete button | N/A |

### Example Rules

| From (OCR Error) | To (Correct Text) | Use Case |
|------------------|-------------------|----------|
| `VVelcome` | `Welcome` | Double-V misread as W |
| `0RBS` | `ORBS` | Zero misread as O |
| `Cornella` | `Corneria` | Location name typo |
| `l` | `I` | Lowercase L misread as uppercase I |
| `rn` | `m` | Common OCR confusion |

## Workflows

### Adding a Rule Manually

1. Click **➕ Add Rule**
2. A blank row appears in the grid
3. Type the OCR error in **From** column
4. Type the correct text in **To** column
5. Click **💾 Save All**
6. Status message: `✓ Saved N OCR fixes`

### Editing an Existing Rule

1. Click in the **From** or **To** cell
2. Edit the text directly in the grid
3. Click **💾 Save All** to persist changes
4. Status message: `✓ Saved N OCR fixes`

### Deleting a Rule

1. Find the rule to delete
2. Click the **🗑️** button in that row
3. Rule is immediately removed and saved
4. Status message: `✓ Deleted OCR fix: 'from' → 'to'`

### Refreshing from File

If you manually edit `ocr_fixes.json`:
1. Click **🔄 Refresh**
2. Grid reloads from file
3. Status message: `✓ OCR fixes refreshed`

## File Format

**Location:** `{PackFolder}/Configuration/ocr_fixes.json`

```json
{
  "fixes": [
    {
      "from": "0rbs",
      "to": "ORBS"
    },
    {
      "from": "cornella",
      "to": "Corneria"
    },
    {
      "from": "vvelcome",
      "to": "Welcome"
    }
  ]
}
```

**Notes:**
- `from` keys are stored **lowercase** internally for case-insensitive matching
- Rules are **alphabetically sorted** when saved
- Empty rules (blank From or To) are automatically removed on save

## Integration Points

### Auto-Generation (Discovery Tab)

When you edit dialogue text:
1. System compares original OCR vs your edit
2. If word count matches, generates 1-to-1 mappings
3. Adds rules to `ocr_fixes.json`
4. Activity Log shows: `✓ Learned 2 OCR corrections: 'VVelcome' → 'Welcome', '0RBS' → 'ORBS'`

### Auto-Loading (Pack Open)

When you open a pack:
1. `OcrFixesStore.LoadFromFolderAsync()` loads `ocr_fixes.json`
2. `SettingsViewModel.LoadOcrFixes()` populates the grid
3. Status message: `✓ Loaded N OCR fixes`

### Auto-Saving (Edit Actions)

Changes are saved:
- When you click **💾 Save All**
- When you delete a rule (auto-saves immediately)
- Rules update `OcrFixesStore` → writes to `ocr_fixes.json`

## Technical Details

### OcrFixEntry Model

**File:** `Models/OcrFixEntry.cs`

```csharp
public partial class OcrFixEntry : ObservableObject
{
    [ObservableProperty]
    private string _from = string.Empty;

    [ObservableProperty]
    private string _to = string.Empty;
}
```

### SettingsViewModel Commands

**File:** `ViewModels/SettingsViewModel.cs`

```csharp
[RelayCommand]
private async Task AddOcrFixAsync()
{
    var newEntry = new OcrFixEntry("", "");
    OcrFixes.Add(newEntry);
    StatusMessage = "➕ New OCR fix added - edit the From and To fields";
}

[RelayCommand]
private async Task DeleteOcrFixAsync(OcrFixEntry entry)
{
    OcrFixes.Remove(entry);
    await SaveOcrFixesAsync();
    StatusMessage = $"✓ Deleted OCR fix: '{entry.From}' → '{entry.To}'";
}

[RelayCommand]
private async Task SaveOcrFixesAsync()
{
    // Remove empty entries
    var emptyEntries = OcrFixes.Where(f => string.IsNullOrWhiteSpace(f.From) || string.IsNullOrWhiteSpace(f.To)).ToList();
    foreach (var empty in emptyEntries) OcrFixes.Remove(empty);

    // Update store and save to file
    var fixes = OcrFixes.Select(f => new KeyValuePair<string, string>(f.From, f.To));
    _ocrFixesStore.SetAll(fixes);
    await _ocrFixesStore.SaveAsync();
    
    StatusMessage = $"✓ Saved {OcrFixes.Count} OCR fixes";
}
```

### OcrFixesStore Methods

**File:** `Services/OcrFixesStore.cs`

```csharp
public IReadOnlyDictionary<string, string> GetAll() => _fixes;

public void SetAll(IEnumerable<KeyValuePair<string, string>> fixes)
{
    _fixes.Clear();
    foreach (var fix in fixes)
    {
        if (!string.IsNullOrWhiteSpace(fix.Key) && !string.IsNullOrWhiteSpace(fix.Value))
        {
            _fixes[fix.Key.Trim().ToLowerInvariant()] = fix.Value.Trim();
        }
    }
}

public bool RemoveFix(string from)
{
    var key = from.Trim().ToLowerInvariant();
    return _fixes.Remove(key);
}
```

## Status Messages

| Message | Meaning |
|---------|---------|
| `✓ Loaded N OCR fixes` | Rules loaded successfully from file |
| `➕ New OCR fix added - edit the From and To fields` | Blank rule added, ready for editing |
| `✓ Saved N OCR fixes` | All rules saved to file |
| `✓ Deleted OCR fix: 'from' → 'to'` | Specific rule deleted |
| `✓ OCR fixes refreshed` | Rules reloaded from file |
| `⚠️ Failed to load OCR fixes: {error}` | Load error occurred |
| `⚠️ Failed to save fixes: {error}` | Save error occurred |

## Tips

### 💡 Best Practices

1. **Use Specific Rules:** Target specific OCR errors, not generic words
2. **Test Corrections:** Verify rules work by running discovery after saving
3. **Case Sensitivity:** "from" is case-insensitive, "to" preserves case
4. **Word-Level Only:** Rules apply to individual words, not phrases
5. **Regular Backups:** Rules are in `ocr_fixes.json` - version control recommended

### 💡 Common Patterns

**Number/Letter Confusion:**
- `0` → `O`
- `1` → `I` or `l`
- `5` → `S`

**Double Characters:**
- `VV` → `W`
- `rn` → `m`
- `cl` → `d`

**Fantasy Names:**
- `Cornella` → `Corneria`
- `Elfheirn` → `Elfheim`
- `Pravoka` → `Provoka`

### 💡 Troubleshooting

**Rules not applying?**
- Click **🔄 Refresh** to reload
- Check rule is saved to `ocr_fixes.json`
- Verify "from" text matches OCR output exactly
- Remember: rules are case-insensitive for matching

**Empty grid?**
- No rules exist yet (normal for new packs)
- Click **➕ Add Rule** to create first rule
- Or edit dialogue in Discovery tab to auto-generate

## Future Enhancements

### Planned Features
- Import/export rule sets from community
- Rule usage statistics (how often each is applied)
- Pattern-based rules (regex support)
- Bulk import from CSV
- Rule testing tool

### Not Implemented
- Phrase-level corrections (multi-word)
- Context-aware rules
- Character-level substitution patterns
- Rule prioritization/ordering
