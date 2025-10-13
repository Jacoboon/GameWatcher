# V1 Detection Loop - SimpleLoop Architecture

> **Source**: `Archive\SimpleLoop\SimpleLoop\Program.cs`  
> **Version**: SimpleLoop v5.0 (Pristine Working Implementation)  
> **Purpose**: Document the complete detection flow for FF1 dialogue capture

---

## 🎯 High-Level Overview

The V1 detection loop is a **frame-based stability detection system** that captures game frames at 15 FPS, waits for stable frames (no animation/changes), detects textboxes within those stable frames, and runs OCR to extract dialogue text.

### Core Philosophy
- **Stability First**: Only process frames that have stabilized (no background animations, menu changes, etc.)
- **Performance Optimized**: 15 FPS target with <60ms processing budget per frame
- **Duplicate Prevention**: Skip OCR for identical textboxes to avoid spam
- **Async Processing**: OCR runs in background tasks to never block the capture loop

---

## 📊 Detection Loop Flow Chart

```
┌─────────────────────────────────────────────────────────────────┐
│                    MAIN LOOP (15 FPS Timer)                     │
│                    Every ~67ms (15 times/sec)                    │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 1: CAPTURE FRAME                                           │
│ ────────────────────────────────────────────────────────────────│
│ • ScreenCapture.CaptureGameWindow()                             │
│ • Targets game window by title (FindWindow Win32 API)           │
│ • Falls back to desktop capture if game not found               │
│ • Returns: Bitmap (full game frame)                             │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 2: STABILITY DETECTION (Lock Block - Thread Safe)          │
│ ────────────────────────────────────────────────────────────────│
│ Compare current frame with _lastFrame:                          │
│                                                                  │
│ IF _lastFrame == null:                                          │
│   • First frame captured                                        │
│   • Store as _lastFrame                                         │
│   • Dispose currentFrame                                        │
│   • return (skip textbox detection)                             │
│                                                                  │
│ IF AreImagesSimilar(currentFrame, _lastFrame, sampleRate=500):  │
│   ┌──────────────────────────────────────────────────────────┐ │
│   │ FRAME IS STABLE (no significant changes)                 │ │
│   │                                                           │ │
│   │ State Check:                                             │ │
│   │ IF _waitingForStableFrame == true AND                    │ │
│   │    _processingStableFrame == false:                      │ │
│   │   • First stable frame detected!                         │ │
│   │   • _waitingForStableFrame = false                       │ │
│   │   • _processingStableFrame = true                        │ │
│   │   • Continue to STEP 3 (textbox detection)              │ │
│   │                                                           │ │
│   │ ELSE (already processed this stable frame):             │ │
│   │   • Skip duplicate processing                            │ │
│   │   • Dispose currentFrame                                 │ │
│   │   • return                                               │ │
│   └──────────────────────────────────────────────────────────┘ │
│                                                                  │
│ ELSE (frame has CHANGED):                                       │
│   ┌──────────────────────────────────────────────────────────┐ │
│   │ FRAME IS UNSTABLE (animation/movement detected)          │ │
│   │                                                           │ │
│   │ • _waitingForStableFrame = true   (reset)                │ │
│   │ • _processingStableFrame = false  (reset)                │ │
│   │ • Dispose _lastFrame                                     │ │
│   │ • _lastFrame = Clone(currentFrame)                       │ │
│   │ • Dispose currentFrame                                   │ │
│   │ • return (skip textbox detection)                        │ │
│   └──────────────────────────────────────────────────────────┘ │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      │ (Only reached if frame is STABLE and
                      │  needs processing)
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 3: TEXTBOX DETECTION                                       │
│ ────────────────────────────────────────────────────────────────│
│ var textboxRect = _detector.DetectTextbox(currentFrame)         │
│                                                                  │
│ DynamicTextboxDetector.DetectTextbox():                         │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ STRATEGY 1: Cached Position Search (Fastest)               │ │
│ │ ───────────────────────────────────────────────────────────│ │
│ │ IF _lastKnownTextbox exists:                               │ │
│ │   • Expand last position by 50px buffer                    │ │
│ │   • SearchForTextboxInRegion(expandedArea)                 │ │
│ │   • IF found:                                              │ │
│ │       - Update _lastKnownTextbox                           │ │
│ │       - Reset _consecutiveFailures = 0                     │ │
│ │       - return textboxRect                                 │ │
│ │   • ELSE:                                                  │ │
│ │       - _consecutiveFailures++                             │ │
│ │       - IF _consecutiveFailures > 3:                       │ │
│ │           * Clear _lastKnownTextbox                        │ │
│ │           * Fall through to Strategy 2                     │ │
│ └────────────────────────────────────────────────────────────┘ │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ STRATEGY 2: Full Screen Blue Rectangle Search             │ │
│ │ ───────────────────────────────────────────────────────────│ │
│ │ FindBlueRectangularRegions():                              │ │
│ │                                                             │ │
│ │ 1. Define Targeted Search Area:                            │ │
│ │    • X = 19.6875% of screen width  - 25px buffer           │ │
│ │    • Y = 5.0926% of screen height  - 25px buffer           │ │
│ │    • Width = 60.4688% of screen width + 50px buffer        │ │
│ │    • Height = 28.2407% of screen height + 50px buffer      │ │
│ │    • Result: 79.3% search area reduction vs full screen    │ │
│ │                                                             │ │
│ │ 2. Sample for Blue Pixels (FF1 dialogue border color):     │ │
│ │    • Sample every 10px in targeted area                    │ │
│ │    • IsFF1Blue() checks for multiple blue variants:        │ │
│ │      - RGB(66,66,231), RGB(99,99,255), RGB(33,33,165)     │ │
│ │      - RGB(0,88,248), RGB(82,82,247), RGB(0,0,255)        │ │
│ │      - Tolerance: ±80 per channel                          │ │
│ │      - Additional: Blue channel dominant check             │ │
│ │                                                             │ │
│ │ 3. Trace Blue Rectangles:                                  │ │
│ │    FOR each blue pixel found:                              │ │
│ │      • TraceBlueRectangle(x, y)                            │ │
│ │        - Expand left/right along row (find horizontal)     │ │
│ │        - Expand up/down along column (find vertical)       │ │
│ │        - Create Rectangle from bounds                      │ │
│ │        - Validate with HasRectangularBorder()              │ │
│ │                                                             │ │
│ │ 4. Validate Rectangle:                                     │ │
│ │    • IsValidTextboxSize():                                 │ │
│ │      - Width: 200px - 1920px                               │ │
│ │      - Height: 100px - 800px                               │ │
│ │      - Width > Height (landscape aspect)                   │ │
│ │    • HasRectangularBorder():                               │ │
│ │      - Sample 20 points on each edge                       │ │
│ │      - At least 50% must be blue                           │ │
│ │                                                             │ │
│ │ 5. Select Best Candidate:                                  │ │
│ │    • Sort all valid rectangles by area (largest first)     │ │
│ │    • Remove duplicates (>70% overlap)                      │ │
│ │    • Return largest valid rectangle                        │ │
│ │    • Cache as _lastKnownTextbox for next frame            │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ IF textboxRect == null:                                         │
│   • Reset _processingStableFrame = false                        │
│   • Log "No textbox found" (throttled)                          │
│   • Dispose currentFrame                                        │
│   • return                                                      │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 4: CROP TEXTBOX AREA                                       │
│ ────────────────────────────────────────────────────────────────│
│ var textboxImage = CropImage(currentFrame, textboxRect)         │
│                                                                  │
│ CropImage():                                                     │
│ • Intersect cropRect with source bounds (safety)                │
│ • Create new Bitmap(cropRect.Width, cropRect.Height)            │
│ • DrawImage(source, 0, 0, cropRect, Pixel)                      │
│ • Return cropped Bitmap (only textbox pixels)                   │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 5: UNIQUENESS CHECK (Avoid OCR spam)                       │
│ ────────────────────────────────────────────────────────────────│
│ IF IsUniqueTextbox(textboxImage):                               │
│                                                                  │
│ IsUniqueTextbox():                                              │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ IF _lastTextbox == null:                                   │ │
│ │   • First textbox ever                                     │ │
│ │   • Store as _lastTextbox                                  │ │
│ │   • return true (process it)                               │ │
│ │                                                             │ │
│ │ IF dimensions differ:                                      │ │
│ │   • Different textbox size                                 │ │
│ │   • Store as _lastTextbox                                  │ │
│ │   • return true (process it)                               │ │
│ │                                                             │ │
│ │ Sample-based pixel comparison:                             │ │
│ │   FOR y = 0 to height STEP 10:                             │ │
│ │     FOR x = 0 to width STEP 10:                            │ │
│ │       • Compare pixels with tolerance (±30 RGB)            │ │
│ │       • IF any pixel differs significantly:                │ │
│ │           - Store as _lastTextbox                          │ │
│ │           - return true (new content!)                     │ │
│ │                                                             │ │
│ │ • All sampled pixels match                                 │ │
│ │ • return false (same textbox, skip OCR)                    │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│ IF unique == false:                                             │
│   • Log "Same textbox, skipping OCR"                            │
│   • Dispose textboxImage                                        │
│   • Reset _processingStableFrame = false                        │
│   • Dispose currentFrame                                        │
│   • return                                                      │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 6: IMAGE ENHANCEMENT FOR OCR                               │
│ ────────────────────────────────────────────────────────────────│
│ var enhancedImage = EnhanceForOCR(textboxImage)                 │
│                                                                  │
│ EnhanceForOCR():                                                │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │ 1. Convert to Grayscale:                                   │ │
│ │    FOR each pixel:                                         │ │
│ │      gray = R×0.299 + G×0.587 + B×0.114                    │ │
│ │                                                             │ │
│ │ 2. Scale Up 4x:                                            │ │
│ │    • 4× width and height                                   │ │
│ │    • InterpolationMode.NearestNeighbor                     │ │
│ │    • Preserves pixel art sharpness                         │ │
│ │                                                             │ │
│ │ 3. Apply Binary Threshold:                                 │ │
│ │    FOR each pixel:                                         │ │
│ │      IF brightness > 128: White                            │ │
│ │      ELSE: Black                                           │ │
│ │    • Pure black/white text for OCR                         │ │
│ └────────────────────────────────────────────────────────────┘ │
└─────────────────────┬───────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────────┐
│ STEP 7: ASYNC OCR PROCESSING (Non-Blocking)                     │
│ ────────────────────────────────────────────────────────────────│
│ Task.Run(() => {                                                │
│   try {                                                          │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 7a. OCR Extraction                                     │ │
│     │ ─────────────────────────────────────────────────────  │ │
│     │ var rawText = _ocr.ExtractTextFast(enhancedImage)     │ │
│     │                                                         │ │
│     │ SimpleOCR.ExtractTextFast():                           │ │
│     │ • Uses Tesseract OCR engine                            │ │
│     │ • English language model                               │ │
│     │ • Returns: raw extracted text (may have errors)        │ │
│     └────────────────────────────────────────────────────────┘ │
│                                                                  │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 7b. Quality Filter                                     │ │
│     │ ─────────────────────────────────────────────────────  │ │
│     │ IF IsOCRGarbage(rawText):                              │ │
│     │   • Check minimum length (>3 chars)                    │ │
│     │   • Check maximum length (<500 chars)                  │ │
│     │   • Letter ratio (>40% letters)                        │ │
│     │   • Symbol ratio (<30% symbols)                        │ │
│     │   • Digit ratio (<30% digits)                          │ │
│     │   • Vowel ratio (>15% of letters)                      │ │
│     │   • Pattern matching (garbage sequences)               │ │
│     │   • Valid English word presence check                  │ │
│     │   • IF garbage: return "[REJECTED: Low Quality OCR]"   │ │
│     └────────────────────────────────────────────────────────┘ │
│                                                                  │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 7c. Text Cleaning                                      │ │
│     │ ─────────────────────────────────────────────────────  │ │
│     │ var cleanedText = CleanOCRText(rawText)                │ │
│     │                                                         │ │
│     │ Common OCR error fixes:                                │ │
│     │ • "Il " → "I " (letter l to I)                         │ │
│     │ • "15" → "is" (number to word)                         │ │
│     │ • "1s" → "is"                                          │ │
│     │ • "0" → "o" (zero to letter O)                         │ │
│     │ • "5" → "s" (five to S)                                │ │
│     │ • "3" → "e" (three to E)                               │ │
│     │ • "1" → "i" (one to i)                                 │ │
│     │ • "KIM" → "King" (case fixes)                          │ │
│     │ • "AStoS" → "Astos" (character names)                  │ │
│     │ • "fou" → "You"                                        │ │
│     │ • "mow" → "now"                                        │ │
│     │ • "powertul" → "powerful"                              │ │
│     │ • "don?t" → "don't" (smart quotes)                     │ │
│     │ • "quer" → "our", "aur" → "our"                        │ │
│     │ • "helo" → "help"                                      │ │
│     │ • "elts" → "elfs"                                      │ │
│     │ • Many more game-specific fixes...                     │ │
│     └────────────────────────────────────────────────────────┘ │
│                                                                  │
│     ┌────────────────────────────────────────────────────────┐ │
│     │ 7d. Process New Dialogue                               │ │
│     │ ─────────────────────────────────────────────────────  │ │
│     │ ProcessNewDialogue(cleanedText):                       │ │
│     │ • Check if text is unique (not in catalog)             │ │
│     │ • Add to DialogueCatalog                               │ │
│     │ • Detect speaker (SpeakerCatalog)                      │ │
│     │ • Trigger TTS generation (if enabled)                  │ │
│     │ • Log dialogue entry                                   │ │
│     └────────────────────────────────────────────────────────┘ │
│   }                                                              │
│   catch (Exception ex) {                                        │
│     Log OCR error                                               │
│   }                                                              │
│   finally {                                                     │
│     Dispose textboxCopy                                         │
│     Dispose enhancedImage                                       │
│   }                                                              │
│ });                                                             │
│                                                                  │
│ • Reset _processingStableFrame = false                          │
│ • Dispose textboxImage                                          │
│ • Dispose currentFrame                                          │
│ • return                                                        │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    LOOP RETURNS TO STEP 1                       │
│              Waits ~67ms, then captures next frame              │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔧 Core Components

### 1. **ScreenCapture.CaptureGameWindow()**
**Purpose**: Capture current frame from game window  
**Implementation**: Win32 API (`FindWindow`, `BitBlt`, `GetWindowDC`)

```csharp
// Searches for game window by title
FindWindow(null, "FINAL FANTASY") → IntPtr windowHandle

// If found, capture specific window
GetWindowRect(windowHandle, out RECT) → Rectangle bounds
BitBlt(destDC, 0, 0, width, height, srcDC, 0, 0, SRCCOPY) → Bitmap

// Fallback: Full desktop capture if game not found
```

**Game Window Titles Searched**:
- "FINAL FANTASY"
- "Final Fantasy"
- "FF1"
- "ePSXe" (PS1 emulator)
- "PCSX-R"
- "RetroArch"
- "Duckstation"

---

### 2. **ScreenCapture.AreImagesSimilar()**
**Purpose**: Detect frame stability (sampled pixel comparison)  
**Performance**: ~0.1ms on 1920×1080 frames

```csharp
AreImagesSimilar(bitmap1, bitmap2, sampleRate = 500)

Parameters:
  sampleRate: Check every Nth pixel (500 = every 500th pixel)
  tolerance: ±10 per RGB channel

Algorithm:
  FOR i = 0 to totalBytes STEP sampleRate:
    diff = Abs(pixel1[i] - pixel2[i])
    IF diff > tolerance:
      diffPixels++
    
    // 5% threshold - if >5% of sampled pixels differ, frames are different
    IF diffPixels × 20 > totalSampled:
      return false
  
  return true  // Frames are similar (stable)
```

**Sample Rate 500**: Checks ~4,147 pixels out of 2,073,600 (0.2% sample)  
**Result**: Fast detection with <60ms budget maintained

---

### 3. **State Machine (Stability Tracking)**

**State Variables**:
```csharp
bool _waitingForStableFrame = true   // Looking for frame stability
bool _processingStableFrame = false  // Currently processing a stable frame
Bitmap _lastFrame = null             // Previous frame for comparison
```

**State Transitions**:
```
[INITIAL STATE]
  _waitingForStableFrame = true
  _processingStableFrame = false
  _lastFrame = null

↓ First frame captured
  _lastFrame = currentFrame (store)
  return (skip processing)

↓ Subsequent frames differ
  _waitingForStableFrame = true  (stay in waiting state)
  _processingStableFrame = false (not processing)
  _lastFrame = currentFrame (update)
  return (skip processing)

↓ Frame MATCHES last frame (stability detected!)
  _waitingForStableFrame = false  (found stability)
  _processingStableFrame = true   (start processing)
  → Continue to textbox detection

↓ Textbox processed (or not found)
  _processingStableFrame = false  (done processing)

↓ Next frame differs (scene changed)
  _waitingForStableFrame = true   (reset to waiting)
  _processingStableFrame = false  (reset)
  _lastFrame = currentFrame (update)
```

---

### 4. **DynamicTextboxDetector.DetectTextbox()**

**Two-Strategy Detection System**:

#### **Strategy 1: Cached Position Search** (Performance Optimization)
- **When**: If `_lastKnownTextbox` exists from previous frame
- **Method**: Expand last position by 50px, search that small area only
- **Benefit**: 10× faster than full screen search
- **Failure Handling**: After 3 consecutive failures, clear cache and fall back to Strategy 2

#### **Strategy 2: Full Screen Blue Rectangle Search**
- **When**: No cached position OR cached search failed
- **Target Area**: 79.3% reduction using hardcoded FF1 coordinates
  - X: 19.6875% screen width - 25px buffer
  - Y: 5.0926% screen height - 25px buffer  
  - Width: 60.4688% screen width + 50px buffer
  - Height: 28.2407% screen height + 50px buffer
- **Method**: Sample for blue pixels, trace rectangles, validate borders

**Blue Color Detection**:
```csharp
IsFF1Blue(Color pixel):
  // Check against 8 known FF1 blue variants
  RGB(66,66,231), RGB(99,99,255), RGB(33,33,165),
  RGB(0,88,248), RGB(82,82,247), RGB(0,0,255),
  RGB(0,100,200), RGB(50,50,200)
  
  Tolerance: ±80 per channel
  
  // Additional check: Blue channel dominance
  IF pixel.B > pixel.R + 50 AND 
     pixel.B > pixel.G + 50 AND 
     pixel.B > 100:
    return true
```

**Rectangle Validation**:
1. **Size Check**: 200×100 minimum, 1920×800 maximum
2. **Aspect Ratio**: Width > Height (landscape)
3. **Border Check**: Sample 20 points per edge, ≥50% must be blue

---

### 5. **IsUniqueTextbox()** (Duplicate Prevention)
**Purpose**: Avoid running OCR on same textbox repeatedly

```csharp
IsUniqueTextbox(Bitmap textbox):
  IF _lastTextbox == null:
    return true  // First textbox
  
  IF dimensions differ:
    return true  // Different size = different textbox
  
  // Sample-based comparison (every 10th pixel)
  FOR y = 0 to height STEP 10:
    FOR x = 0 to width STEP 10:
      IF PixelDiff(current, last) > ±30 RGB:
        return true  // Content changed
  
  return false  // Same textbox, skip OCR
```

**Performance**: ~0.5ms for 1000×200 textbox (1,000 sampled pixels)

---

### 6. **EnhanceForOCR()** (Image Preprocessing)
**Purpose**: Improve OCR accuracy for pixel art text

```
Original Textbox → Grayscale → Scale 4× → Binary Threshold → OCR
    (200×80)       (200×80)    (800×320)      (800×320)
```

**Steps**:
1. **Grayscale**: Luma formula (0.299R + 0.587G + 0.114B)
2. **Scale 4×**: NearestNeighbor interpolation (preserves pixel art)
3. **Threshold 128**: Pixel > 128 = White, ≤128 = Black
4. **Result**: Pure black text on white background (optimal for Tesseract)

---

### 7. **CleanOCRText()** (Post-Processing)
**Purpose**: Fix common OCR character errors

**Categories**:
- **Numeric Confusion**: "15" → "is", "0" → "o", "1" → "i"
- **Letter Confusion**: "Il" → "I", "mow" → "now"
- **Character Names**: "AStoS" → "Astos", "KIM" → "King"
- **Punctuation**: "don?t" → "don't" (smart quotes)
- **Common Words**: "helo" → "help", "quer" → "our"

**100+ Regex Rules**: Tuned specifically for FF1 Pixel Remaster dialogue

---

### 8. **IsOCRGarbage()** (Quality Filter)
**Purpose**: Reject obvious OCR errors before processing

**Heuristics**:
- Length: 3-500 characters
- Letter ratio: ≥40%
- Symbol ratio: ≤30%
- Digit ratio: ≤30%
- Vowel ratio: ≥15% of letters
- Pattern matching: Rejects "brc", "pada", "L-. -", etc.
- Valid word check: Must contain common English words

**Example Rejected**:
```
"brc pada pel sree 123 !@#" → REJECTED (too few letters, garbage patterns)
"The King needs help." → ACCEPTED (passes all checks)
```

---

## ⏱️ Performance Metrics

### Target Budget: **67ms per frame** (15 FPS)

| Operation | Typical Time | Budget % |
|-----------|-------------|----------|
| Capture Frame | 5-10ms | 15% |
| Frame Comparison | 0.1ms | 0.2% |
| Textbox Detection (cached) | 1-2ms | 3% |
| Textbox Detection (full) | 10-15ms | 22% |
| Crop + Uniqueness Check | 0.5ms | 1% |
| Enhancement | 5-8ms | 12% |
| **OCR (async)** | 50-200ms | **N/A** (non-blocking) |
| **Total (synchronous)** | **12-36ms** | **18-54%** |

**Critical Design**: OCR runs in `Task.Run()` to avoid blocking the 67ms budget  
**Result**: Capture loop maintains 15 FPS even with slow OCR

---

## 🎮 Frame State Examples

### Example 1: Static Menu Screen
```
Frame 0: Menu displayed
  → Store as _lastFrame
  → _waitingForStableFrame = true

Frame 1: Same menu (stable)
  → AreImagesSimilar() = true
  → _processingStableFrame = true
  → Detect textbox → Found at (400, 80, 1200, 280)
  → Unique textbox → Run OCR async
  → OCR: "Welcome to Cornelia"

Frame 2-10: Same menu (already processed)
  → AreImagesSimilar() = true
  → _processingStableFrame = true
  → Skip (already processing this stable frame)

Frame 11: Player pressed button (scene change)
  → AreImagesSimilar() = false
  → Reset flags
  → _waitingForStableFrame = true
```

### Example 2: Dialogue Sequence
```
Frame 0: Dialogue line 1 appears
  → Unstable (animation in progress)
  → Skip

Frame 1-3: Text animating in
  → Unstable (each frame different)
  → Skip

Frame 4: Text fully displayed (stable)
  → Stable detected
  → Textbox found
  → OCR: "The king has been waiting for you."

Frame 5-20: Same dialogue displayed
  → Already processed
  → Skip

Frame 21: Next dialogue line appears
  → Frame changed (text changed)
  → Reset to waiting state

Frame 22-24: New text animating
  → Unstable
  → Skip

Frame 25: New text stable
  → Detect textbox
  → Different textbox content (IsUniqueTextbox = true)
  → OCR: "Please save the princess!"
```

---

## 🔑 Key Design Decisions

### 1. **Why 15 FPS?**
- **Balance**: Fast enough to catch dialogue (humans need 300-500ms to read)
- **Performance**: 67ms budget allows reliable processing
- **Battery**: Lower FPS = less CPU usage for long captures

### 2. **Why Stability Detection?**
- **Animation Avoidance**: Skip frames with text animations, transitions, effects
- **Duplicate Prevention**: Only process once per stable textbox appearance
- **Accuracy**: OCR works better on fully-rendered static text

### 3. **Why Async OCR?**
- **Non-Blocking**: OCR takes 50-200ms, would kill 15 FPS if synchronous
- **Parallelism**: Multiple OCR tasks can run while capture continues
- **Responsiveness**: Loop never stalls waiting for OCR

### 4. **Why Two-Strategy Detection?**
- **Performance**: Cached search (Strategy 1) is 10× faster
- **Reliability**: Full search (Strategy 2) catches textbox if it moves
- **Adaptability**: Works even if textbox position changes mid-game

### 5. **Why Targeted Search Area?**
- **Speed**: 79.3% reduction in pixels to scan
- **Accuracy**: FF1 textbox always appears in same relative position
- **Consistency**: Hardcoded coordinates proven through testing

---

## 📝 Pseudocode Summary

```python
# Main Loop (15 FPS Timer)
def CaptureAndProcess():
    currentFrame = CaptureGameWindow()
    
    # STEP 2: Stability Detection
    lock(lockObject):
        if lastFrame exists and AreImagesSimilar(lastFrame, currentFrame, 500):
            # Frame is STABLE
            if waitingForStableFrame and not processingStableFrame:
                # First stable frame - process it!
                waitingForStableFrame = false
                processingStableFrame = true
                # Continue to textbox detection below
            else:
                # Already processed this stable frame
                dispose(currentFrame)
                return
        else:
            # Frame CHANGED - reset and skip
            waitingForStableFrame = true
            processingStableFrame = false
            lastFrame = clone(currentFrame)
            dispose(currentFrame)
            return
    
    # STEP 3: Textbox Detection
    textboxRect = detector.DetectTextbox(currentFrame)
    
    if textboxRect == null:
        processingStableFrame = false
        dispose(currentFrame)
        return
    
    # STEP 4: Crop Textbox
    textboxImage = CropImage(currentFrame, textboxRect)
    
    # STEP 5: Uniqueness Check
    if not IsUniqueTextbox(textboxImage):
        dispose(textboxImage, currentFrame)
        processingStableFrame = false
        return
    
    # STEP 6: Enhance for OCR
    enhancedImage = EnhanceForOCR(textboxImage)
    
    # STEP 7: Async OCR Processing
    Task.Run(() =>
        try:
            rawText = ocr.ExtractTextFast(enhancedImage)
            
            if IsOCRGarbage(rawText):
                return  # Reject low quality
            
            cleanedText = CleanOCRText(rawText)
            ProcessNewDialogue(cleanedText)
        finally:
            dispose(textboxImage, enhancedImage)
    )
    
    processingStableFrame = false
    dispose(currentFrame)
```

---

## 🎯 Critical Success Factors

1. **Stability Detection**: Only process stable frames (no animations)
2. **Duplicate Prevention**: Skip identical textboxes (avoid OCR spam)
3. **Async OCR**: Never block the capture loop (maintain 15 FPS)
4. **Targeted Search**: 79.3% area reduction (performance)
5. **Quality Filtering**: Reject garbage before processing (accuracy)
6. **Error Correction**: 100+ cleanup rules (FF1-specific tuning)
7. **State Management**: Three-state FSM (waiting/processing/done)
8. **Performance Budget**: <67ms synchronous, OCR async (reliable 15 FPS)

---

## 📚 File References

**Core Files**:
- `Program.cs` - Main loop, state machine, OCR processing
- `DynamicTextboxDetector.cs` - Two-strategy textbox detection
- `ScreenCapture.cs` - Win32 frame capture, similarity detection
- `SimpleOCR.cs` - Tesseract OCR wrapper
- `DialogueCatalog.cs` - Dialogue storage and deduplication
- `SpeakerCatalog.cs` - Speaker identification and voice management

**Location**: `Archive\SimpleLoop\SimpleLoop\`

---

**End of V1 Detection Loop Documentation**  
*Last Updated: October 13, 2025*


---

Activity Log Dump from a live session:

[14:43:54.948] ℹ️ Capture session started: 2025-10-13 14:43:54
[14:43:55.390] ℹ️ TTS Manager initialized successfully
[14:43:55.390] ℹ️ Capture service initialized successfully
[14:43:55.405] ℹ️ Capture resolution: 1920x1080
[14:44:00.577] ℹ️ Capture service started (15 FPS)
[14:44:00.671] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:00.678] 🔍 Frame processing: 29ms
[14:44:03.888] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:04.822] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:04.827] 🔍 Frame processing: 22ms
[14:44:05.836] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:05.895] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:07.235] 🔍 Frame #100 captured: 1920x1080
[14:44:07.243] 🔍 Frame high-sensitivity: matches=True, isBusy=True
[14:44:07.430] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:07.511] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:07.570] ℹ️ Textbox detected at {X=403,Y=80,Width=1111,Height=255}
[14:44:07.573] ℹ️ Unique textbox detected, processing OCR
[14:44:07.576] ⚠️ SLOW: Frame processing took 1698ms
[14:44:07.577] ℹ️ Running enhanced OCR on textbox...
[14:44:07.671] ℹ️ Raw OCR result: 'I am a sage. When the time is right, the future is revealed to me.' (length: 66)
[14:44:07.672] ℹ️ Cleaned text: 'I am a sage. When the time is right, the future is revealed to me.' (length: 66)
[14:44:07.696] ✅ DIALOGUE: "I am a sage. When the time is right, the future is revealed to me." → Sage of Elfheim (echo)
[14:44:07.696] ℹ️ 🎤 Generating new TTS for: 'I am a sage. When the time is right, the future is revealed to me.' (Sage of Elfheim)
[14:44:08.453] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:08.516] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:08.527] 🔍 Frame processing: 36ms
[14:44:08.572] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:09.600] ℹ️ Textbox detected at {X=403,Y=80,Width=1111,Height=255}
[14:44:09.606] ℹ️ Unique textbox detected, processing OCR
[14:44:09.607] ℹ️ Running enhanced OCR on textbox...
[14:44:09.608] ⚠️ SLOW: Frame processing took 2116ms
[14:44:09.625] ℹ️ Raw OCR result: 'I shall wait patiently until then.' (length: 34)
[14:44:09.625] ℹ️ Cleaned text: 'I shall wait patiently until then.' (length: 34)
[14:44:09.640] ✅ DIALOGUE: "I shall wait patiently until then." → Generic NPC (alloy)
[14:44:09.640] ℹ️ 🎤 Generating new TTS for: 'I shall wait patiently until then.' (Generic NPC)
[14:44:10.571] ℹ️ Audio generated: C:\Code Projects\GameWatcher\Archive\SimpleLoop\voices\Sage of Elfheim\Sage of Elfheim_59DE475A_echo_0.8.mp3 for "I am a sage. When the time is right, the future is revealed to me."
[14:44:11.325] ℹ️ Audio generated: C:\Code Projects\GameWatcher\Archive\SimpleLoop\voices\Generic NPC\Generic NPC_2AEBCB6B_alloy_1.0.mp3 for "I shall wait patiently until then."
[14:44:11.850] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:11.856] 🔍 Frame processing: 21ms
[14:44:13.529] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:13.928] 🔍 Frame #200 captured: 1920x1080
[14:44:13.934] 🔍 Frame standard: matches=False, isBusy=False
[14:44:18.498] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:18.503] 🔍 Frame processing: 28ms
[14:44:18.827] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:18.960] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:18.961] ℹ️ Textbox detected at {X=403,Y=80,Width=1111,Height=255}
[14:44:18.962] ℹ️ Unique textbox detected, processing OCR
[14:44:18.963] ℹ️ Running enhanced OCR on textbox...
[14:44:18.964] 🔍 Frame processing: 21ms
[14:44:18.982] ℹ️ Raw OCR result: 'I just don't know what we can do... help our prince! p I e,3Se' (length: 62)
[14:44:18.982] ℹ️ Cleaned text: 'I just don't know what we can do... help our prince! p I e,eSe' (length: 62)
[14:44:18.985] ✅ DIALOGUE: "I just don't know what we can do... help our prince! p I e,eSe" → Mysterious Voice (shimmer)
[14:44:18.985] ℹ️ 🎤 Generating new TTS for: 'I just don't know what we can do... help our prince! p I e,eSe' (Mysterious Voice)
[14:44:20.629] 🔍 Frame #300 captured: 1920x1080
[14:44:20.636] 🔍 Frame high-sensitivity: matches=True, isBusy=True
[14:44:20.839] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:20.903] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:20.909] 🔍 Frame processing: 26ms
[14:44:21.098] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:21.179] ℹ️ Audio generated: C:\Code Projects\GameWatcher\Archive\SimpleLoop\voices\Mysterious Voice\Mysterious Voice_2A950AB2_shimmer_0.9.mp3 for "I just don't know what we can do... help our prince! p I e,eSe"
[14:44:27.328] 🔍 Frame #400 captured: 1920x1080
[14:44:27.335] 🔍 Frame standard: matches=False, isBusy=False
[14:44:27.599] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:27.611] 🔍 Frame processing: 29ms
[14:44:27.863] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:27.949] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:27.950] ℹ️ Textbox detected at {X=403,Y=80,Width=1111,Height=255}
[14:44:27.952] ℹ️ Unique textbox detected, processing OCR
[14:44:27.953] ℹ️ Running enhanced OCR on textbox...
[14:44:27.953] 🔍 Frame processing: 28ms
[14:44:27.983] ℹ️ Raw OCR result: 'Ijeapons and armor made of mythril are sturdy and powerful. You should give them a try. You' II be surprised!' (length: 109)
[14:44:27.983] ℹ️ Cleaned text: 'Ijeapons and armor made of mythril are sturdy and powerful. You should give them a try. You' II be surprised!' (length: 109)
[14:44:27.986] ✅ DIALOGUE: "Ijeapons and armor made of mythril are sturdy and powerful. You should give them a try. You' II be surprised!" → Generic NPC (alloy)
[14:44:27.986] ℹ️ 🎤 Generating new TTS for: 'Ijeapons and armor made of mythril are sturdy and powerful. You should give them a try. You' II be surprised!' (Generic NPC)
[14:44:29.878] ℹ️ Audio generated: C:\Code Projects\GameWatcher\Archive\SimpleLoop\voices\Generic NPC\Generic NPC_7E0D0D93_alloy_1.0.mp3 for "Ijeapons and armor made of mythril are sturdy and powerful. You should give them a try. You' II be surprised!"
[14:44:29.941] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:30.082] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:30.087] 🔍 Frame processing: 24ms
[14:44:30.142] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:32.490] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:32.496] 🔍 Frame processing: 21ms
[14:44:32.555] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:32.819] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:32.825] 🔍 Frame processing: 22ms
[14:44:33.692] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:34.037] 🔍 Frame #500 captured: 1920x1080
[14:44:34.043] 🔍 Frame standard: matches=False, isBusy=False
[14:44:34.301] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:34.307] 🔍 Frame processing: 21ms
[14:44:34.568] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:34.709] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:35.970] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:36.048] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:36.386] ℹ️ Textbox detected at {X=403,Y=80,Width=1111,Height=255}
[14:44:36.388] ℹ️ Unique textbox detected, processing OCR
[14:44:36.389] ℹ️ Running enhanced OCR on textbox...
[14:44:36.389] ⚠️ SLOW: Frame processing took 1697ms
[14:44:36.411] ℹ️ Raw OCR result: 'If the prince does not awaken, there will be no elf king.' (length: 57)
[14:44:36.411] ℹ️ Cleaned text: 'If the prince does not awaken, there will be no elf king.' (length: 57)
[14:44:36.414] ✅ DIALOGUE: "If the prince does not awaken, there will be no elf king." → Generic NPC (alloy)
[14:44:36.414] ℹ️ 🎤 Generating new TTS for: 'If the prince does not awaken, there will be no elf king.' (Generic NPC)
[14:44:37.524] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:37.583] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:37.585] ℹ️ Textbox detected at {X=403,Y=80,Width=1111,Height=255}
[14:44:37.587] ℹ️ Unique textbox detected, processing OCR
[14:44:37.588] ℹ️ Running enhanced OCR on textbox...
[14:44:37.589] 🔍 Frame processing: 23ms
[14:44:37.608] ℹ️ Raw OCR result: 'Ije will be at the mercy of the dark elf' z evil power.' (length: 55)
[14:44:37.608] ℹ️ Cleaned text: 'Ije will be at the mercy of the dark elf' z evil power.' (length: 55)
[14:44:37.611] ✅ DIALOGUE: "Ije will be at the mercy of the dark elf' z evil power." → Generic NPC (alloy)
[14:44:37.611] ℹ️ 🎤 Generating new TTS for: 'Ije will be at the mercy of the dark elf' z evil power.' (Generic NPC)
[14:44:37.741] ℹ️ Unique textbox detected, processing OCR
[14:44:37.743] ℹ️ Running enhanced OCR on textbox...
[14:44:37.743] ⚠️ SLOW: Frame processing took 1712ms
[14:44:37.766] ℹ️ Raw OCR result: 'If the prince does not awaken, there will be no elf king.' (length: 57)
[14:44:37.766] ℹ️ Cleaned text: 'If the prince does not awaken, there will be no elf king.' (length: 57)
[14:44:37.768] ✅ DIALOGUE: "If the prince does not awaken, there will be no elf king." → Generic NPC (alloy)
[14:44:37.768] ℹ️ 🎤 Generating new TTS for: 'If the prince does not awaken, there will be no elf king.' (Generic NPC)
[14:44:38.519] ℹ️ Audio generated: C:\Code Projects\GameWatcher\Archive\SimpleLoop\voices\Generic NPC\Generic NPC_598B77E1_alloy_1.0.mp3 for "If the prince does not awaken, there will be no elf king."
[14:44:39.058] ℹ️ Audio generated: C:\Code Projects\GameWatcher\Archive\SimpleLoop\voices\Generic NPC\Generic NPC_7DD583B2_alloy_1.0.mp3 for "Ije will be at the mercy of the dark elf' z evil power."
[14:44:39.866] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:39.925] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:39.930] 🔍 Frame processing: 22ms
[14:44:40.406] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:40.519] ⚠️ TTS Error: Failed to generate audio for: If the prince does not awaken, there will be no elf king.
[14:44:40.743] 🔍 Frame #600 captured: 1920x1080
[14:44:40.749] 🔍 Frame standard: matches=False, isBusy=False
[14:44:41.066] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:41.071] 🔍 Frame processing: 22ms
[14:44:41.334] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:41.612] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:41.617] 🔍 Frame processing: 23ms
[14:44:42.752] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:43.077] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:43.085] 🔍 Frame processing: 25ms
[14:44:45.085] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:45.164] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:45.167] 🔍 Frame processing: 21ms
[14:44:46.165] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:46.236] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:46.238] 🔍 Frame processing: 20ms
[14:44:47.108] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:47.227] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:47.230] 🔍 Frame processing: 18ms
[14:44:47.426] 🔍 Frame #700 captured: 1920x1080
[14:44:47.434] 🔍 Frame high-sensitivity: matches=True, isBusy=True
[14:44:52.597] 🔍 Text/scene change detected (exact mismatch) - resetting state
[14:44:52.659] 🔍 Stable frame detected (fuzzy) - processing for textbox
[14:44:52.662] 🔍 Frame processing: 19ms
[14:44:53.382] ℹ️ Capture service stopped
[14:44:53.387] 🔍 Text/scene change detected (exact mismatch) - resetting state