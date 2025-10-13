# V2 Detection Loop - GameWatcher Platform Architecture

> **Source**: `GameWatcher-Platform\GameWatcher.AuthorStudio\Services\DiscoveryService.cs`  
> **Version**: GameWatcher Platform V2 (Current Production)  
> **Purpose**: Document the complete modular detection flow with DI, textbox-focused comparison, and pack-based configuration

---

## 🎯 High-Level Overview

The V2 detection loop is a **textbox-focused modular detection system** that builds on V1's proven algorithms while adding dependency injection, game pack abstraction, and improved multi-line dialogue detection. It maintains 15 FPS capture while processing only the textbox area for better accuracy and follow-up dialogue support.

### Core Philosophy
- **Textbox-Focused**: Compare only textbox content, not full frames (ignores background/UI changes)
- **Modular Architecture**: DI-based with ITextboxDetector, IOcrEngine, logging interfaces
- **Pack-Based Configuration**: Game-specific settings via TextboxDetectionConfig
- **Follow-up Dialogue Support**: 99% similarity threshold when busy to catch text changes
- **Async Processing**: OCR runs in background tasks to maintain 15 FPS

### V2 vs V1 Key Differences
| Aspect | V1 | V2 |
|--------|----|----|
| **Frame Comparison** | Full frame similarity | **Textbox area only** |
| **Architecture** | Monolithic Program.cs | **Modular DI services** |
| **Configuration** | Hardcoded | **Pack-based TextboxDetectionConfig** |
| **State Management** | 3-state (_waiting, _processing) | **2-state (_isBusy)** |
| **Multi-line Detection** | Basic | **Enhanced (99% threshold)** |
| **OCR Fixes** | Hardcoded CleanOCRText() | **JSON-based OcrFixesStore** |
| **Logging** | Console.WriteLine | **ILogger<T> with structured logging** |

---

## 📊 Detection Loop Flow Chart

```
┌─────────────────────────────────────────────────────────────────┐
│                    MAIN LOOP (15 FPS Timer)                     │
│                    Every ~67ms (15 times/sec)                    │
│                    Timer callback: CaptureTick()                 │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 1: CAPTURE FRAME                                           │
│ ────────────────────────────────────────────────────────────────│
│ var currentFrame = ScreenCapture.CaptureGameWindow()            │
│                                                                  │
│ • Same as V1: Win32 FindWindow + BitBlt                         │
│ • Searches for: "FINAL FANTASY", "FF1", emulator windows        │
│ • Falls back to desktop capture if not found                    │
│ • Returns: Bitmap (full game frame)                             │
│                                                                  │
│ Periodic logging (every 100 frames):                            │
│ • Log frame count and dimensions                                │
│ • Structured logging: _logger.LogDebug("[Activity] ...")       │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 2: TEXTBOX-FOCUSED COMPARISON (Lock Block - Thread Safe)   │
│ ────────────────────────────────────────────────────────────────│
│ lock (_lockObject):                                             │
│                                                                  │
│ 2a. Detect Current Textbox Area                                 │
│ ────────────────────────────────────                            │
│ var currentTextboxRect = _detector.DetectTextbox(currentFrame)  │
│                                                                  │
│ IF currentTextboxRect == null:                                  │
│   ┌──────────────────────────────────────────────────────────┐ │
│   │ No textbox found in frame                                │ │
│   │                                                           │ │
│   │ IF _isBusy:                                              │ │
│   │   Log "No textbox detected - resetting"                 │ │
│   │                                                           │ │
│   │ • _isBusy = false         (reset state)                 │ │
│   │ • _lastTextboxHash = ""   (clear hash)                  │ │
│   │ • _lastFrame = Clone(currentFrame)  (update)            │ │
│   │ • Dispose currentFrame                                   │ │
│   │ • return                                                 │ │
│   └──────────────────────────────────────────────────────────┘ │
│                                                                  │
│ 2b. Crop to Textbox Area ONLY                                   │
│ ────────────────────────────────────                            │
│ using var currentTextbox = CropImage(currentFrame,              │
│                                       currentTextboxRect.Value)  │
│                                                                  │
│ • V2 IMPROVEMENT: Only compare textbox pixels                   │
│ • Ignores background, menus, UI animations                      │
│ • Result: Cropped Bitmap of just dialogue box                   │
│                                                                  │
│ 2c. Compare Textbox Content (Not Full Frame!)                   │
│ ─────────────────────────────────────────────                   │
│ bool textboxMatches = false                                     │
│                                                                  │
│ IF _lastFrame != null:                                          │
│   var lastTextboxRect = _detector.DetectTextbox(_lastFrame)     │
│                                                                  │
│   IF lastTextboxRect.HasValue:                                  │
│     using var lastTextbox = CropImage(_lastFrame,               │
│                                        lastTextboxRect.Value)    │
│                                                                  │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ Dynamic Threshold Based on State                       │ │
│     │ ────────────────────────────────────────────────────── │ │
│     │ IF !_isBusy:                                           │ │
│     │   // Not processing = detect NEW stable dialogue       │ │
│     │   textboxMatches = AreImagesSimilar(                   │ │
│     │                      lastTextbox, currentTextbox, 500) │ │
│     │   • Sample rate 500 = check every 500th pixel          │ │
│     │   • 5% difference threshold                            │ │
│     │                                                         │ │
│     │ ELSE (_isBusy == true):                                │ │
│     │   // Processing = catch FOLLOW-UP dialogue changes     │ │
│     │   textboxMatches = AreImagesSimilar(                   │ │
│     │                      lastTextbox, currentTextbox, 50)  │ │
│     │   • Sample rate 50 = check every 50th pixel            │ │
│     │   • 99% similarity = detect subtle text changes        │ │
│     └────────────────────────────────────────────────────────┘ │
│                                                                  │
│ 2d. State Machine Transitions                                   │
│ ──────────────────────────────                                  │
│ IF textboxMatches && !_isBusy:                                  │
│   ┌──────────────────────────────────────────────────────────┐ │
│   │ TEXTBOX STABLE + NOT BUSY = New Dialogue Detected        │ │
│   │                                                           │ │
│   │ • _isBusy = true                                         │ │
│   │ • Log "Textbox stable at X,Y - processing..."           │ │
│   │ • Continue to STEP 3 (OCR processing below)             │ │
│   └──────────────────────────────────────────────────────────┘ │
│                                                                  │
│ ELSE IF textboxMatches && _isBusy:                              │
│   ┌──────────────────────────────────────────────────────────┐ │
│   │ TEXTBOX UNCHANGED + BUSY = Same Dialogue (Skip)          │ │
│   │                                                           │ │
│   │ • Log "Same textbox content - skipping"                 │ │
│   │ • Dispose currentFrame                                   │ │
│   │ • return (skip OCR, already processed)                   │ │
│   └──────────────────────────────────────────────────────────┘ │
│                                                                  │
│ ELSE (!textboxMatches):                                         │
│   ┌──────────────────────────────────────────────────────────┐ │
│   │ TEXTBOX CHANGED = Reset for New Detection                │ │
│   │                                                           │ │
│   │ IF _isBusy:                                              │ │
│   │   Log "Textbox content changed - ready for new dialogue" │ │
│   │                                                           │ │
│   │ • _isBusy = false                                        │ │
│   │ • _lastTextboxHash = ""                                  │ │
│   │ • _lastFrame = Clone(currentFrame)                       │ │
│   │ • Dispose currentFrame                                   │ │
│   │ • return (wait for next stable frame)                    │ │
│   └──────────────────────────────────────────────────────────┘ │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      │ (Only reached if textbox stable && !_isBusy)
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 3: RE-DETECT TEXTBOX (Verification)                        │
│ ────────────────────────────────────────────────────────────────│
│ var textboxRect = _detector.DetectTextbox(currentFrame)         │
│                                                                  │
│ IF !textboxRect.HasValue:                                       │
│   • Dispose currentFrame                                        │
│   • return (textbox disappeared)                                │
│                                                                  │
│ Log textbox coordinates and dimensions                          │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 4: CROP TEXTBOX & HASH CHECK                               │
│ ────────────────────────────────────────────────────────────────│
│ using var textboxImage = CropImage(currentFrame, textboxRect)   │
│ var textboxHash = GetImageHash(textboxImage)                    │
│                                                                  │
│ GetImageHash() - Content-Focused Hashing:                       │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ • Focus on TEXT AREA (center 60×60% region)                │ │
│ │ • Sample 7 strategic points across text content:           │ │
│ │   - Upper left, Upper right                                │ │
│ │   - Lower left, Lower right                                │ │
│ │   - Center, Left center, Right center                      │ │
│ │ • Combine pixel RGB values into hash                       │ │
│ │ • Result: Content-sensitive hash ignoring borders          │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ IF textboxHash == _lastTextboxHash:                             │
│   • Log "Textbox content unchanged - skipping OCR"             │
│   • Dispose currentFrame                                        │
│   • return (same content, no need for OCR)                      │
│                                                                  │
│ • _lastTextboxHash = textboxHash (update)                       │
│ • Log "New textbox content detected - running OCR..."          │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 5: ASYNC OCR PROCESSING (Non-Blocking)                     │
│ ────────────────────────────────────────────────────────────────│
│ var textboxCopy = new Bitmap(textboxImage)                      │
│ currentFrame.Dispose()                                          │
│                                                                  │
│ _ = Task.Run(async () => {                                      │
│   try {                                                          │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 5a. OCR Extraction (WindowsOCR)                        │ │
│     │ ────────────────────────────────────────────────────── │ │
│     │ var text = _ocr.ExtractTextFast(textboxCopy)?.Trim()  │ │
│     │                                                         │ │
│     │ WindowsOCR.ExtractTextFast():                          │ │
│     │ • Uses Windows.Media.Ocr.OcrEngine                     │ │
│     │ • English language recognition                         │ │
│     │ • No preprocessing (textbox already cropped)           │ │
│     │ • Returns: raw OCR text string                         │ │
│     │                                                         │ │
│     │ IF string.IsNullOrWhiteSpace(text):                    │ │
│     │   Log "OCR returned empty text"                        │ │
│     │   return (skip empty results)                          │ │
│     └────────────────────────────────────────────────────────┘ │
│                                                                  │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 5b. OCR Fixes (Pack-Specific JSON Rules)              │ │
│     │ ────────────────────────────────────────────────────── │ │
│     │ var originalOcrText = text  (preserve for UI)          │ │
│     │ text = _fixes.Apply(text)                              │ │
│     │                                                         │ │
│     │ OcrFixesStore.Apply():                                 │ │
│     │ • Load from: PackFolder/Configuration/ocr_fixes.json   │ │
│     │ • JSON format: {"fixes": [{"from": "", "to": ""}]}    │ │
│     │ • Case-insensitive matching                            │ │
│     │ • Apply all fixes in order                             │ │
│     │                                                         │ │
│     │ Example fixes (FF1):                                   │ │
│     │   "Il " → "I "                                         │ │
│     │   "15" → "is"                                          │ │
│     │   "mow" → "now"                                        │ │
│     │   "KIM" → "King"                                       │ │
│     │                                                         │ │
│     │ IF text != originalOcrText:                            │ │
│     │   Log "OCR fixes applied: 'before' -> 'after'"        │ │
│     └────────────────────────────────────────────────────────┘ │
│                                                                  │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 5c. Text Normalization                                 │ │
│     │ ────────────────────────────────────────────────────── │ │
│     │ var norm = TextNormalizer.Normalize(text)              │ │
│     │                                                         │ │
│     │ TextNormalizer.Normalize():                            │ │
│     │ • Replace smart quotes: ' " " → ' " "                  │ │
│     │ • Replace ellipsis: … → ...                            │ │
│     │ • Collapse whitespace: multiple spaces → single space  │ │
│     │ • ToLowerInvariant() for matching                      │ │
│     │ • Trim()                                               │ │
│     │                                                         │ │
│     │ Result: Normalized key for deduplication               │ │
│     └────────────────────────────────────────────────────────┘ │
│                                                                  │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 5d. Duplicate Detection                                │ │
│     │ ────────────────────────────────────────────────────── │ │
│     │ lock (_lockObject):                                    │ │
│     │   IF norm == _lastNormalized:                          │ │
│     │     Log "Duplicate (normalized match)"                 │ │
│     │     return (skip, same as last)                        │ │
│     │                                                         │ │
│     │   IF _seen.Contains(norm):                             │ │
│     │     Log "Duplicate (seen in session)"                  │ │
│     │     return (skip, already captured this session)       │ │
│     │                                                         │ │
│     │   • _lastText = text                                   │ │
│     │   • _lastNormalized = norm                             │ │
│     │   • _seen.Add(norm)                                    │ │
│     └────────────────────────────────────────────────────────┘ │
│                                                                  │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 5e. Create Dialogue Entry & UI Update                 │ │
│     │ ────────────────────────────────────────────────────── │ │
│     │ await App.Current.Dispatcher.InvokeAsync(() =>         │ │
│     │ {                                                       │ │
│     │   var entry = new PendingDialogueEntry                 │ │
│     │   {                                                     │ │
│     │     Text = text,                   (cleaned)           │ │
│     │     OriginalOcrText = originalOcrText,  (raw)          │ │
│     │     Timestamp = DateTime.UtcNow,                       │ │
│     │     Approved = false                                   │ │
│     │   };                                                    │ │
│     │                                                         │ │
│     │   Discovered.Add(entry);  (ObservableCollection)       │ │
│     │   Log "✅ Found: {text}"                               │ │
│     │   _logger.LogInformation("[Activity] Found unique...")│ │
│     │                                                         │ │
│     │   TryPlayExistingAudio(entry);                         │ │
│     │   // Lookup in AudioStore, play if exists             │ │
│     │ });                                                     │ │
│     └────────────────────────────────────────────────────────┘ │
│   }                                                              │
│   catch (Exception ex) {                                        │
│     Log "OCR error: {ex.Message}"                              │
│     _logger.LogError(ex, "OCR processing error")               │
│   }                                                              │
│   finally {                                                     │
│     textboxCopy?.Dispose()                                      │
│   }                                                              │
│ });                                                             │
│                                                                  │
│ • Main loop continues immediately (non-blocking)                │
│ • OCR runs in background (50-200ms typical)                     │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    LOOP RETURNS TO STEP 1                       │
│              Waits ~67ms, then captures next frame              │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔧 Core Components

### 1. **DiscoveryService (Main Orchestrator)**
**Purpose**: WPF service managing capture loop lifecycle  
**Architecture**: Dependency injection with ITextboxDetector, IOcrEngine, ILogger

```csharp
Constructor Dependencies:
  OcrFixesStore fixes          // Pack-specific OCR corrections
  ILogger<DiscoveryService>    // Structured logging
  AudioPlaybackService         // Audio preview during discovery
  AudioStore                   // Lookup existing audio files
  ILoggerFactory? loggerFactory  // For creating detector logger
  ITextboxDetector? detector   // Optional DI, defaults to FF1
```

**Lifecycle Methods**:
- `StartAsync()` - Start 15 FPS timer (67ms interval)
- `PauseAsync()` - Pause capture without clearing state
- `StopAsync()` - Stop and clear transient state
- `LoadOcrFixesAsync(packFolder)` - Load pack configuration

**Observable Collections** (WPF Data Binding):
- `Discovered` - ObservableCollection<PendingDialogueEntry>
- `LogLines` - ObservableCollection<string> (UI activity log)

---

### 2. **DynamicTextboxDetector (Engine.Detection)**
**Purpose**: Locate dialogue boxes using configurable pack settings  
**Configuration**: `TextboxDetectionConfig` from game pack

**Two-Strategy Detection** (Same as V1):

#### **Strategy 1: Cached Position Search**
```csharp
IF _lastKnownTextbox exists:
  expandedArea = Expand(_lastKnownTextbox, 50px)
  SearchForTextboxInRegion(screenshot, expandedArea)
  
  IF found:
    Update cache, return rectangle
  ELSE:
    _consecutiveFailures++
    IF failures > 3: Clear cache
```

#### **Strategy 2: Targeted Area Search**
```csharp
// V2 RESTORED V1 HARDCODED COORDINATES
// (Config system temporarily reverted after detection issues)

Calculate search area:
  X = (width × 0.196875) - 25px
  Y = (height × 0.050926) - 25px
  Width = (width × 0.604688) + 50px
  Height = (height × 0.282407) + 50px

Result: 79.3% search area reduction

Sample for blue border pixels (every 10px)
Trace rectangles from border pixels
Validate with IsValidDialogueBoxSize()
Validate with HasValidBorder() (20 samples per edge)
Return largest valid rectangle
```

**Configuration Schema** (TextboxDetectionConfig):
```csharp
class TextboxDetectionConfig {
  List<Color> BorderColors           // Blue variants to detect
  int ColorTolerance = 80             // ±80 RGB per channel
  Size MinSize = (200, 100)           // Minimum textbox size
  Size MaxSize = (1920, 800)          // Maximum textbox size
  bool RequireLandscapeAspect = true  // Width > Height
  int CachedPositionExpansion = 50    // Cache buffer px
  int MaxConsecutiveFailures = 3      // Clear cache threshold
}
```

**FF1 Border Colors**:
- RGB(66, 66, 231) - Dark blue
- RGB(99, 99, 255) - Medium blue
- RGB(33, 33, 165) - Very dark blue
- RGB(0, 88, 248) - Bright blue
- RGB(82, 82, 247) - Another variant
- RGB(0, 0, 255) - Pure blue
- RGB(0, 100, 200) - Cyan-blue
- RGB(50, 50, 200) - Dark blue-purple

---

### 3. **ScreenCapture (Runtime.Services.Capture)**
**Purpose**: Frame capture and similarity detection (Identical to V1)

```csharp
CaptureGameWindow():
  FindWindow("FINAL FANTASY", "FF1", emulators...)
  IF found: BitBlt window
  ELSE: BitBlt desktop

AreImagesSimilar(bmp1, bmp2, sampleRate):
  Sample every Nth pixel
  Tolerance: ±10 RGB
  Threshold: 5% difference = different frames
  
  sampleRate = 500: Check every 500th pixel (0.2% sample)
  sampleRate = 50:  Check every 50th pixel (2% sample)
```

**V2 Usage Pattern**:
- **Not Busy** (`sampleRate = 500`): Coarse check for new stable dialogue
- **Busy** (`sampleRate = 50`): Fine check for follow-up dialogue text changes

---

### 4. **WindowsOCR (Runtime.Services.OCR)**
**Purpose**: Modern Windows OCR engine wrapper

```csharp
ExtractTextFast(Bitmap image):
  // Uses Windows.Media.Ocr.OcrEngine
  // English language model
  // No preprocessing needed (V1 enhancement removed)
  
  Convert Bitmap → SoftwareBitmap
  OcrEngine.RecognizeAsync()
  Return text lines concatenated
```

**V1 vs V2 OCR**:
| Feature | V1 (Tesseract) | V2 (Windows OCR) |
|---------|----------------|------------------|
| Engine | Tesseract 4.x | Windows.Media.Ocr |
| Preprocessing | 4× scale, grayscale, threshold | None (native) |
| Speed | 50-200ms | 30-100ms |
| Accuracy | Good (with enhancement) | Excellent (native) |
| Dependencies | Tesseract.dll + data | Built-in Windows |

---

### 5. **OcrFixesStore (AuthorStudio.Services)**
**Purpose**: Pack-specific JSON-based OCR corrections

```csharp
Load from: PackFolder/Configuration/ocr_fixes.json

JSON Format:
{
  "fixes": [
    {"from": "Il ", "to": "I "},
    {"from": "15", "to": "is"},
    {"from": "mow", "to": "now"},
    {"from": "KIM", "to": "King"}
  ]
}

Apply():
  FOR each fix:
    IF text.Contains(fix.from, ignoreCase):
      text = text.Replace(fix.from, fix.to)
  return text
```

**Editor Integration**:
- AuthorStudio includes visual OCR fixes editor
- Add/remove/edit fixes per pack
- Saves to ocr_fixes.json immediately
- Changes apply to next detected dialogue

---

### 6. **TextNormalizer (AuthorStudio.Services)**
**Purpose**: Normalize text for deduplication matching

```csharp
Normalize(string input):
  1. Replace smart quotes: ' " " → ' " "
  2. Replace ellipsis: … → ...
  3. Collapse whitespace: \s+ → single space
  4. ToLowerInvariant()
  5. Trim()
  
  Return: Normalized key for _seen HashSet
```

**Example**:
```
Input:  "The King's power… is fading!"
Output: "the king's power... is fading!"
```

---

### 7. **AudioStore & Playback Integration**
**Purpose**: Lookup and play existing audio during discovery

```csharp
TryPlayExistingAudio(PendingDialogueEntry entry):
  audioEntry = _audioStore.GetAudioEntry(entry.Text)
  
  IF audioEntry exists:
    fullPath = PackFolder + audioEntry.Path
    IF File.Exists(fullPath):
      _audioPlayer.Play(fullPath)
      entry.AudioPath = audioEntry.Path
      entry.TtsVoice = audioEntry.VoiceName
      Log "Playing audio: {filename}"
```

**Benefit**: Hear existing voiceovers as dialogue is discovered

---

## 🎯 State Machine (2-State vs V1's 3-State)

### V2 State Machine

**State Variable**:
```csharp
bool _isBusy = false  // Currently processing dialogue
```

**State Transitions**:
```
[INITIAL STATE]
  _isBusy = false
  _lastFrame = null
  _lastTextboxHash = ""

↓ Capture frame, no textbox found
  _isBusy = false (stay idle)
  _lastFrame = currentFrame (update)
  return

↓ Textbox detected, stable, not busy
  _isBusy = true (start processing)
  Continue to OCR

↓ Textbox stable, already busy
  _isBusy = true (stay busy)
  return (skip, same dialogue)

↓ Textbox changed (content different)
  _isBusy = false (reset to idle)
  _lastFrame = currentFrame (update)
  _lastTextboxHash = "" (clear)
  return (wait for next stable textbox)

↓ OCR completes (async)
  No state change (OCR is background)
  Entry added to Discovered collection
```

### V1 vs V2 State Comparison

| Aspect | V1 (3-State) | V2 (2-State) |
|--------|--------------|--------------|
| **States** | `_waitingForStableFrame`, `_processingStableFrame` | `_isBusy` |
| **Comparison** | Full frame similarity | **Textbox area only** |
| **Complexity** | More explicit states | Simplified logic |
| **Follow-up Detection** | Basic | **Enhanced with dynamic threshold** |
| **Benefit** | Clear state tracking | **Simpler, more robust** |

**Why V2 Simplified**: Textbox-focused comparison naturally handles stability detection - if textbox area matches, it's stable. No need for separate "waiting" vs "processing" flags.

---

## ⏱️ Performance Metrics

### Target Budget: **67ms per frame** (15 FPS)

| Operation | Typical Time | Budget % | Notes |
|-----------|-------------|----------|-------|
| Capture Frame | 5-10ms | 15% | Same as V1 |
| Detect Textbox (cached) | 1-2ms | 3% | Strategy 1 hit |
| Detect Textbox (full) | 10-15ms | 22% | Strategy 2 full search |
| Crop Current Textbox | 0.2ms | 0.3% | Small bitmap copy |
| Crop Last Textbox | 0.2ms | 0.3% | Small bitmap copy |
| Textbox Comparison (500) | 0.1ms | 0.1% | Coarse sampling |
| Textbox Comparison (50) | 0.5ms | 0.7% | Fine sampling |
| Hash Calculation | 0.1ms | 0.1% | 7 pixel samples |
| **OCR (async)** | 30-100ms | **N/A** | **Non-blocking** |
| **Total (synchronous)** | **12-28ms** | **18-42%** | Excellent budget |

**V2 Performance Advantages**:
- **Faster Comparison**: Textbox area (~100K pixels) vs full frame (~2M pixels) = 20× smaller
- **Better OCR**: Windows OCR faster than Tesseract (30-100ms vs 50-200ms)
- **Simpler Logic**: 2-state machine = fewer conditionals

---

## 🎮 Detection Examples

### Example 1: Initial Dialogue Detection
```
Frame 0: Game with no dialogue visible
  → Capture frame
  → DetectTextbox() = null
  → _isBusy = false
  → Update _lastFrame
  → return

Frame 1: Dialogue appears (animating)
  → Capture frame
  → DetectTextbox() = (400, 80, 1200, 280)
  → Crop currentTextbox
  → Crop lastTextbox = null (no textbox last frame)
  → textboxMatches = false (no comparison possible)
  → _isBusy = false
  → Update _lastFrame
  → return (wait for stability)

Frame 2: Dialogue stable
  → Capture frame
  → DetectTextbox() = (400, 80, 1200, 280)
  → Crop currentTextbox, Crop lastTextbox
  → AreImagesSimilar(lastTextbox, currentTextbox, 500) = true
  → textboxMatches = true, !_isBusy = true
  → _isBusy = true (start processing)
  → Continue to OCR
  → Hash = "123456"
  → OCR async: "Welcome to Cornelia."
  → Add to Discovered collection
  
Frame 3-10: Same dialogue displayed
  → textboxMatches = true, _isBusy = true
  → Skip (already processing)
  
Frame 11: Player advances dialogue
  → Textbox content changed
  → textboxMatches = false
  → _isBusy = false (reset)
  → Wait for new stable dialogue
```

### Example 2: Follow-up Dialogue (Multi-line)
```
Frame 0: First line: "The king awaits you."
  → Process with sampleRate=500
  → _isBusy = true
  → OCR captures line 1

Frame 1-5: Player holds advance button
  → textboxMatches (sampleRate=500) = true
  → Skip (same dialogue)

Frame 6: Second line appears: "Please hurry!"
  → NOW using sampleRate=50 (99% similarity)
  → Small text change detected!
  → textboxMatches (sampleRate=50) = false
  → _isBusy = false (reset)
  → Update _lastFrame

Frame 7: Second line stable
  → textboxMatches (sampleRate=500) = true
  → _isBusy = true
  → OCR captures line 2: "Please hurry!"
```

**V2 Advantage**: Dynamic threshold (500 → 50) when busy catches follow-up dialogue changes that V1 might miss.

---

## 🔑 Key Design Decisions

### 1. **Why Textbox-Focused Comparison?**
- **Problem**: V1's full-frame comparison failed with background animations, menu changes
- **Solution**: Only compare cropped textbox pixels
- **Benefit**: Ignores everything except dialogue content
- **Result**: More reliable multi-line dialogue detection

### 2. **Why Dynamic Threshold (500 vs 50)?**
- **Not Busy (500)**: Coarse sampling finds new stable dialogue fast
- **Busy (50)**: Fine sampling catches subtle text changes (follow-up lines)
- **Benefit**: Balance between performance and sensitivity

### 3. **Why 2-State vs 3-State?**
- **V1 Problem**: Complex state machine with waiting/processing flags
- **V2 Solution**: Single `_isBusy` flag sufficient with textbox comparison
- **Benefit**: Simpler logic, fewer edge cases, easier to debug

### 4. **Why Keep V1 Hardcoded Coordinates?**
- **Config Attempt**: Oct 9 commit tried TextboxDetectionConfig
- **Result**: Detection completely broke (math should be identical but wasn't)
- **Emergency Revert**: Restored V1 hardcoded values (0.196875, 0.050926, etc.)
- **Lesson**: "Don't fix what works" - proven coordinates > configuration

### 5. **Why JSON-Based OCR Fixes?**
- **V1 Problem**: 100+ hardcoded regex rules in CleanOCRText()
- **V2 Solution**: Pack-specific ocr_fixes.json with visual editor
- **Benefit**: Authors can fix OCR errors without code changes
- **Scalability**: Each game pack has custom fixes

### 6. **Why WindowsOCR vs Tesseract?**
- **Performance**: 30-100ms vs 50-200ms (30-50% faster)
- **Accuracy**: Native Windows recognition excellent for modern text
- **Dependencies**: No external DLLs, built into Windows 10+
- **Simplicity**: No preprocessing needed (V1's 4× scaling removed)

### 7. **Why Keep Async OCR?**
- **Critical**: OCR takes 30-100ms, would kill 15 FPS if synchronous
- **Solution**: Same as V1 - Task.Run() for background processing
- **Result**: Main loop maintains 67ms budget, OCR runs in parallel

---

## 📝 Pseudocode Summary

```python
# V2 Detection Loop - Simplified View

def CaptureTick():
    currentFrame = CaptureGameWindow()
    
    # STEP 2: Textbox-Focused Comparison
    lock(lockObject):
        currentTextboxRect = detector.DetectTextbox(currentFrame)
        
        if currentTextboxRect == null:
            # No textbox found
            isBusy = false
            lastFrame = clone(currentFrame)
            dispose(currentFrame)
            return
        
        currentTextbox = CropImage(currentFrame, currentTextboxRect)
        
        if lastFrame != null:
            lastTextboxRect = detector.DetectTextbox(lastFrame)
            if lastTextboxRect != null:
                lastTextbox = CropImage(lastFrame, lastTextboxRect)
                
                # Dynamic threshold based on state
                if !isBusy:
                    textboxMatches = AreImagesSimilar(lastTextbox, currentTextbox, 500)
                else:
                    textboxMatches = AreImagesSimilar(lastTextbox, currentTextbox, 50)
        
        if textboxMatches and !isBusy:
            # New stable dialogue detected
            isBusy = true
            # Continue to OCR below
        elif textboxMatches and isBusy:
            # Same dialogue, skip
            dispose(currentFrame)
            return
        else:
            # Textbox changed, reset
            isBusy = false
            lastTextboxHash = ""
            lastFrame = clone(currentFrame)
            dispose(currentFrame)
            return
    
    # STEP 3: Re-verify textbox
    textboxRect = detector.DetectTextbox(currentFrame)
    if textboxRect == null:
        dispose(currentFrame)
        return
    
    # STEP 4: Hash check
    textboxImage = CropImage(currentFrame, textboxRect)
    textboxHash = GetImageHash(textboxImage)
    
    if textboxHash == lastTextboxHash:
        # Same content
        dispose(currentFrame)
        return
    
    lastTextboxHash = textboxHash
    
    # STEP 5: Async OCR
    textboxCopy = clone(textboxImage)
    dispose(currentFrame)
    
    Task.Run(async () =>
        try:
            # Extract text
            text = ocr.ExtractTextFast(textboxCopy).Trim()
            if IsEmpty(text): return
            
            # Apply fixes
            originalText = text
            text = fixes.Apply(text)
            
            # Normalize
            norm = TextNormalizer.Normalize(text)
            
            # Check duplicates
            lock(lockObject):
                if norm == lastNormalized or seen.Contains(norm):
                    return  # Skip duplicate
                lastNormalized = norm
                seen.Add(norm)
            
            # Create entry
            await Dispatcher.InvokeAsync(() =>
                entry = new PendingDialogueEntry {
                    Text = text,
                    OriginalOcrText = originalText,
                    Timestamp = UtcNow,
                    Approved = false
                }
                Discovered.Add(entry)
                TryPlayExistingAudio(entry)
            )
        finally:
            dispose(textboxCopy)
    )
```

---

## 🏗️ Modular Architecture

### Dependency Injection Graph

```
DiscoveryService
    ├── ITextboxDetector (injected or default)
    │   └── DynamicTextboxDetector(TextboxDetectionConfig, ILogger)
    │       └── FF1DetectionConfig.GetConfig()
    │
    ├── IOcrEngine (hardcoded)
    │   └── WindowsOCR()
    │
    ├── OcrFixesStore (injected)
    │   └── Loads: PackFolder/Configuration/ocr_fixes.json
    │
    ├── AudioPlaybackService (injected)
    │   └── NAudio-based playback for discovery preview
    │
    ├── AudioStore (injected)
    │   └── Loads: PackFolder/audio_manifest.json
    │
    └── ILogger<DiscoveryService> (injected)
        └── Structured logging with categories
```

### Service Lifetimes (DI Container)

```csharp
// Program.cs or App.xaml.cs setup:

services.AddSingleton<OcrFixesStore>();
services.AddSingleton<AudioStore>();
services.AddSingleton<AudioPlaybackService>();
services.AddTransient<DiscoveryService>();  // Per-session service
services.AddLogging(builder => {
    builder.AddDebug();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

---

## 🎯 Critical Success Factors

1. **Textbox-Focused Comparison**: Only compare dialogue area, ignore background
2. **Dynamic Threshold**: 500 (new) vs 50 (follow-up) for multi-line detection
3. **2-State Simplification**: `_isBusy` sufficient with textbox comparison
4. **Async OCR**: Maintains 15 FPS while OCR runs in background
5. **Pack-Based Fixes**: JSON configuration per game, no code changes needed
6. **Content Hash**: Text-area focused hashing catches subtle changes
7. **Modular DI**: Testable, replaceable components via interfaces
8. **Structured Logging**: ILogger with categories for debugging

---

## 📚 File References

**Core Files**:
- `GameWatcher.AuthorStudio\Services\DiscoveryService.cs` - Main orchestrator
- `GameWatcher.Engine\Detection\DynamicTextboxDetector.cs` - Textbox detection
- `GameWatcher.Engine\Detection\TextboxDetectionConfig.cs` - Pack configuration
- `GameWatcher.Runtime\Services\Capture\ScreenCapture.cs` - Frame capture
- `GameWatcher.Runtime\Services\OCR\WindowsOCR.cs` - OCR engine
- `GameWatcher.AuthorStudio\Services\OcrFixesStore.cs` - JSON-based fixes
- `GameWatcher.AuthorStudio\Services\TextNormalizer.cs` - Text normalization
- `GameWatcher.AuthorStudio\Services\AudioStore.cs` - Audio manifest
- `FF1.PixelRemaster\Detection\FF1DetectionConfig.cs` - FF1 configuration

**Pack Structure**:
```
FF1.PixelRemaster/
  ├── Configuration/
  │   └── ocr_fixes.json          // OCR correction rules
  ├── audio_manifest.json         // Audio file mappings
  └── voices/                     // Generated audio files
      └── [dialogue_hash].wav
```

**Location**: `GameWatcher-Platform\`

---

## 🔄 V1 to V2 Migration Summary

| Feature | V1 Implementation | V2 Implementation | Status |
|---------|-------------------|-------------------|--------|
| **Frame Capture** | ScreenCapture.cs | Same (ported) | ✅ Identical |
| **Similarity Detection** | Full frame | **Textbox area only** | ✅ Improved |
| **State Machine** | 3-state FSM | 2-state simplified | ✅ Simpler |
| **Textbox Detection** | DynamicTextboxDetector | Same algorithm | ✅ Ported |
| **Coordinates** | Hardcoded | Hardcoded (config reverted) | ✅ Working |
| **OCR Engine** | Tesseract | WindowsOCR | ✅ Faster |
| **OCR Fixes** | CleanOCRText() 100+ rules | JSON-based per pack | ✅ Flexible |
| **Text Normalization** | Inline | TextNormalizer service | ✅ Modular |
| **Deduplication** | Inline HashSet | _seen + _lastNormalized | ✅ Robust |
| **Logging** | Console.WriteLine | ILogger<T> structured | ✅ Professional |
| **Audio Integration** | Basic TTS | AudioStore + Playback | ✅ Enhanced |
| **Architecture** | Monolithic | DI + Services | ✅ Modular |
| **Multi-line Detection** | Basic | **Dynamic threshold** | ✅ Improved |

---

**End of V2 Detection Loop Documentation**  
*Last Updated: October 13, 2025*

---

Activity Log Dump from a live session:

2025-10-13 14:40:28.971 -04:00 [INF] SessionStore initialized. Sessions directory: C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions
2025-10-13 14:40:29.105 -04:00 [INF] Initializing MainWindow ViewModel
2025-10-13 14:40:29.386 -04:00 [INF] MainWindow initialized
2025-10-13 14:40:29.387 -04:00 [INF] GameWatcher Author Studio starting up
2025-10-13 14:40:29.657 -04:00 [INF] Audio format updated to: mp3
2025-10-13 14:40:29.788 -04:00 [INF] User settings loaded from: C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\user-settings.json
2025-10-13 14:40:29.790 -04:00 [INF] Initializing Discovery ViewModel
2025-10-13 14:40:29.791 -04:00 [INF] Initializing Speakers ViewModel
2025-10-13 14:40:29.791 -04:00 [INF] Initializing Voice Lab ViewModel
2025-10-13 14:40:29.797 -04:00 [INF] Initializing Pack Builder ViewModel
2025-10-13 14:40:29.800 -04:00 [INF] Auto-loading last pack: C:\Code Projects\GameWatcher\GameWatcher-Platform\FF1.PixelRemaster
2025-10-13 14:40:29.802 -04:00 [INF] Opening pack from: C:\Code Projects\GameWatcher\GameWatcher-Platform\FF1.PixelRemaster
2025-10-13 14:40:29.805 -04:00 [INF] Set current pack: C:\Code Projects\GameWatcher\GameWatcher-Platform\FF1.PixelRemaster -> Session file: C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:29.819 -04:00 [INF] Loaded session: 22 discovered, 0 accepted
2025-10-13 14:40:30.445 -04:00 [INF] Loaded pack session: 22 discovered, 0 accepted
2025-10-13 14:40:30.447 -04:00 [INF] Saved session: 0 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.447 -04:00 [INF] Saved session: 0 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.447 -04:00 [INF] Saved session: 0 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.447 -04:00 [INF] Saved session: 1 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.447 -04:00 [INF] Saved session: 1 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.447 -04:00 [INF] Saved session: 2 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.447 -04:00 [INF] Saved session: 2 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 3 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 3 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 4 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 4 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 5 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 5 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 6 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 6 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 7 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.448 -04:00 [INF] Saved session: 7 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 8 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 8 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 9 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 9 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 10 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 10 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 11 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 11 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 12 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.449 -04:00 [INF] Saved session: 12 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 13 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 13 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 14 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 14 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 15 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 15 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 16 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 16 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.450 -04:00 [INF] Saved session: 17 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 17 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 18 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 18 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 19 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 19 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 20 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 20 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 21 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 21 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 22 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.451 -04:00 [INF] Saved session: 22 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:30.511 -04:00 [INF] Pack loaded successfully: 0 entries from pack, 22 preserved from session
2025-10-13 14:40:30.512 -04:00 [INF] Loaded 0 OCR fixes into Settings view
2025-10-13 14:40:30.520 -04:00 [INF] Initializing Settings ViewModel
2025-10-13 14:40:30.522 -04:00 [INF] MainWindow ViewModel initialized successfully
2025-10-13 14:40:36.998 -04:00 [INF] Deleted discovery entry: My s-s-sister... I w-want my s-sister!
2025-10-13 14:40:36.999 -04:00 [INF] Saved session: 21 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:36.999 -04:00 [INF] Saved session: 21 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:38.846 -04:00 [INF] Deleted discovery entry: The king is searching for the prophesied Warriors of Light.
2025-10-13 14:40:38.846 -04:00 [INF] Saved session: 20 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:38.846 -04:00 [INF] Saved session: 20 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:39.534 -04:00 [INF] Deleted discovery entry: Her Majesty'- overcome with grief- herself inside her chambers. Pleas to upset her.
2025-10-13 14:40:39.534 -04:00 [INF] Saved session: 19 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:39.535 -04:00 [INF] Saved session: 19 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:40.109 -04:00 [INF] Deleted discovery entry: Jayne, Queen of Cornel ia. a... please bring my daughter.. ...back to me safely. . my
2025-10-13 14:40:40.110 -04:00 [INF] Saved session: 18 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:40.110 -04:00 [INF] Saved session: 18 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:40.693 -04:00 [INF] Deleted discovery entry: The king is search Ijarriors of Light.
2025-10-13 14:40:40.693 -04:00 [INF] Saved session: 17 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:40.693 -04:00 [INF] Saved session: 17 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:41.216 -04:00 [INF] Deleted discovery entry: Please, please save Lady Sarah!
2025-10-13 14:40:41.217 -04:00 [INF] Saved session: 16 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:41.217 -04:00 [INF] Saved session: 16 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:41.799 -04:00 [INF] Deleted discovery entry: Garland was once the greatest knight in the kingdom.
2025-10-13 14:40:41.799 -04:00 [INF] Saved session: 15 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:41.799 -04:00 [INF] Saved session: 15 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:42.343 -04:00 [INF] Deleted discovery entry: But power consumed him, and he lost sight of who he real ly was.
2025-10-13 14:40:42.343 -04:00 [INF] Saved session: 14 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:42.343 -04:00 [INF] Saved session: 14 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:42.878 -04:00 [INF] Deleted discovery entry: Our reports say that Garland fled north with the princess! To the Chaos Shrine!
2025-10-13 14:40:42.878 -04:00 [INF] Saved session: 13 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:42.878 -04:00 [INF] Saved session: 13 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:43.433 -04:00 [INF] Deleted discovery entry: p I ease, p I ease save Lady Sarah!
2025-10-13 14:40:43.434 -04:00 [INF] Saved session: 12 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:43.434 -04:00 [INF] Saved session: 12 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:43.966 -04:00 [INF] Deleted discovery entry: Our ancestors sealed weapons within this treasure roam four hundred years ago...
2025-10-13 14:40:43.966 -04:00 [INF] Saved session: 11 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:43.966 -04:00 [INF] Saved session: 11 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:44.544 -04:00 [INF] Deleted discovery entry: They then gave the key to the elf king to hold until the coming of the Ijarriars of Light.
2025-10-13 14:40:44.544 -04:00 [INF] Saved session: 10 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:44.544 -04:00 [INF] Saved session: 10 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:45.071 -04:00 [INF] Deleted discovery entry: This door has been secured with the mystic
2025-10-13 14:40:45.071 -04:00 [INF] Saved session: 9 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:45.071 -04:00 [INF] Saved session: 9 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:45.624 -04:00 [INF] Deleted discovery entry: Our ancestors sealed treasure roam four hl-
2025-10-13 14:40:45.624 -04:00 [INF] Saved session: 8 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:45.624 -04:00 [INF] Saved session: 8 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:46.151 -04:00 [INF] Deleted discovery entry: This door has been secured with the mystic key.
2025-10-13 14:40:46.151 -04:00 [INF] Saved session: 7 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:46.151 -04:00 [INF] Saved session: 7 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:46.687 -04:00 [INF] Deleted discovery entry: They then gave the key to the elf king to hold until the coming Clf the Ijarriors of Light.
2025-10-13 14:40:46.687 -04:00 [INF] Saved session: 6 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:46.687 -04:00 [INF] Saved session: 6 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:47.232 -04:00 [INF] Deleted discovery entry: Our ancestors sealed weap treasure roam four hundre
2025-10-13 14:40:47.232 -04:00 [INF] Saved session: 5 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:47.232 -04:00 [INF] Saved session: 5 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:47.729 -04:00 [INF] Deleted discovery entry: They then gave the key to hold until the coming of Light.
2025-10-13 14:40:47.730 -04:00 [INF] Saved session: 4 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:47.730 -04:00 [INF] Saved session: 4 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:48.248 -04:00 [INF] Deleted discovery entry: Our ancestors sealed weap treasure roam four hundre
2025-10-13 14:40:48.248 -04:00 [INF] Saved session: 3 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:48.248 -04:00 [INF] Saved session: 3 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:48.764 -04:00 [INF] Deleted discovery entry: They then gave the key to hold until the coming of Light.
2025-10-13 14:40:48.764 -04:00 [INF] Saved session: 2 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:48.765 -04:00 [INF] Saved session: 2 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:49.555 -04:00 [INF] Deleted discovery entry: The king is searching for the prophesied Ijarriars of Light.
2025-10-13 14:40:49.555 -04:00 [INF] Saved session: 1 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:49.555 -04:00 [INF] Saved session: 1 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:50.328 -04:00 [INF] Deleted discovery entry: The king is searching for the prophesied Ijarriars of Light.
2025-10-13 14:40:50.328 -04:00 [INF] Saved session: 0 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:40:50.328 -04:00 [INF] Saved session: 0 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:41:16.638 -04:00 [INF] Audio format updated to: wav
2025-10-13 14:41:45.177 -04:00 [INF] Starting discovery session
2025-10-13 14:41:45.178 -04:00 [INF] [Activity] Discovery started (15 FPS)
2025-10-13 14:41:56.734 -04:00 [INF] 🎯 TEXTBOX FOUND: {X=403,Y=80,Width=1111,Height=255}
2025-10-13 14:41:59.057 -04:00 [INF] [Activity] Found unique dialogue: I just don't know what we can do... p I ease help our prince!
2025-10-13 14:41:59.058 -04:00 [INF] Saved session: 1 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:41:59.058 -04:00 [INF] Saved session: 1 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:42:02.781 -04:00 [INF] 🎯 TEXTBOX FOUND: {X=403,Y=80,Width=1111,Height=255}
2025-10-13 14:42:02.965 -04:00 [INF] [Activity] Found unique dialogue: I am a sage. When the time is right, the future is revealed to me.
2025-10-13 14:42:02.965 -04:00 [INF] Saved session: 2 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:42:02.965 -04:00 [INF] Saved session: 2 discovered, 0 accepted to C:\Users\Jacob\AppData\Roaming\GameWatcher\AuthorStudio\sessions\session_b643eb8ba23ea9d2d2977562ae1e8848.json
2025-10-13 14:42:09.035 -04:00 [INF] 🎯 TEXTBOX FOUND: {X=403,Y=80,Width=1111,Height=255}
2025-10-13 14:42:11.130 -04:00 [INF] 🎯 TEXTBOX FOUND: {X=403,Y=80,Width=1111,Height=255}
2025-10-13 14:42:14.045 -04:00 [INF] Stopping discovery session
2025-10-13 14:42:14.046 -04:00 [INF] [Activity] Discovery paused
2025-10-13 14:42:14.046 -04:00 [INF] [Activity] Discovery stopped
2025-10-13 14:42:16.829 -04:00 [INF] MainWindow closing
2025-10-13 14:42:16.836 -04:00 [INF] GameWatcher Author Studio shutting down
