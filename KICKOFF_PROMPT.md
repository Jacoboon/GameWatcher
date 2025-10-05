# SimpleLoop.Gui - Real-time Integration Kickoff

## Current State Summary

We have successfully built a comprehensive WPF GUI application for the SimpleLoop game text capture system. The foundation is solid and committed to GitHub (commit `95cd343` on `stable-baseline` branch).

## What's Working ✅

### Core SimpleLoop Engine
- **SimpleTextboxDetector**: Proven baseline detector finding textboxes reliably at 2ms performance
- **DialogueCatalog**: Manages 30 dialogue entries with persistence to JSON
- **SpeakerCatalog**: Manages 4 speaker profiles with TTS and audio effects
- **Git Safety**: Stable commit `0c6868d` as fallback, current work on `stable-baseline`

### SimpleLoop.Gui WPF Application  
- **3-Tab Interface**: Live Monitor, Dialogue Catalog, Speaker Profiles
- **Dialogue Management**: Searchable DataGrid with filters, edit/delete, bulk operations
- **Speaker Editor**: TTS voice selection, open-ended audio effects, character profiles
- **Status Bar**: Real-time counts (30 dialogues, 4 speakers) and statistics at bottom
- **Always On Top**: Checkbox for overlay-style usage
- **Path Resolution**: Fixed catalog loading from `../SimpleLoop/` directory

### Technical Architecture
- **MVVM Patterns**: ObservableCollection binding, proper data flow
- **Thread Safety**: Exception handling and UI updates prepared
- **File Structure**: Clean separation between core engine and GUI
- **Build System**: .NET 8 WPF with SimpleLoop project references

## Next Phase Objectives 🎯

### Real-time Monitoring Dashboard (Todo #6)
**Goal**: Live view of capture system with interactive controls

**Required Components**:
1. **Live Preview Panel**: Show current game window capture with textbox overlay
2. **Detection Statistics**: FPS, textbox found/missed counts, OCR success rate
3. **Start/Stop Controls**: GUI buttons to control SimpleLoop capture engine
4. **Real-time Status**: Active window detection, capture health, processing pipeline

### SimpleLoop Engine Integration (Todo #7)  
**Goal**: Embed proven capture engine with background processing

**Technical Requirements**:
1. **Background Threading**: Run SimpleLoop capture without blocking UI
2. **Progress Callbacks**: Update GUI with detection results and statistics  
3. **State Management**: Proper start/stop/pause controls with cleanup
4. **Performance Monitoring**: Track FPS, detection accuracy, memory usage

## Key Integration Points 🔧

### Files to Modify
- `SimpleLoop.Gui/MainWindow.xaml`: Add Live Monitor tab controls (image preview, stats panels, control buttons)
- `SimpleLoop.Gui/MainWindow.xaml.cs`: Add background worker, progress handlers, SimpleLoop integration
- `SimpleLoop/Program.cs`: Extract core loop logic into reusable service class
- Status bar elements: `StatusFps`, `StatusRuntime` already exist but need population

### Architecture Pattern
```
GUI Thread (WPF) ←→ Background Worker ←→ SimpleLoop Engine
     ↑                     ↑                    ↑
  UI Updates         Progress Events      Game Capture
```

## Critical Code References 📋

### Existing UI Elements (Ready for Integration)
```xml
<!-- MainWindow.xaml - Live Monitor Tab (currently minimal) -->
<TabItem Header="Live Monitor" Name="LiveMonitorTab">
    <!-- ADD: Game window preview, stats panels, controls -->
</TabItem>

<!-- Status Bar (already implemented) -->
<TextBlock Name="StatusFps" Text="0.0" FontWeight="Bold"/>
<TextBlock Name="StatusRuntime" Text="00:00" FontWeight="Bold"/>
```

### Core SimpleLoop Logic (SimpleLoop/Program.cs lines 50-200)
- Window detection and capture setup
- Frame processing loop with SimpleTextboxDetector
- OCR pipeline and dialogue catalog integration
- Performance statistics and timing

### Success Criteria ✅
1. **Live Preview**: See game window with detected textbox overlays in GUI
2. **Interactive Control**: Start/stop capture from GUI buttons  
3. **Real-time Stats**: FPS, detection rate, runtime updating in status bar
4. **Background Processing**: Capture runs smoothly without freezing GUI
5. **Dialogue Integration**: New dialogue automatically appears in Dialogue Catalog tab

## Development Approach 🛠️

### Phase 1: Basic Integration
- Extract SimpleLoop main loop into `CaptureService` class
- Add background worker to GUI with basic start/stop
- Wire up progress events for status bar updates

### Phase 2: Live Preview  
- Add Image control to Live Monitor tab
- Stream captured frames to GUI (scaled/throttled for performance)
- Overlay textbox detection rectangles

### Phase 3: Advanced Monitoring
- Comprehensive statistics dashboard
- Performance graphs and health indicators
- Advanced control options (pause, single-step, settings)

## Current File State 📁
- **Branch**: `stable-baseline` (13 files changed, +1343 lines)
- **Catalog Data**: 30 dialogues, 4 speakers loaded successfully
- **Build Status**: Clean build, no errors, 6 warnings (nullable references)
- **GUI Status**: Fully functional for static data management

---

## Request for Next Agent

Please continue with **Phase 1: Basic Integration** by:

1. **Extract SimpleLoop Logic**: Create `CaptureService` class from existing `Program.cs` main loop
2. **Add Background Worker**: Implement in `MainWindow.xaml.cs` with start/stop controls  
3. **Wire Progress Events**: Connect capture statistics to status bar updates
4. **Test Integration**: Verify capture engine runs in background while GUI remains responsive

The foundation is solid - now we need to bring the proven capture engine into the GUI for real-time monitoring and control.