using GameWatcher.Engine.Audio;
using GameWatcher.Engine.Audio.Effects;

namespace GameWatcher.EffectsTest;

/// <summary>
/// Quick test to verify effects metadata serialization round-trip works correctly.
/// Tests: Effect → Metadata → JSON → Metadata → Effect
/// </summary>
public static class SerializationTest
{
    public static void Run()
    {
        Console.WriteLine("=== Effects Metadata Serialization Test ===\n");

        try
        {
            // Test 1: Single effect serialization
            TestSingleEffect();

            // Test 2: Effect chain serialization
            TestEffectChain();

            // Test 3: Preset serialization
            TestPreset();

            // Test 4: DialogueAudioMetadata serialization
            TestDialogueMetadata();

            Console.WriteLine("\n✅ All serialization tests passed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void TestSingleEffect()
    {
        Console.WriteLine("Test 1: Single Effect Serialization");

        var original = new VolumeEffect();
        original.Parameters["gain_db"] = -6.0;
        
        var metadata = AudioEffectMetadata.FromEffect(original);
        var restored = metadata.ToEffect();

        if (restored is not VolumeEffect restoredVolume)
            throw new Exception("Restored effect is not VolumeEffect");

        var originalGain = original.Parameters["gain_db"];
        var restoredGain = restoredVolume.Parameters["gain_db"];

        if (Math.Abs((double)originalGain - (double)restoredGain) > 0.001)
            throw new Exception($"Gain mismatch: {originalGain} vs {restoredGain}");

        Console.WriteLine($"  ✓ VolumeEffect: {originalGain}dB → Metadata → {restoredGain}dB\n");
    }

    private static void TestEffectChain()
    {
        Console.WriteLine("Test 2: Effect Chain Serialization");

        var lowPass = new LowPassFilterEffect();
        lowPass.Parameters["cutoff_frequency"] = 1500.0;
        lowPass.Parameters["resonance"] = 0.7;

        var highPass = new HighPassFilterEffect();
        highPass.Parameters["cutoff_frequency"] = 300.0;
        highPass.Parameters["resonance"] = 0.5;

        var volume = new VolumeEffect();
        volume.Parameters["gain_db"] = -3.0;

        var original = new List<IAudioEffect> { lowPass, highPass, volume };

        var metadataList = original.Select(AudioEffectMetadata.FromEffect).ToList();
        var restored = metadataList.Select(m => m.ToEffect()).ToList();

        if (restored.Count != original.Count)
            throw new Exception($"Effect count mismatch: {original.Count} vs {restored.Count}");

        for (int i = 0; i < original.Count; i++)
        {
            if (original[i].Type != restored[i].Type)
                throw new Exception($"Effect type mismatch at index {i}: {original[i].Type} vs {restored[i].Type}");

            Console.WriteLine($"  ✓ Effect {i + 1}: {original[i].Type}");
        }

        Console.WriteLine();
    }

    private static void TestPreset()
    {
        Console.WriteLine("Test 3: Preset Serialization");

        var effects = AudioEffectsEngine.EffectFactory.CreateCaveEchoPreset();
        var preset = EffectPreset.FromEffects("Cave Echo", "Test preset", "Environmental", effects);

        var tempFile = Path.Combine(Path.GetTempPath(), "test_preset.json");
        preset.SaveToFile(tempFile);

        var loaded = EffectPreset.LoadFromFile(tempFile);
        File.Delete(tempFile);

        if (loaded.Name != preset.Name)
            throw new Exception($"Preset name mismatch: {preset.Name} vs {loaded.Name}");

        if (loaded.Effects.Count != preset.Effects.Count)
            throw new Exception($"Effect count mismatch: {preset.Effects.Count} vs {loaded.Effects.Count}");

        var restoredEffects = loaded.ToEffects();
        Console.WriteLine($"  ✓ Preset '{loaded.Name}': {restoredEffects.Count} effects");
        foreach (var effect in restoredEffects)
        {
            Console.WriteLine($"    - {effect.Type}");
        }

        Console.WriteLine();
    }

    private static void TestDialogueMetadata()
    {
        Console.WriteLine("Test 4: DialogueAudioMetadata Serialization");

        var volume = new VolumeEffect();
        volume.Parameters["gain_db"] = -6.0;

        var lowPass = new LowPassFilterEffect();
        lowPass.Parameters["cutoff_frequency"] = 1500.0;
        lowPass.Parameters["resonance"] = 0.7;

        var effects = new List<IAudioEffect> { volume, lowPass };

        var audioPath = Path.Combine(Path.GetTempPath(), "test_audio.mp3");
        var metadata = DialogueAudioMetadata.Create(audioPath, effects, "Underwater");

        metadata.SaveToFile(audioPath);
        var metadataPath = DialogueAudioMetadata.GetMetadataPath(audioPath);

        if (!File.Exists(metadataPath))
            throw new Exception($"Metadata file not created: {metadataPath}");

        var loaded = DialogueAudioMetadata.LoadFromFile(audioPath);
        File.Delete(metadataPath);

        if (loaded == null)
            throw new Exception("Failed to load metadata");

        if (loaded.PresetName != "Underwater")
            throw new Exception($"Preset name mismatch: Underwater vs {loaded.PresetName}");

        if (loaded.Effects.Count != 2)
            throw new Exception($"Effect count mismatch: 2 vs {loaded.Effects.Count}");

        Console.WriteLine($"  ✓ Metadata for '{loaded.AudioFile}'");
        Console.WriteLine($"    - Preset: {loaded.PresetName}");
        Console.WriteLine($"    - Effects: {loaded.Effects.Count}");
        foreach (var effect in loaded.Effects)
        {
            Console.WriteLine($"      - {effect.Type}");
        }

        Console.WriteLine();
    }
}
