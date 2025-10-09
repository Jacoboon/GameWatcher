# Development Scripts Reference

PowerShell automation scripts for common GameWatcher development tasks.

## Location
All scripts are in `GameWatcher/scripts/` (parent directory, not Platform).

## Quick Reference

### Build & Run

```powershell
# Watch mode - runs live capture with V1 (Archive/SimpleLoop)
.\scripts\watch.ps1 -Title "FINAL FANTASY" -Fps 15

# Live authoring - watch + periodic voice generation
.\scripts\live_author.ps1 -Title "FINAL FANTASY" -MaxPerPass 10 -IntervalSec 20

# Smoke test - validates OCR on sample image
.\scripts\run_smoke.ps1
```

### Voice & Pack Management

```powershell
# Generate voices for discovered dialogue
.\scripts\author_gen.ps1 -Max 10              # Generate up to 10 voices
.\scripts\author_gen.ps1 -DryRun              # Preview what would be generated

# Sync voice files across directories
.\scripts\sync_voices.ps1                     # Copy voices to target locations

# Generate preview samples for engine
.\scripts\generate_engine_previews.ps1        # Create voice samples

# Sync artifacts (templates, assets)
.\scripts\sync_artifacts.ps1                  # Copy textbox templates
```

### Data Management

```powershell
# Clean data directories
.\scripts\clear_data.ps1                      # Remove cached/temp data

# Generate mapping stubs
.\scripts\generate_mapping_stubs.ps1          # Create dialogue JSON templates

# Fix audio associations
.\scripts\fix_audio_associations.py           # Repair audio mapping (Python)
```

### Release & Publishing

```powershell
# Create release build
.\scripts\release.ps1                         # Build release artifacts

# Publish to GitHub
.\scripts\publish_github.ps1                  # Upload release to GitHub
```

## Detailed Usage

### watch.ps1
**Purpose:** Run live V1 capture and voiceover playback

**Parameters:**
- `-Title` (required): Window title to capture (e.g., "FINAL FANTASY")
- `-Fps` (default: 15): Capture frame rate (higher = more CPU)
- `-TestImage`: Use static test image instead of live capture

**Example:**
```powershell
# Watch FF1 at 15 FPS
.\scripts\watch.ps1 -Title "FINAL FANTASY" -Fps 15

# Test with static image
.\scripts\watch.ps1 -TestImage
```

**What it does:**
1. Starts V1 SimpleLoop capture
2. Detects FF1 textbox
3. OCRs dialogue
4. Plays mapped audio from `assets/voices/`
5. Logs misses to `data/misses.json`

---

### author_gen.ps1
**Purpose:** Generate TTS voices for discovered dialogue

**Parameters:**
- `-Max` (default: 10): Maximum voices to generate per run
- `-DryRun`: Preview without actually generating
- `-Speaker`: Target specific speaker

**Requirements:**
- OpenAI API key in `Secrets/openai-api-key.txt`
- Discovered dialogue in `data/` from watch.ps1

**Example:**
```powershell
# Generate 10 voices
.\scripts\author_gen.ps1 -Max 10

# Preview what would be generated
.\scripts\author_gen.ps1 -DryRun

# Generate for specific speaker
.\scripts\author_gen.ps1 -Max 5 -Speaker "Princess Sarah"
```

**What it does:**
1. Loads misses from `data/misses.json`
2. Loads speaker assignments from `assets/maps/speakers.json`
3. Generates voices via OpenAI TTS
4. Saves to `assets/voices/{Speaker}/`
5. Updates `assets/maps/dialogue.en.json`

---

### live_author.ps1
**Purpose:** Combined watch + periodic voice generation

**Parameters:**
- `-Title` (required): Window title to capture
- `-MaxPerPass` (default: 10): Voices per generation cycle
- `-IntervalSec` (default: 30): Seconds between generations
- `-Fps` (default: 15): Capture frame rate

**Example:**
```powershell
# Watch and generate every 20 seconds
.\scripts\live_author.ps1 -Title "FINAL FANTASY" -MaxPerPass 10 -IntervalSec 20
```

**What it does:**
1. Starts watch.ps1 in background
2. Every {IntervalSec}, runs author_gen.ps1
3. Hot-reloads mapping JSON
4. Continues until stopped (Ctrl+C)

---

### sync_voices.ps1
**Purpose:** Copy voice files between directories

**Parameters:**
- `-Source` (default: `assets/voices`): Source directory
- `-Destination`: Target directory

**Example:**
```powershell
# Sync to Platform project
.\scripts\sync_voices.ps1 -Destination "GameWatcher-Platform/assets/voices"
```

**What it does:**
1. Copies all `.mp3`/`.wav` files from source
2. Preserves speaker folder structure
3. Skips existing files (no overwrites)

---

### generate_engine_previews.ps1
**Purpose:** Create voice preview samples for V2 Engine

**Example:**
```powershell
.\scripts\generate_engine_previews.ps1
```

**What it does:**
1. Scans `voices/` directory
2. Generates preview clips (first 3 seconds)
3. Saves to `voices/previews/`
4. Updates preview index JSON

---

### clear_data.ps1
**Purpose:** Clean temporary and cached data

**Parameters:**
- `-KeepMisses`: Preserve misses.json
- `-KeepCatalogs`: Preserve dialogue catalogs

**Example:**
```powershell
# Clean everything
.\scripts\clear_data.ps1

# Keep misses log
.\scripts\clear_data.ps1 -KeepMisses
```

**What it does:**
1. Removes `data/` directory contents
2. Clears OCR debug outputs
3. Resets session state

---

### sync_artifacts.ps1
**Purpose:** Copy textbox templates and assets

**Example:**
```powershell
.\scripts\sync_artifacts.ps1
```

**What it does:**
1. Copies FF1 textbox templates to working directory
2. Syncs asset files needed for detection
3. Creates necessary directories

---

### release.ps1
**Purpose:** Build release artifacts

**Parameters:**
- `-Configuration` (default: Release): Build configuration
- `-Platform` (default: win-x64): Target platform

**Example:**
```powershell
# Build release
.\scripts\release.ps1

# Build for specific platform
.\scripts\release.ps1 -Platform win-arm64
```

**What it does:**
1. Builds all projects in Release mode
2. Publishes self-contained executables
3. Copies assets and voices
4. Creates distribution package

---

## V2 Development

**Note:** Most scripts are for V1 (Archive/SimpleLoop). For V2 Platform development:

```bash
# Use dotnet directly
cd GameWatcher-Platform

# Build
dotnet build GameWatcher-Platform.sln

# Run apps
dotnet run --project GameWatcher.Studio
dotnet run --project GameWatcher.AuthorStudio
dotnet run --project GameWatcher.Runtime

# Release build
dotnet publish -c Release -r win-x64 --self-contained
```

## Common Workflows

### First-Time Setup
```powershell
# 1. Sync assets
.\scripts\sync_artifacts.ps1

# 2. Test OCR
.\scripts\run_smoke.ps1

# 3. Try live capture
.\scripts\watch.ps1 -Title "FINAL FANTASY"
```

### Voice Pack Creation
```powershell
# 1. Discover dialogue (play the game with watch running)
.\scripts\watch.ps1 -Title "FINAL FANTASY" -Fps 15

# 2. Generate voices
.\scripts\author_gen.ps1 -Max 20

# 3. Test playback (replay game sections)
.\scripts\watch.ps1 -Title "FINAL FANTASY"

# 4. Repeat until complete
```

### Continuous Authoring
```powershell
# One command - watch and auto-generate
.\scripts\live_author.ps1 -Title "FINAL FANTASY" -MaxPerPass 10 -IntervalSec 30
```

## Troubleshooting

### "Script not found"
Ensure you're in the repository root:
```powershell
cd "C:\Code Projects\GameWatcher"
.\scripts\watch.ps1 -Title "FINAL FANTASY"
```

### "Execution policy" error
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

### "API key not found"
Create `Secrets/openai-api-key.txt` with your OpenAI API key:
```powershell
New-Item -Path "Secrets" -ItemType Directory -Force
Set-Content -Path "Secrets/openai-api-key.txt" -Value "sk-..."
```

### "Tesseract not found"
Install Tesseract OCR and add to PATH, or set `TESSERACT_EXE` environment variable:
```powershell
$env:TESSERACT_EXE = "C:\Program Files\Tesseract-OCR\tesseract.exe"
```

## Script Maintenance

When adding new scripts:
1. Add usage example to this document
2. Include parameter documentation
3. Add to appropriate workflow section
4. Test on clean environment
