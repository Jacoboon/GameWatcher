# GameWatcher Studio User Guide

Welcome to **GameWatcher Studio** - the universal GUI for the GameWatcher V2 Platform! This guide will help you get started with managing game packs, monitoring performance, and configuring your voiceover experience.

## 🚀 Getting Started

### System Requirements
- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- Supported games (see Game Pack compatibility)

### First Launch
1. Navigate to your GameWatcher installation directory
2. Run `GameWatcher.Studio.exe`
3. The application will initialize and scan for available game packs

## 📦 Pack Management

### Understanding Game Packs
Game Packs are modular components that provide game-specific functionality:
- **Detection Logic** - How to identify the game window and dialogue boxes
- **Speaker Profiles** - Voice mappings for characters
- **Configuration** - Game-specific settings and optimizations

### Loading Game Packs

#### From the Pack Manager Tab:
1. Click the **Pack Manager** tab
2. Available packs will be listed automatically
3. Select a pack from the list
4. Click **Load Pack** to activate it
5. The status will change to "Loaded" when successful

#### Automatic Detection:
- GameWatcher can automatically detect running games
- Compatible packs will be loaded automatically
- Check **Settings > General > Auto Start Monitoring** to enable

### Pack Information
Each pack displays:
- **Name** - Pack identifier (e.g., "FF1.PixelRemaster")
- **Version** - Pack version number
- **Description** - What the pack provides
- **Supported Games** - Compatible game executables
- **Status** - Loaded/Available state

## 🎮 Game Monitoring

### Starting Monitoring
1. Ensure a game pack is loaded
2. Launch your supported game
3. Click **Start** in the main control panel
4. GameWatcher will begin monitoring for dialogue

### Status Indicators
- **🟢 Green Dot** - Monitoring active
- **🔴 Red Dot** - Monitoring stopped
- **Game Field** - Shows detected game process
- **Pack Field** - Shows active pack name

### What GameWatcher Monitors
- **Game Windows** - Detects when supported games are running
- **Dialogue Boxes** - Identifies text areas in real-time
- **Text Changes** - Extracts new dialogue as it appears
- **Performance** - Tracks processing speed and accuracy

## 📊 Activity Monitoring

### Real-Time Activity Log
The Activity Monitor shows live system activity:
- **Timestamp** - When each event occurred
- **Event Type** - Frame processing, text detection, audio playback
- **Details** - Specific information about each event

### Performance Metrics

#### Processing Statistics
- **Frames Processed** - Total frames analyzed
- **Text Detections** - Dialogue instances found
- **Audio Played** - Voice clips triggered
- **Avg Processing** - Performance in milliseconds

#### System Resources
- **CPU Usage** - Current processor load
- **Memory** - RAM consumption
- **Last Activity** - Most recent system event

### Performance Expectations
GameWatcher V2 maintains excellent performance:
- **~2.3ms** average processing time
- **4.1x faster** than original implementation
- **79.3% reduced** search area through optimization
- **Real-time** dialogue detection with minimal lag

## ⚙️ Configuration Settings

### General Settings
- **Auto Start Monitoring** - Begin monitoring when games are detected
- **Detection Interval** - How often to check for new games (milliseconds)
- **Pack Directories** - Folders to search for game packs

### Capture Settings
- **Capture Rate** - Frame analysis frequency (FPS)
- **Enable Optimization** - Use performance enhancements
- **Optimization Threshold** - Similarity threshold for optimizations

### OCR Settings
- **Language** - Text recognition language
- **Confidence Threshold** - Minimum OCR accuracy required
- **Enable Preprocessing** - Image enhancement for better text recognition

### Audio Settings
- **Master Volume** - Overall audio level
- **Audio Device** - Primary output device
- **Enable Crossfade** - Smooth transitions between clips

## 🎯 Supported Games

### Current Game Packs

#### Final Fantasy I Pixel Remaster
- **Executable**: `FINAL FANTASY_F2P.exe`
- **Features**: 25+ speaker profiles, optimized detection
- **Performance**: 4.1x faster processing, 79.3% search reduction
- **Status**: Complete with all V1 optimizations preserved

### Adding New Games
New game packs can be added by:
1. Placing compatible pack folders in the `packs` directory
2. Using the **Refresh Packs** button to rescan
3. Compatible packs will appear in the Pack Manager

## 🔧 Troubleshooting

### Common Issues

#### "No game detected"
- Ensure the game is running and visible
- Check that a compatible pack is loaded
- Verify the game executable matches the pack specification

#### "No pack loaded"
- Use **Refresh Packs** to rescan directories
- Check that pack files are in the correct folder structure
- Ensure pack manifest files are valid JSON

#### High CPU/Memory usage
- Reduce **Capture Rate** in settings
- Enable **Optimization** features
- Close unnecessary applications

### Performance Optimization
For best results:
- Use **targeted detection** packs when available
- Enable **search area optimization**
- Set appropriate **confidence thresholds**
- Monitor system resources in the Activity Monitor

### Log Files
Application logs are stored in:
```
logs/gamewatcher-studio_YYYY-MM-DD.log
```

## 🎪 Advanced Features

### Hot-Swapping Packs
- Switch between games without restarting
- Automatic pack detection and loading
- No interruption to system operation

### Multi-Game Support
- Monitor multiple supported games
- Automatic switching based on active window
- Pack-specific optimizations maintained

### Real-Time Analytics
- Live performance monitoring
- Processing time tracking
- Resource usage analysis

## 📝 Best Practices

### For Optimal Performance
1. **Keep packs updated** - Use latest pack versions
2. **Monitor resources** - Check CPU/memory usage regularly
3. **Use optimization** - Enable performance features
4. **Regular maintenance** - Clear old log files periodically

### For Best User Experience
1. **Test pack compatibility** - Verify games work before streaming
2. **Configure audio levels** - Set appropriate volume levels
3. **Monitor activity** - Watch for text detection issues
4. **Update regularly** - Use latest GameWatcher versions

## 🆘 Getting Help

### Support Resources
- **Activity Monitor** - Real-time diagnostic information
- **Log Files** - Detailed application logs
- **Settings Export** - Share configuration for troubleshooting
- **Performance Metrics** - System health indicators

### Community
- Game Pack development community
- Performance optimization sharing
- Troubleshooting assistance
- Feature requests and feedback

---

**GameWatcher Studio v2.0** - Universal GUI for the GameWatcher V2 Platform
*Preserving all V1 performance optimizations while providing unlimited game support*