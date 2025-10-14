# GameWatcher Author Studio

**Author Studio** is the creator toolset for building game dialogue packs with voiceover content.

## Current Features (V2)

### Discovery V2
- **Automated Dialogue Capture:** Real-time textbox detection and OCR extraction
- **Dual-List Workflow:** Discovered lines → Review → Accept into catalog
- **OCR Correction System:**
  - Multi-word pattern support (e.g., "cast le" → "castle")
  - Pack-specific rules with engine-level correction logic
  - Visual indicators (🔧 wrench icon) for corrected lines
  - Multi-fix detection with pagination controls
- **Session Persistence:** Auto-saves Discovered and Accepted lines across app restarts
- **Duplicate Prevention:** Case-insensitive detection across both lists

### Audio Management
- **Voice Assignment:** Map speakers to voices with dropdown selection
- **Audio File Association:** Link dialogue lines to pre-rendered or TTS-generated audio files
- **Catalog Export:** Save complete dialogue catalog with audio mappings

### Settings & Configuration
- **OCR Correction Rules Editor:** Manual management of OCR fix dictionary
- **Pack Configuration:** Game-specific settings and metadata

## Planned Enhancements (V3+)

- Speaker analysis & automatic voice generation
- Pack builder & validator
- Community pack sharing
- SDK and plugin support for external games

This is intentionally scoped for V2 (dialogue cataloguing and pack authoring). The broader SDK and plugin vision will be addressed in V3+.