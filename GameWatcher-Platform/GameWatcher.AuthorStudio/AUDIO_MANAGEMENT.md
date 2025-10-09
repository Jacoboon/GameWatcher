# Audio File Management - Design Notes

## Problem Statement

Currently, audio files can exist in multiple locations:
- User-supplied audio (imported from anywhere)
- TTS-generated audio (in temp folders or voices/)
- Pack's Audio folder (final export location)

**Risks:**
- Orphaned audio files (audio exists but no dialogue references it)
- Mismatched references (dialogue points to non-existent file)
- Duplicate files (same audio copied/moved multiple times)
- Broken paths after pack export (relative vs absolute paths)

## Proposed Solution: Audio Store with Dialogue-Audio Dictionary

### Core Concept

A **singleton AudioStore** service that maintains:
```json
{
  "dialogue-id-001": "Audio/line_001.mp3",
  "dialogue-id-002": "Audio/line_002.mp3",
  "dialogue-id-003": "Audio/line_003.mp3"
}
```

### Key Responsibilities

1. **Assign Unique IDs to Dialogue Entries**
   - Generate stable IDs (hash of normalized text, or sequential)
   - `dialogue.Id = AudioStore.GenerateId(dialogue.Text)`

2. **Manage Audio File Lifecycle**
   - **Import:** Copy user file → Pack's Audio folder, assign ID
   - **Generate:** Move TTS output → Pack's Audio folder, assign ID
   - **Delete:** Remove file when dialogue is deleted
   - **Update:** Handle re-generation (replace file, keep ID)

3. **Maintain Single Source of Truth**
   - Dictionary: `<Dialogue-ID, Audio-Filepath>`
   - All lookups go through AudioStore
   - Serialize to `audio-manifest.json` in pack

### Implementation Sketch

```csharp
public class AudioStore
{
    private readonly Dictionary<string, string> _audioMap = new();
    private readonly string _packFolder;
    
    public AudioStore(string packFolder)
    {
        _packFolder = packFolder;
        LoadManifest();
    }
    
    /// <summary>
    /// Registers audio for a dialogue entry. Copies/moves file to pack's Audio folder.
    /// </summary>
    public async Task<string> SetAudioAsync(PendingDialogueEntry dialogue, string sourcePath, bool isGenerated)
    {
        var dialogueId = GenerateId(dialogue.Text);
        var audioFolder = Path.Combine(_packFolder, "Audio");
        Directory.CreateDirectory(audioFolder);
        
        var ext = Path.GetExtension(sourcePath);
        var destFile = Path.Combine(audioFolder, $"{dialogueId}{ext}");
        
        if (isGenerated)
        {
            // Move TTS-generated files (they're temp)
            File.Move(sourcePath, destFile, overwrite: true);
        }
        else
        {
            // Copy user-imported files (preserve original)
            File.Copy(sourcePath, destFile, overwrite: true);
        }
        
        var relativePath = Path.Combine("Audio", Path.GetFileName(destFile)).Replace('\\', '/');
        _audioMap[dialogueId] = relativePath;
        
        await SaveManifestAsync();
        
        return relativePath;
    }
    
    /// <summary>
    /// Gets audio path for a dialogue entry (if it exists).
    /// </summary>
    public string? GetAudio(PendingDialogueEntry dialogue)
    {
        var id = GenerateId(dialogue.Text);
        return _audioMap.TryGetValue(id, out var path) ? path : null;
    }
    
    /// <summary>
    /// Removes audio for a dialogue entry.
    /// </summary>
    public async Task RemoveAudioAsync(PendingDialogueEntry dialogue)
    {
        var id = GenerateId(dialogue.Text);
        if (_audioMap.TryGetValue(id, out var relativePath))
        {
            var fullPath = Path.Combine(_packFolder, relativePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            
            _audioMap.Remove(id);
            await SaveManifestAsync();
        }
    }
    
    /// <summary>
    /// Generates a stable ID for dialogue text.
    /// </summary>
    private string GenerateId(string text)
    {
        var normalized = TextNormalizer.Normalize(text);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalized));
        return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
    }
    
    private void LoadManifest()
    {
        var manifestPath = Path.Combine(_packFolder, "Configuration", "audio-manifest.json");
        if (File.Exists(manifestPath))
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (manifest != null)
            {
                foreach (var kvp in manifest)
                {
                    _audioMap[kvp.Key] = kvp.Value;
                }
            }
        }
    }
    
    private async Task SaveManifestAsync()
    {
        var configDir = Path.Combine(_packFolder, "Configuration");
        Directory.CreateDirectory(configDir);
        
        var manifestPath = Path.Combine(configDir, "audio-manifest.json");
        var json = JsonSerializer.Serialize(_audioMap, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json);
    }
}
```

### Workflow Integration

#### TTS Generation
```csharp
// Old way (risky):
var audioPath = await TtsService.GenerateAsync(dialogue.Text, speaker.Voice);
dialogue.AudioPath = audioPath;  // Might be temp path!

// New way (managed):
var tempPath = await TtsService.GenerateAsync(dialogue.Text, speaker.Voice);
var managedPath = await AudioStore.SetAudioAsync(dialogue, tempPath, isGenerated: true);
dialogue.AudioPath = managedPath;  // Always relative to pack
```

#### User Import
```csharp
// Old way (risky):
dialogue.AudioPath = userSelectedFile;  // Might be absolute path outside pack!

// New way (managed):
var managedPath = await AudioStore.SetAudioAsync(dialogue, userSelectedFile, isGenerated: false);
dialogue.AudioPath = managedPath;  // Copied into pack's Audio folder
```

#### Discovery Playback
```csharp
// Old way (risky):
if (File.Exists(dialogue.AudioPath)) 
    AudioPlayer.Play(dialogue.AudioPath);

// New way (managed):
var audioPath = AudioStore.GetAudio(dialogue);
if (audioPath != null)
{
    var fullPath = Path.Combine(packFolder, audioPath);
    AudioPlayer.Play(fullPath);
}
```

### Benefits

✅ **No Orphans:** Files only exist if referenced in manifest  
✅ **No Mismatches:** Manifest is single source of truth  
✅ **Portable Packs:** All paths relative to pack folder  
✅ **Clean Deletion:** Delete dialogue → delete audio automatically  
✅ **Deduplication:** ID-based naming prevents duplicates  
✅ **Auditability:** Manifest file shows all audio associations  

### Manifest Example

```json
{
  "a1b2c3d4e5f6g7h8": "Audio/line_001.mp3",
  "9i8j7k6l5m4n3o2p": "Audio/line_002.mp3",
  "q1r2s3t4u5v6w7x8": "Audio/line_003.wav"
}
```

### Future Enhancements

- **Versioning:** Track audio file version/timestamp
- **Metadata:** Store speaker, voice, generation params
- **Cleanup Tool:** Find orphaned files not in manifest
- **Validation:** Verify all manifest entries have files
- **Migration:** Convert old packs to new manifest format

## Implementation Priority

**Phase 1 (MVP):**
- Basic AudioStore with SetAudio/GetAudio/RemoveAudio
- Integrate with TTS generation
- Integrate with user import

**Phase 2 (Polish):**
- Auto-cleanup orphaned files
- Validation on pack load
- Migration tool for existing packs

**Phase 3 (Advanced):**
- Metadata tracking
- Versioning support
- Deduplication detection

---

**Status:** Design document - not yet implemented  
**Related Files:** PackExporter.cs (currently handles audio copying ad-hoc)  
**Next Steps:** Implement AudioStore service and integrate with TTS/import workflows
