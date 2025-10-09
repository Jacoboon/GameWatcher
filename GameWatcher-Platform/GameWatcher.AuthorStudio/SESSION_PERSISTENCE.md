# Discovery Session Persistence

## Overview

The AuthorStudio's Discovery tab maintains separate **Discovered** and **Accepted** dialogue lists that persist across app restarts. Session data is automatically saved and is **pack-specific** to prevent cross-contamination when switching between different game packs.

## Architecture

### SessionStore Service

**Location:** `Services/SessionStore.cs`

**Responsibilities:**
- Persists Discovered and Accepted dialogue lists to JSON files
- Keys session files by pack folder path (using SHA256 hash for safe filenames)
- Provides automatic save/load based on current pack context
- Prevents data leakage between different pack projects

**Storage Location:**
```
%AppData%/GameWatcher/AuthorStudio/sessions/
  └── session_{packhash}.json
```

### Session File Format

```json
{
  "packPath": "C:\\Packs\\FF1.PixelRemaster",
  "lastSaved": "2025-10-08T14:30:00Z",
  "discovered": [
    {
      "text": "Welcome to Cornelia!",
      "originalOcrText": "VVelcome to Cornelia!",
      "speakerId": "King of Cornelia",
      "audioPath": null,
      "timestamp": "2025-10-08T14:15:00Z"
    }
  ],
  "accepted": [
    {
      "text": "The ORBS have lost their power.",
      "originalOcrText": "The 0RBS have lost their power.",
      "speakerId": "Princess Sara",
      "audioPath": "voices/Princess Sara/line_001.mp3",
      "timestamp": "2025-10-08T14:20:00Z"
    }
  ]
}
```

## Workflow

### Opening a Pack

1. User opens pack via "Open Pack" button or Recent Packs list
2. `PackBuilderViewModel.OpenPackAsync(folderPath)` is called
3. `DiscoveryViewModel.LoadPackSessionAsync(folderPath)` loads session:
   - Sets SessionStore's current pack context
   - Loads Discovered and Accepted lists from `session_{hash}.json`
   - Merges pack's catalogue entries with session discoveries
4. Session data appears in Discovery tab grids

### Auto-Save Behavior

- **Trigger:** Any change to Discovered or Accepted collections
- **Mechanism:** `CollectionChanged` event handlers in DiscoveryViewModel
- **Action:** Calls `SessionStore.SaveSessionAsync()` in background
- **Result:** Session file updated immediately (no manual save needed)

### Switching Packs

When opening a different pack:
1. `LoadPackSessionAsync(newPackPath)` is called
2. Current lists are cleared
3. SessionStore context switches to new pack hash
4. New pack's session is loaded (or empty if first time)

**Safety:** Previous pack's session remains saved in its own file and won't be affected.

### Starting Fresh

To reset a pack's session:
- Call `DiscoveryViewModel.ClearPackSessionAsync()`
- This clears lists and deletes the session file

## Integration Points

### DiscoveryViewModel

**Constructor:**
```csharp
public DiscoveryViewModel(
    ILogger<DiscoveryViewModel> logger,
    DiscoveryService discoveryService,
    SpeakerStore speakerStore,
    SessionStore sessionStore)  // <-- Injected
```

**Key Methods:**
- `LoadPackSessionAsync(packPath)` - Load session for pack
- `ClearPackSessionAsync()` - Clear session data
- `AutoSaveSessionAsync()` - Background save (private)

**Auto-Save Wiring:**
```csharp
_discoveredDialogue.CollectionChanged += (s, e) => _ = AutoSaveSessionAsync();
_acceptedDialogue.CollectionChanged += (s, e) => _ = AutoSaveSessionAsync();
```

### PackBuilderViewModel

**Dependency:**
```csharp
private readonly DiscoveryViewModel _discoveryViewModel;
```

**OpenPackAsync Integration:**
```csharp
public async Task OpenPackAsync(string folderPath)
{
    // Load the pack's session data first
    await _discoveryViewModel.LoadPackSessionAsync(folderPath);
    
    var (name, display, version, entries) = await _packLoader.LoadAsync(folderPath);
    // ... rest of pack loading
}
```

## User Experience

### Discovery Tab Workflow

1. **Capture Phase:**
   - Start discovery session
   - OCR captures dialogue → appears in Discovered grid
   - Auto-saved continuously

2. **Review Phase:**
   - Edit OCR mistakes
   - Assign speakers
   - Click green ✓ Accept button → moves to Accepted grid
   - Click orange ↩ Demote button (in Accepted grid) → moves back to Discovered

3. **Export Phase:**
   - Accepted entries are marked "Ready for Export"
   - Pack Builder exports both Discovered and Accepted to catalogue
   - Session preserved for next authoring session

### Data Safety

**Scenarios Handled:**
- ✅ App crash → Session auto-saved, recoverable on restart
- ✅ Switch packs → Each pack has isolated session data
- ✅ Duplicate pack paths → Same session file loaded (consistent)
- ✅ Rename pack folder → New session created (old one orphaned but safe)
- ✅ Multiple AuthorStudio instances → Last write wins (file locking not implemented)

**Edge Cases:**
- Empty session file → Loads as empty lists (graceful)
- Corrupted JSON → Logs error, returns empty lists
- No pack set → Save/load operations are no-ops with warnings

## Future Enhancements

### Planned Features:
- Session metadata (game name, notes, session stats)
- Session history/versioning
- Manual save/load UI controls
- Session export/import for sharing
- Multi-user conflict resolution

### Not Implemented:
- File locking (multi-instance protection)
- Session backups/snapshots
- Merge conflict resolution
- Session templates
