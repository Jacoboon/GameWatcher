# Author Studio TODO List
*Generated: 2025-10-13*

## Current Status: Core Detection Working ✅
- OCR fixes loading properly (14 fixes applied)
- Detection loop smooth and efficient
- Cache system performing well (135 hits / 1600 frames in test)
- "Ijhen" → "When" conversion confirmed working

---

## 1. Settings Persistence & Engine Integration

### 1A. Author Settings Not Persisting
**Status**: ❌ Not implemented  
**Description**: Author Studio Settings tab exists but settings are not saved/loaded between sessions.

**Tasks**:
- [ ] Implement save/load for Author Studio specific settings
- [ ] Verify settings file path and format
- [ ] Add auto-save on settings change
- [ ] Test persistence across app restarts

**Files**:
- `GameWatcher.AuthorStudio/ViewModels/SettingsViewModel.cs`
- Settings file location: `C:\Users\{User}\AppData\Roaming\GameWatcher\AuthorStudio\author-settings.json` (?)

---

### 1B. Settings → Engine Integration
**Status**: ⚠️ Needs verification  
**Description**: Verify that settings from both Studio and Author Studio are actually applied to the GameWatcher.Engine services.

**Questions to Answer**:
- Does Studio respect user settings overrides?
- Does Author Studio respect author settings?
- Which settings apply to which app?
- Are OCR settings, detection settings, audio settings all wired up?

**Tasks**:
- [ ] Audit Studio settings → Engine wiring
- [ ] Audit Author Studio settings → Engine wiring  
- [ ] Document settings inheritance/override model
- [ ] Test setting changes actually affect engine behavior

**Files**:
- `GameWatcher.Studio/` - settings integration
- `GameWatcher.AuthorStudio/` - settings integration
- `GameWatcher.Engine/Detection/DetectionLoopConfig.cs`
- `GameWatcher.Engine/Ocr/OcrConfig.cs` (if exists)

---

## 2. Discovery Tab - Activity Log

### 2A. Activity Log Should Mirror Session Logs
**Status**: ❌ Not implemented  
**Description**: The Activity Log in the Discovery tab should show the same information as the session log files for real-time feedback.

**Current Behavior**: Activity log may be incomplete or not showing detection loop events

**Desired Behavior**: 
- Show textbox detection events
- Show OCR processing
- Show dialogue detected
- Show cache hits / frame statistics
- Mirror everything that goes to log file

**Tasks**:
- [ ] Wire logger to duplicate to Activity Log ObservableCollection
- [ ] Filter appropriate log levels (Info, Warning, Error)
- [ ] Add auto-scroll to latest log entry
- [ ] Consider max log buffer size (1000 lines?)

**Files**:
- `GameWatcher.AuthorStudio/ViewModels/DiscoveryViewModel.cs`
- `GameWatcher.AuthorStudio/Services/DiscoveryService.cs`

---

### 2B. Discovery Tab UI Overhaul
**Status**: ✅ COMPLETED (2025-10-13)  
**Description**: ~~Current DataGrid is functional but limited. Replace with List + Details pane for richer information display.~~ **Implemented as Discovery V2 tab!**

**Implementation Strategy**: ✅ Created new "Discovery V2" tab alongside existing Discovery tab. Working and tested - ready to remove old tab once confidence is high.

**What Was Implemented**:
- ✅ List + Details pane design
- ✅ ListView replaces DataGrid
- ✅ Details pane with:
  - ✅ Editable text field (fix OCR errors inline) - **with instant feedback**
  - ✅ OCR debug image viewer placeholder
  - ✅ **Smart OCR Fix Creator** (inline, auto-detects differences, Save/Remove buttons)
  - ✅ Timestamp display
  - ✅ Speaker assignment dropdown
  - ✅ Accept/Delete buttons
- ✅ Selection changed event working
- ✅ Visual indicators for lines needing review (⚠ icon)
- ✅ **Original vs Current text comparison** (readonly, color-coded)
- ✅ **Multiple OCR fixes per line** with Previous/Next navigation
- ✅ **Instant OCR fix detection** as user types
- ✅ **Case-insensitive OCR fix matching**
- ✅ **Re-apply OCR fixes on session load** (fixes "limbo state")

**Known Issues**:
- Accepted list not visible yet (exists in memory, needs UI tab)
- OCR debug image path not yet populated by detection loop

**Proposed Design**:
```
┌─────────────────────────────────────────────────┐
│ [▶ Start] [⏸ Pause] [⏹ Stop]    Status: Running │
│                                   Lines: 5       │
├─────────────────────────────────────────────────┤
│ Discovered Dialogue (List)    │ Details Pane    │
│ ───────────────────────────── │ ──────────────  │
│ ✓ When the time is right...   │ 📝 Corrected:   │
│ ⚠ No one knows where Rstos... │ [edit field]    │
│ ⚠ Weapons and armor made Clf  │ When the time...│
│ ✓ I shall wait patiently...   │                 │
│   I just don't know what...    │ 🖼 OCR Debug:   │
│                                │ [image viewer]  │
│ (⚠ = needs review/OCR fix)    │ Shows what OCR  │
│                                │ actually saw    │
│                                │                 │
│                                │ 🔧 OCR Fix:     │
│                                │ ┌─────────────┐ │
│                                │ │ From: Rstos │ │
│                                │ │ To: Astos   │ │
│                                │ │ [Create]    │ │
│                                │ └─────────────┘ │
│                                │                 │
│                                │ ⏰ Timestamp:   │
│                                │ 23:21:30        │
│                                │                 │
│                                │ 🔊 Speaker:     │
│                                │ [dropdown]      │
│                                │ [✓ Accept]      │
└────────────────────────────────────────────────┘
```

**Tasks**:
- [ ] Design new layout (WPF XAML)
- [ ] Replace DataGrid with ListView/ListBox
- [ ] Add Details pane with:
  - [ ] Editable text field (fix OCR errors before accepting)
  - [ ] OCR debug image viewer
  - [ ] **OCR Fix Creator** (see 2D below)
  - [ ] Timestamp display
  - [ ] Speaker assignment dropdown
  - [ ] Accept/Reject buttons
- [ ] Wire up selection changed event
- [ ] Test layout responsiveness
- [ ] Visual indicators for lines needing review (⚠ icon)

**Benefits**:
- Edit text before accepting (fix OCR errors inline)
- See OCR debug image to understand what was captured
- Create OCR fixes with confirmation (not auto-generated)
- Assign speakers during discovery (not just after)
- Better visual hierarchy

**Files**:
- `GameWatcher.AuthorStudio/Views/DiscoveryView.xaml` (major redesign)
- `GameWatcher.AuthorStudio/ViewModels/DiscoveryViewModel.cs` (add SelectedDialogue property)

---

### 2D. Smart OCR Fix Creation
**Status**: ✅ COMPLETED (2025-10-14) - Fully implemented in Discovery V2!  
**Description**: ~~When user edits text to fix OCR errors, provide intelligent workflow to create reusable OCR fix rules with confirmation.~~ **Completed and optimized!**

**What Was Implemented**:
- ✅ Auto-detects differences between Original OCR and Current text
- ✅ Populates From/To fields automatically as user types
- ✅ **Two detection modes**:
  - Case 1: User makes edits → detects new potential fixes
  - Case 2: OCR fixes already applied → shows which rules were used
- ✅ Always-visible OCR Fix section (not hidden)
- ✅ Case-insensitive matching with case-preserved output
- ✅ **Multi-word pattern support** (e.g., "cast le" → "castle")
- ✅ **Multiple fixes per line** with Previous/Next navigation (◀ ▶)
- ✅ Save button creates new rule → saves to engine-level `ocr_fixes.json`
- ✅ Remove button deletes existing rule
- ✅ **Instant feedback** via TextChanged event (no waiting)
- ✅ **Original vs Corrected comparison view** (readonly reference, color-coded red/green)
- ✅ Rules persist and re-apply on session reload
- ✅ **Engine-level storage** (`%AppData%/GameWatcher/AuthorStudio/ocr_fixes.json`)
- ✅ **Two-pass application** (multi-word patterns first, then single-word tokens)
- ✅ **Visual indicators**: 🔧 wrench icon for OCR-corrected lines
- ✅ **INotifyPropertyChanged** implementation for real-time UI updates
- ✅ **Multi-word detection in existing fixes** (shows all applied rules with pagination)

**Still TODO** (Advanced Features - Future):
- [ ] Duplicate prevention check before creating rule
- [ ] Preview impact: "This would affect X other lines"
- [ ] Batch suggestions: "Also fix 'ijhen' in 3 other lines?"
- [ ] Show existing rules in creation dialog
- [ ] Auto-suggest fixes based on common OCR patterns
- [ ] Confidence score display from Windows OCR
- [ ] Bulk fix review mode
- [ ] Export/import OCR fixes
- [ ] Statistics on most common errors

**Files Modified**:
- `GameWatcher.AuthorStudio/Views/DiscoveryV2View.xaml` - Complete UI with pagination
- `GameWatcher.AuthorStudio/ViewModels/DiscoveryV2ViewModel.cs` - Multi-word detection logic
- `GameWatcher.AuthorStudio/DiscoverySession.cs` - INotifyPropertyChanged implementation
- `GameWatcher.AuthorStudio/Services/OcrFixesStore.cs` - Two-pass Apply() method
- `GameWatcher.AuthorStudio/Converters/NullConverters.cs` - Added BoolToVisibilityConverter

**Current Problem**:
- No easy way to create OCR fixes from discovered errors
- Users have to manually edit `ocr_fixes.json` outside the app
- Can't see what mistakes OCR is making in real-time
- Auto-creating rules blindly could lead to false positives

**Proposed Smart Flow**:

**Option A: Inline in Details Pane** (Integrated with 2B redesign)
```
Details Pane:
┌──────────────────────────────────────┐
│ 📝 OCR Result:                       │
│ ┌──────────────────────────────────┐ │
│ │ No one knows where Rstos, king   │ │
│ │ of the dark has gone.            │ │
│ └──────────────────────────────────┘ │
│                                      │
│ ✏️ Corrected Text:                   │
│ ┌──────────────────────────────────┐ │
│ │ No one knows where Astos, king   │ │ ← User edits here
│ │ of the dark has gone.            │ │
│ └──────────────────────────────────┘ │
│                                      │
│ 🔍 Detected Difference:              │
│ "Rstos" → "Astos"                    │
│ [💾 Create OCR Fix Rule]             │ ← Button appears when text differs
└──────────────────────────────────────┘
```

When user clicks "Create OCR Fix Rule":
```
┌─────────────────────────────────────────┐
│ Create OCR Fix Rule                     │
├─────────────────────────────────────────┤
│ OCR consistently misread this as:       │
│ ┌─────────────┐                         │
│ │ Rstos       │ (From - what OCR saw)   │
│ └─────────────┘                         │
│                                         │
│ Correct it to:                          │
│ ┌─────────────┐                         │
│ │ Astos       │ (To - correct text)     │
│ └─────────────┘                         │
│                                         │
│ ⚙️ Options:                             │
│ ☑ Case insensitive                     │
│ ☐ Whole word only                      │
│ ☐ Apply to all similar (Rstos's, etc)  │
│                                         │
│ 📋 Existing Rules (2):                  │
│ • ijhen → When                          │
│ • ljhen → When                          │
│                                         │
│ ⚠️ This will add to:                    │
│ Configuration/ocr_fixes.json            │
│                                         │
│ [Cancel] [Create & Apply]               │
└─────────────────────────────────────────┘
```

**Option B: Context Menu** (Simpler, less integrated)
- User edits text in details pane
- Right-click corrected word → "Create OCR Fix for 'Rstos'"
- Shows similar dialog to confirm

**Smart Features**:
1. **Difference Detection**: Automatically detect what changed between OCR and corrected text
2. **Word Isolation**: Identify specific misread words vs full-text replacement
3. **Duplicate Prevention**: Check if rule already exists before offering to create
4. **Preview Impact**: Show how many other discovered lines would be affected
5. **Batch Suggestions**: "OCR also misread 'ijhen' in 3 other lines - create fix?"
6. **Rule Review**: Show existing rules in the dialog for context

**Workflow**:
1. User discovers dialogue: "No one knows where Rstos..."
2. Sees OCR debug image, realizes "Rstos" should be "Astos"
3. Edits text in corrected field: "...where Astos..."
4. System detects difference: `Rstos` → `Astos`
5. "Create OCR Fix" button appears (or auto-prompt)
6. Dialog shows proposed rule with options
7. User confirms, rule added to `ocr_fixes.json`
8. Rule immediately applied to all discovered lines
9. Visual feedback: "✓ OCR fix created - 0 other lines updated"

**Advanced Features** (Phase 2):
- [ ] Auto-suggest fixes based on common OCR patterns (l→I, 0→O, rn→m)
- [ ] Show confidence score from Windows OCR
- [ ] Bulk fix review: Show all potential fixes at once
- [ ] Export/import OCR fixes for sharing between users
- [ ] Statistics: Most common OCR errors for this game

**Tasks**:
- [ ] Add text comparison logic to detect differences
- [ ] Build OCR fix creation dialog
- [ ] Wire dialog to OcrFixesStore service
- [ ] Implement real-time rule application (re-process discovered lines)
- [ ] Add duplicate rule detection
- [ ] Show visual feedback when rule is created
- [ ] Test with various OCR error patterns

**Files**:
- `GameWatcher.AuthorStudio/Views/DiscoveryView.xaml` (details pane + dialog)
- `GameWatcher.AuthorStudio/ViewModels/DiscoveryViewModel.cs` (comparison logic)
- `GameWatcher.Engine/Ocr/OcrFixesStore.cs` (add rule at runtime)
- `GameWatcher.AuthorStudio/Models/PendingDialogueEntry.cs` (track original vs corrected)

**Priority**: HIGH - This is a core authoring workflow feature

---

### 2E. Log OCR Fix Applications
**Status**: ❌ Not implemented (Moved to active TODO list)  
**Description**: Add logging when OCR fixes are applied so users can see the corrections happening in real-time.

**Current Behavior**: OCR fixes silently applied, no visibility into what's being corrected (but visual indicators work - 🔧 wrench icon shows corrected lines)

**Desired Behavior**:
```
[INF] OCR detected: "I am a sage. Ijhen the time is right..."
[INF] 🔧 Applied OCR fix: "Ijhen" → "When"
[INF] 🔧 Applied OCR fix: "cast le" → "castle"
[INF] Corrected text: "I am a sage. When the time is right..."
```

**Benefits**:
- User sees fixes working in real-time in Activity Log
- Debugging OCR fix rules
- Confidence that rules are being applied correctly
- Shows which rules are most frequently used
- Can identify if wrong rules are being applied

**Implementation Details**:
- Log at Info level (visible but not spammy)
- Include: original text snippet, fix applied (from → to), corrected result
- Consider: Count how many times each rule is used (statistics)
- UI: Show in Activity Log (feed to DiscoveryService.LogLines)

**Tasks**:
- [ ] Add logging to `OcrFixesStore.Apply()` method
- [ ] Log each fix application with before/after
- [ ] Consider adding usage statistics counter
- [ ] Wire logs to Activity Log ObservableCollection

**Files**:
- `GameWatcher.AuthorStudio/Services/OcrFixesStore.cs` (add logging in Apply method)
- `GameWatcher.AuthorStudio/Services/DiscoveryService.cs` (receive logs)

**Priority**: MEDIUM - Nice debugging/visibility feature

---

### 2C. Live Metrics Display
**Status**: ❌ Not implemented  
**Description**: Show real-time performance metrics during discovery session.

**Metrics to Display**:
- Current FPS (actual vs target)
- Frame processing time (avg/min/max)
- Cache hit rate (%)
- Dialogues detected count
- Session duration
- Maybe: Frame number, textbox status

**Proposed Location**: Top of Discovery tab as status bar or info panel

**Tasks**:
- [ ] Add metrics properties to DiscoveryViewModel
- [ ] Wire DetectionLoop statistics to ViewModel (expose via DiscoveryService?)
- [ ] Update UI with periodic refresh (1 second interval?)
- [ ] Design metrics display layout

**Files**:
- `GameWatcher.AuthorStudio/ViewModels/DiscoveryViewModel.cs`
- `GameWatcher.AuthorStudio/Services/DiscoveryService.cs`
- `GameWatcher.AuthorStudio/Views/DiscoveryView.xaml`

---

### 2F. Accepted Dialogue Tab
**Status**: ❌ Not implemented - **USER BLOCKED** (2025-10-13)  
**Description**: When user clicks "Accept" in Discovery V2, line moves to `AcceptedDialogue` collection but there's no UI to view it.

**Current Behavior**:
- Accept button works, line disappears from Discovered list
- Line saved to `session.json` in `accepted` array
- No way to see/edit accepted lines in UI

**Desired Behavior**:
- Add "Accepted" tab in DiscoveryV2View
- Show AcceptedDialogue collection in ListView
- Allow editing speaker, text, instructions
- Allow un-accepting (move back to Discovered)
- Show metadata: timestamp, speaker, audio status

**Proposed UI** (new tab in Discovery V2):
```
[Discovered (3)] [Accepted (5)] ← Tabs
┌─────────────────────────────────────────────────┐
│ Accepted Dialogue List     │ Details Pane       │
│ ─────────────────────────  │ ──────────────     │
│ ✓ When the time is right.. │ Same details as    │
│ ✓ I shall wait patiently.. │ Discovered tab but │
│ ✓ Weapons and armor made.. │ with "Unaccept"    │
│                            │ button instead     │
└────────────────────────────────────────────────┘
```

**Tasks**:
- [ ] Add TabControl to DiscoveryV2View.xaml
- [ ] Create Discovered tab with current content
- [ ] Create Accepted tab with AcceptedDialogue ListView
- [ ] Share same Details pane for both tabs
- [ ] Add "Unaccept" button for accepted lines
- [ ] Test tab switching and selection

**Files**:
- `GameWatcher.AuthorStudio/Views/DiscoveryV2View.xaml` - Add TabControl
- `GameWatcher.AuthorStudio/ViewModels/DiscoveryV2ViewModel.cs` - Add UnacceptCommand

**Priority**: HIGH - User is blocked from seeing accepted lines

---

## 3. Speakers Tab Overhaul

### 3A. Replace DataGrid with List + Details Pane
**Status**: ❌ Design needed  
**Description**: Similar to Discovery tab redesign, Speakers tab needs richer detail view.

**Proposed Design**:
```
┌─────────────────────────────────────────────────┐
│ Speakers List              │ Speaker Details    │
│ ─────────────────────────  │ ─────────────────  │
│ 🎭 Sage of Elfheim         │ Name: Sage of...   │
│    Elfheim Villager 1      │ [edit]             │
│    Generic NPC             │                    │
│    King                    │ 🎙️ Voice:          │
│                            │ [Dropdown]         │
│                            │ [🔊 Preview]       │
│                            │                    │
│                            │ ⚡ Speed: 1.0      │
│                            │ [Slider] [Edit]    │
│                            │                    │
│                            │ 📝 Instructions:   │
│                            │ [Text editor]      │
│                            │ Speak wisely...    │
│                            │                    │
│                            │ 💬 Lines (5):      │
│                            │ • When the time... │
│                            │ • I shall wait...  │
└────────────────────────────────────────────────┘
```

**Tasks**:
- [ ] Design new layout
- [ ] Replace DataGrid with ListView
- [ ] Add Details pane with all speaker properties
- [ ] Show assigned dialogue lines for speaker
- [ ] Wire up selection

**Files**:
- `GameWatcher.AuthorStudio/Views/SpeakersView.xaml`
- `GameWatcher.AuthorStudio/ViewModels/SpeakersViewModel.cs`

---

### 3B. Voice Speed Slider Precision
**Status**: ❌ Bug - too chunky  
**Description**: Speed slider currently moves in 0.25 increments, need 0.10 increments.

**Current**: 0.25, 0.50, 0.75, 1.00, 1.25...  
**Desired**: 0.10, 0.20, 0.30, ... 0.90, 1.00, 1.10...

**Tasks**:
- [ ] Change slider TickFrequency to 0.10
- [ ] Add numeric TextBox for manual entry
- [ ] Bind TextBox to same property as Slider
- [ ] Validate input range (0.25 - 4.0?)

**Files**:
- `GameWatcher.AuthorStudio/Views/SpeakersView.xaml`

---

### 3C. Voice Preview Cache System Broken
**Status**: ❌ Bug - not using cache  
**Description**: Voice preview is generating new TTS using NPC names instead of using cached previews based on voice+speed.

**Current Broken Behavior**:
- Preview uses NPC name → generates `Sage_of_Elfheim_preview.mp3`
- Each NPC with same voice creates duplicate preview files
- Wastes API calls and disk space

**Correct Behavior**:
- Preview uses voice+speed hash → generates `alloy_1.0_preview.mp3`
- All NPCs with same voice+speed share one cached preview
- Cache lives in `GameWatcher.Engine/Voices/` or `voices/previews/`

**Existing Cache System**:
- `GameWatcher.Engine/Voices/` contains voice preview infrastructure
- Cache key should be: `{voiceId}_{speed}` 
- Standard preview text: "This is a preview of the selected voice."

**Tasks**:
- [ ] Find where preview generation is called
- [ ] Fix to use voice+speed instead of NPC name
- [ ] Verify cache lookup before generation
- [ ] Test that multiple NPCs share same preview

**Files**:
- `GameWatcher.AuthorStudio/ViewModels/SpeakersViewModel.cs` (preview command)
- `GameWatcher.Engine/Voices/` (cache system)

---

### 3D. TTS Instructions per Line
**Status**: ❌ Not implemented  
**Description**: Now that 4o-mini supports 'instructions', we can add per-line tonal guidance.

**Feature**: 
- After selecting voice and previewing, add optional instructions field
- Instructions customize how the line is spoken (tone, emotion, pacing)
- Example: "Speak slowly and mysteriously" or "Urgent and worried"
- Instructions locked in when line is accepted/generated

**Workflow**:
1. Discover dialogue line
2. Assign speaker (with base voice + speed)
3. **NEW**: Edit instructions for this specific line
4. Generate audio with instructions
5. (Future) Send to Voice Lab for refinement

**UI Addition to Details Pane**:
```
📝 TTS Instructions (Optional):
┌────────────────────────────────────┐
│ Speak slowly and mysteriously,    │
│ emphasizing the word "time"       │
└────────────────────────────────────┘
[Generate with Instructions]
```

**Tasks**:
- [ ] Add Instructions property to dialogue entry model
- [ ] Add Instructions text editor to Discovery/Speakers details pane
- [ ] Wire instructions through to TTS generation
- [ ] Test with 4o-mini TTS
- [ ] Save instructions to pack metadata

**Files**:
- `GameWatcher.AuthorStudio/Models/PendingDialogueEntry.cs`
- `GameWatcher.AuthorStudio/ViewModels/DiscoveryViewModel.cs`
- `GameWatcher.AuthorStudio/ViewModels/SpeakersViewModel.cs`
- TTS generation service (wherever OpenAI call happens)

---

## 4. Voice Lab

### 4A. Complete Voice Lab Implementation
**Status**: ⚠️ Mid-implementation  
**Description**: Voice Lab tab exists but not fully functional. Needs to accept voice lines and provide fine-tuning workflow.

**Missing Pieces**:
- [ ] Accept dialogue line from Discovery/Speakers tab
- [ ] Load existing audio for line
- [ ] Audio playback controls
- [ ] Effect/filter application
- [ ] Instructions editor (similar to speakers tab)
- [ ] Regenerate with new instructions
- [ ] Save modified audio back to pack
- [ ] Waveform visualization (stretch goal?)

**Workflow**:
1. Right-click dialogue in Discovery/Speakers → "Send to Voice Lab"
2. Voice Lab loads line, shows current audio if exists
3. Edit instructions, apply effects, regenerate
4. Preview changes
5. Save when satisfied

**Tasks**:
- [ ] Design "Send to Voice Lab" context menu
- [ ] Implement line transfer to Voice Lab
- [ ] Build audio player controls
- [ ] Wire up TTS regeneration
- [ ] Implement save back to pack

**Files**:
- `GameWatcher.AuthorStudio/ViewModels/VoiceLabViewModel.cs`
- `GameWatcher.AuthorStudio/Views/VoiceLabView.xaml`
- Context menu in Discovery/Speakers views

---

## Priority Ranking

### ✅ Recently Completed (2025-10-14)
1. **Discovery UI Redesign (2B)** - ✅ Discovery V2 working!
2. **Smart OCR Fix Creation (2D)** - ✅ Complete with multi-word support, pagination, visual indicators!
3. **Engine-Level OCR Fixes** - ✅ Refactored to %AppData% storage, applies to all packs globally
4. **Multi-Word Pattern Support** - ✅ Two-pass Apply() handles "cast le" → "castle" patterns
5. **Real-Time UI Updates** - ✅ INotifyPropertyChanged implementation for live comparison boxes
6. **Visual Indicators** - ✅ 🔧 wrench icon shows OCR-corrected lines in both lists
7. **Case-Insensitive Matching** - ✅ Preserved case in keys, case-insensitive lookups

### 🔥 High Priority (Blocking User)
1. **Accepted Dialogue Tab (2F)** - User can't see accepted lines
2. **OCR Fix Logging (2E)** - Need visibility into what's being corrected

### High Priority (Core Functionality)
3. **Settings Persistence (1A)** - Settings should save
4. **Voice Preview Cache Fix (3C)** - Wasting API calls
5. **Speed Slider Fix (3B)** - Quick UX fix

### Medium Priority (Major Features)
6. **TTS Instructions (3D)** - Unique feature, good value
7. **Settings → Engine Integration (1B)** - Need to verify it works
8. **Activity Log Mirror (2A)** - Real-time feedback

### Lower Priority (Nice to Have)
9. **Live Metrics (2C)** - Useful but not critical
10. **Speakers UI Redesign (3A)** - Similar to Discovery redesign
11. **Voice Lab Completion (4A)** - Advanced feature

---

## Notes

- OCR detection is working great now (WindowsOcrEngine fixed, OCR fixes applied)
- Shutdown metrics added to DetectionLoop but didn't appear in test - investigate
- Session persistence working well
- Cache system performing excellently (135 hits / 1600 frames)

---

## Future Considerations (Beyond This List)

- Export pack functionality
- Import/merge dialogue from other sessions
- Bulk operations (assign all lines to speaker, bulk generate audio)
- Pack validation (check for missing audio, speakers, etc.)
- Analytics (most common speakers, dialogue length distribution)
- Multi-game pack support
