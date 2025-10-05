# Fullscreen Capture Solution

## Overview

Your GameWatcher application now has a robust, multi-method capture system specifically designed to handle fullscreen games and applications. The new `CaptureService` automatically detects the best capture method and provides intelligent fallbacks.

## Architecture

### CaptureService (Unified Interface)
- **Location**: `src/GameWatcher.App/Capture/CaptureService.cs`
- **Purpose**: Intelligent capture method selection and fallback
- **Methods**: WGC → DXGI Desktop Duplication → Win32 PrintWindow

### Capture Methods by Priority

1. **Windows Graphics Capture (WGC)** - Modern Windows 10+ API
   - Best for: Windowed applications, UWP apps, modern games
   - Pros: Hardware accelerated, respects DPI, excellent quality
   - Cons: May fail with exclusive fullscreen or older games

2. **DXGI Desktop Duplication** - DirectX-based screen capture
   - Best for: Fullscreen games, exclusive fullscreen applications
   - Pros: Captures fullscreen content, hardware accelerated
   - Cons: Requires D3D11 support, monitor-level capture

3. **Win32 PrintWindow/BitBlt** - Traditional GDI capture
   - Best for: Legacy applications, fallback scenarios
   - Pros: Universal compatibility, always works
   - Cons: Slower, may miss hardware-accelerated content

## Fullscreen Detection

The service automatically detects fullscreen applications using:
- Window size vs. monitor resolution comparison (>90% coverage)
- Window position relative to monitor bounds
- Window style analysis

When fullscreen is detected, DXGI Desktop Duplication is prioritized over WGC.

## Key Features

### Intelligent Fallback
- Automatically tries next method if current one fails
- Remembers successful methods per window
- Avoids retrying failed methods for a cooldown period

### Performance Optimization  
- Method switching only occurs on failures or window changes
- Resource cleanup on method switching
- Minimal overhead when using stable method

### Debugging Support
- Console logging of method switches and failures
- `GetStatus()` method for runtime debugging
- Window title logging for troubleshooting

## Usage

### Simple Usage (Recommended)
```csharp
using var frame = CaptureService.CaptureWindow(hwnd);
if (frame != null)
{
    // Process your captured frame
    ProcessFrame(frame);
}
```

### Advanced Usage
```csharp
// Get current capture status for debugging
var status = CaptureService.GetStatus();
Console.WriteLine(status);

// Reset all capture resources (when switching windows)
CaptureService.Reset();
```

## Environment Variables (Optional Tuning)

### DXGI Desktop Duplication Settings
- `GW_DD_FORCE_MONITOR=1` - Always capture full monitor instead of window crop
- `GW_DD_MINCROP_W=400` - Minimum crop width (default: 400)
- `GW_DD_MINCROP_H=300` - Minimum crop height (default: 300)

### General Settings
- `GW_STABILITY=3` - Frame stability count before OCR (default: 2)

## Testing Your Setup

1. **Test with Windowed Application**:
   - Should use WGC (fastest, best quality)
   - Log: `[CAPTURE] WGC SUCCESS for window: [AppName]`

2. **Test with Fullscreen Game**:
   - Should detect fullscreen and use DXGI
   - Log: `[CAPTURE] DXGI SUCCESS for window: [GameName]`

3. **Test Method Fallback**:
   - If primary method fails, should automatically try others
   - Log: `[CAPTURE] WGC FAILED for window: [AppName]`
   - Log: `[CAPTURE] DXGI SUCCESS for window: [AppName]`

## Troubleshooting Fullscreen Issues

### Game Shows Black Screen
- **Cause**: Game using exclusive fullscreen mode
- **Solution**: DXGI Desktop Duplication should handle this automatically
- **Verify**: Check logs for `DXGI SUCCESS` message

### Performance Issues
- **Cause**: Frequent method switching
- **Solution**: Check `GetStatus()` for failure count
- **Fix**: Ensure stable window handle, check for game alt-tabbing

### Still Not Working?
- **Try**: Set `GW_DD_FORCE_MONITOR=1` to capture full monitor
- **Fallback**: Win32 method will always work as last resort
- **Debug**: Check console logs for specific failure reasons

## Integration Notes

The capture service is now integrated into:
- **GUI Application**: `MainWindow.xaml.cs` uses `CaptureService.CaptureWindow()`
- **CLI Application**: `Program.cs` uses `CaptureService.CaptureWindow()`
- **Existing Code**: Drop-in replacement for `Win32Capture.CaptureClient()`

No changes needed to your OCR, detection, or audio pipeline - they continue to work with the returned `Bitmap` objects as before.

## Future Enhancements

The architecture supports easy addition of new capture methods:
- **DirectML/AI Enhancement** - For improving low-quality captures
- **Hardware-Specific APIs** - NVIDIA NVFBC, AMD RAPIDcapture
- **Cloud Gaming** - Special handling for streaming services
- **Mobile/Console** - External device capture support

Your fullscreen capture issues should now be resolved! The system will automatically choose the best method for each application type.