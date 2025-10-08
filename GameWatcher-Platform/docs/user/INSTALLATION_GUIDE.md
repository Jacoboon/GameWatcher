# GameWatcher V2 Platform - Installation Guide

This guide will help you install, configure, and get started with the GameWatcher V2 Platform on Windows systems.

## 📋 System Requirements

### Minimum Requirements
- **Operating System**: Windows 10 version 1903 (Build 18362) or later
- **Architecture**: x64 (64-bit)
- **RAM**: 4 GB minimum, 8 GB recommended
- **Storage**: 500 MB free space (plus game pack storage)
- **.NET Runtime**: .NET 8.0 (automatically included in installer)

### Recommended Requirements
- **Operating System**: Windows 11 (latest updates)
- **RAM**: 16 GB for optimal performance
- **CPU**: Multi-core processor (4+ cores recommended)
- **Storage**: SSD for better I/O performance
- **Graphics**: DirectX 11 compatible (for capture optimizations)

### Supported Games
Currently supported via game packs:
- **Final Fantasy I Pixel Remaster** (Complete pack included)
- Additional games supported through community packs

## 💾 Installation Methods

### Method 1: Standalone Installer (Recommended)
1. Download `GameWatcher-V2-Setup.exe` from releases
2. Run the installer as Administrator
3. Follow the installation wizard
4. Launch GameWatcher Studio (Player) or GameWatcher Author Studio (Creator) from Start Menu or Desktop

### Method 2: Portable Distribution
1. Download `GameWatcher-V2-Portable.zip` 
2. Extract to your desired installation directory
3. Run `GameWatcher.Studio.exe` (player) or `GameWatcher.AuthorStudio.exe` (creator)

### Method 3: Build from Source
```bash
# Clone the repository
git clone https://github.com/your-repo/GameWatcher.git
cd GameWatcher/GameWatcher-Platform

# Build the solution
dotnet build GameWatcher-Platform.sln --configuration Release

# Run the player
cd GameWatcher.Studio/bin/Release/net8.0-windows
./GameWatcher.Studio.exe

# (Optional) Run the authoring tools
cd ../../..
cd GameWatcher.AuthorStudio/bin/Release/net8.0-windows
./GameWatcher.AuthorStudio.exe
```

## 🔧 Initial Configuration

### First Launch Setup

1. **Launch GameWatcher Studio (Player)**
   - The application will create necessary directories
   - Default configuration files will be generated
   - Available game packs will be scanned automatically

2. **Verify Installation**
   - Check that the main interface loads without errors
   - Confirm pack directories are detected
   - Ensure logging is working (check `logs/` folder)

### Directory Structure
After installation, your GameWatcher directory will contain:
```
GameWatcher/
├── GameWatcher.Studio.exe          # Player application
├── GameWatcher.AuthorStudio.exe    # Authoring application
├── GameWatcher.Engine.dll          # Core engine
├── GameWatcher.Runtime.dll         # Runtime services
├── appsettings.json                # Main configuration
├── logs/                           # Application logs
├── packs/                          # Game pack directory
│   └── FF1.PixelRemaster/         # Included FF1 pack
└── temp/                          # Temporary files
```

## ⚙️ Configuration

### Basic Configuration (appsettings.json)
```json
{
  "GameWatcher": {
    "AutoStart": true,
    "DetectionIntervalMs": 2000,
    "PackDirectories": [
      "packs",
      "../FF1.PixelRemaster"
    ]
  },
  "Capture": {
    "TargetFps": 10,
    "EnableOptimization": true,
    "OptimizationThreshold": 0.85
  },
  "OCR": {
    "Language": "en-US",
    "ConfidenceThreshold": 0.7,
    "EnablePreprocessing": true
  },
  "Audio": {
    "MasterVolume": 80,
    "OutputDevice": "Default",
    "EnableCrossfade": true
  }
}
```

### Pack Directory Configuration
GameWatcher Studio scans these locations for game packs:
- `./packs/` - Local packs directory
- User-specified directories in configuration
- Registry-defined pack locations (if using installer)

### Performance Tuning
For optimal performance:

#### High-Performance Configuration
```json
{
  "Capture": {
    "TargetFps": 15,
    "EnableOptimization": true,
    "OptimizationThreshold": 0.90
  },
  "OCR": {
    "ConfidenceThreshold": 0.8,
    "EnablePreprocessing": true
  }
}
```

#### Low-Resource Configuration  
```json
{
  "Capture": {
    "TargetFps": 5,
    "EnableOptimization": true,
    "OptimizationThreshold": 0.75
  },
  "GameWatcher": {
    "DetectionIntervalMs": 5000
  }
}
```

## 🎮 Installing Game Packs

### Automatic Installation
1. Place pack folders in the `packs/` directory
2. Use **Refresh Packs** button in Pack Manager
3. Compatible packs will appear automatically

### Manual Pack Installation
1. Download pack from source (`.zip` or `.pack` file)
2. Extract to `packs/[PackName]/`
3. Ensure pack contains required files:
   - `pack-manifest.json`
   - Pack assembly (`.dll`)
   - Speaker profiles and audio files

### Pack Structure Verification
Verify your pack installation:
```
packs/YourGame.Pack/
├── pack-manifest.json        ✓ Required
├── YourGame.Pack.dll        ✓ Required  
├── speakers/
│   ├── speaker-catalog.json  ✓ Required
│   └── profiles/            ✓ Audio files
└── templates/               ⚬ Optional
    └── textbox-templates/   ⚬ Detection aids
```

## 🔍 Verification & Testing

### Installation Verification
1. **Launch Test**:
   - Start GameWatcher Studio
   - Verify no error dialogs appear
   - Check main interface loads completely

2. **Pack Detection Test**:
   - Navigate to Pack Manager tab
   - Verify included FF1 pack appears
   - Try loading/unloading the pack

3. **System Integration Test**:
   - Launch a supported game
   - Start monitoring in GameWatcher
   - Verify game detection works

### Performance Verification
Check that V2 maintains V1 performance benefits:
- **Processing Time**: Should average ~2.3ms per frame
- **Detection Speed**: 4.1x faster than baseline
- **Search Efficiency**: 79.3% area reduction when optimized

Monitor these in the Activity Monitor tab during operation.

## 🛠️ Troubleshooting Installation

### Common Installation Issues

#### "Application failed to start"
**Cause**: Missing .NET 8.0 Runtime
**Solution**: 
1. Download .NET 8.0 Desktop Runtime from Microsoft
2. Install and restart system
3. Retry GameWatcher launch

#### "No packs detected"
**Cause**: Pack directory configuration issue
**Solution**:
1. Verify `packs/` folder exists in installation directory
2. Check `appsettings.json` PackDirectories configuration
3. Ensure FF1.PixelRemaster pack is present
4. Use **Refresh Packs** button

#### "Access denied" errors
**Cause**: Insufficient permissions
**Solution**:
1. Run GameWatcher as Administrator (first launch)
2. Ensure installation directory has write permissions
3. Check antivirus software isn't blocking files

#### High CPU/Memory usage
**Cause**: Performance configuration issues
**Solution**:
1. Reduce `TargetFps` in Capture settings
2. Increase `DetectionIntervalMs` 
3. Disable unnecessary optimization features
4. Monitor system resources in Activity Monitor

### Log Analysis
Check application logs for diagnostic information:
```
logs/gamewatcher-studio_YYYY-MM-DD.log
```

Common log indicators:
- **"Pack loaded successfully"** - Pack installation working
- **"Game detected"** - Game integration working  
- **"Processing frame in X.Xms"** - Performance metrics
- **"Error"/"Exception"** - Issues requiring attention

### Registry Cleanup (If Needed)
If using the installer and need to clean up:
```batch
# Remove GameWatcher registry entries (run as Administrator)
reg delete "HKEY_CURRENT_USER\Software\GameWatcher" /f
reg delete "HKEY_LOCAL_MACHINE\SOFTWARE\GameWatcher" /f
```

## 📊 Performance Optimization

### Post-Installation Optimization

1. **Baseline Performance Test**:
   - Run Activity Monitor for 5 minutes during gameplay
   - Note average processing times and resource usage
   - Adjust settings if performance is below targets

2. **Game-Specific Tuning**:
   - Different games may benefit from different settings
   - Monitor detection success rates
   - Adjust confidence thresholds as needed

3. **System Resource Management**:
   - Close unnecessary background applications
   - Ensure adequate free RAM (4GB+ available)
   - Consider SSD installation for I/O intensive operations

### Windows Optimization
For best GameWatcher performance:
- **Disable Windows Game Mode** (can interfere with capture)
- **Set High Performance power plan**
- **Close Windows Game Bar** (Windows + G)
- **Disable Windows Game DVR**

## 🚀 Next Steps

### After Successful Installation
1. **Complete User Guide**: Read the full User Guide for operational details
2. **Configure Settings**: Customize settings for your system and preferences
3. **Test with Games**: Verify functionality with your supported games
4. **Monitor Performance**: Use Activity Monitor to ensure optimal operation

### Expanding Functionality
- **Install Additional Packs**: Add support for more games
- **Join Community**: Connect with other users for tips and troubleshooting
- **Contribute Feedback**: Report issues and suggest improvements
- **Develop Packs**: Create packs for unsupported games (see Developer Guide)

---

**GameWatcher V2 Platform Installation Guide**
*Complete setup instructions for the universal voiceover platform*

**Support**: For installation issues, check the troubleshooting section or consult application logs for detailed error information.
