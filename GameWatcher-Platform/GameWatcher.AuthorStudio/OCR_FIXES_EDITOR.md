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

**Location:** `%AppData%/GameWatcher/AuthorStudio/ocr_fixes.json` (Engine-level, applies to all packs)

```json
{
  "fixes": [
    {
      "from": "cast le",
      "to": "castle"
    },
    {
      "from": "Cin",
      "to": "On"
    },
    {
      "from": "Ijhen",
      "to": "When"
    }
  ]
}
```

**Notes:**
- `from` keys are **case-sensitive** and preserved exactly as entered
- Matching is **case-insensitive** (e.g., "cin", "Cin", "CIN" all match "Cin" rule)
- Rules support **multi-word patterns** (e.g., "cast le" → "castle")
- Rules are **alphabetically sorted** when saved
- Empty rules (blank From or To) are automatically removed on save

## Integration Points

### Auto-Generation (Discovery Tab)

When you edit dialogue text in Discovery V2:
1. System compares original OCR vs your corrected text
2. Detects word-level and multi-word differences
3. Shows detected fixes with pagination if multiple (e.g., "📋 3 potential fixes detected (showing 1 of 3)")
4. Click "💾 Save OCR Fix" to save the currently displayed fix
5. Navigate between detected fixes using ◀ Previous / Next ▶ buttons
6. Activity Log shows: `✓ Added OCR fix: 'cast le' → 'castle'`

### Auto-Application (During Capture)

When new dialogue is captured:
1. `OcrFixesStore.Apply()` runs two-pass correction:
   - **First pass:** Multi-word pattern replacements (e.g., "cast le" → "castle")
   - **Second pass:** Single-word token replacements (e.g., "Cin" → "On")
2. Corrected text shows in Discovery lists with 🔧 wrench icon
3. Original OCR text preserved for comparison and learning new rules

### Auto-Loading (Startup)

When Author Studio starts:
1. `OcrFixesStore` loads from `%AppData%/GameWatcher/AuthorStudio/ocr_fixes.json`
2. Rules apply globally to all packs
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
3. **Case Sensitivity:** Matching is case-insensitive, but "to" value preserves case for output
4. **Multi-Word Support:** Use multi-word patterns for spaced errors (e.g., "cast le" → "castle")
5. **Regular Backups:** Rules are in AppData - consider exporting periodically

### 💡 Common Patterns

**Multi-Word OCR Errors:**
- `cast le` → `castle`
- `m ore` → `more`
- `t he` → `the`

**Number/Letter Confusion:**
- `Cin` → `On`
- `0` → `O`
- `1` → `I` or `l`
- `5` → `S`

**Double Characters:**
- `VV` → `W`
- `rn` → `m`
- `cl` → `d`

**Fantasy Names:**
- `Ijhen` → `When`
- `Cornella` → `Corneria`
- `Elfheirn` → `Elfheim`

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

### Recently Implemented ✅
- ✅ Multi-word pattern support (e.g., "cast le" → "castle")
- ✅ Engine-level storage (applies to all packs globally)
- ✅ Case-insensitive matching with case-preserved output
- ✅ Multi-fix detection and pagination in Discovery V2
- ✅ Visual indicators (🔧 wrench icon) for OCR-corrected lines
- ✅ Two-pass application (multi-word first, then single-word)

### Not Implemented
- Context-aware rules
- Character-level substitution patterns
- Rule prioritization/ordering (currently alphabetical)
