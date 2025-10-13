using Microsoft.Extensions.Logging;
using GameWatcher.Engine.Audio;
using GameWatcher.Engine.Audio.Effects;

namespace GameWatcher.EffectsTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🎵 GameWatcher Audio Effects Test\n");

        // Check if TTS instructions test was requested
        if (args.Length > 0 && args[0] == "--tts-instructions")
        {
            await TtsInstructionsTest.RunAsync();
            return;
        }

        // Check if serialization test was requested
        if (args.Length > 0 && args[0] == "--serialization")
        {
            SerializationTest.Run();
            return;
        }

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<AudioEffectsEngine>();
        var engine = new AudioEffectsEngine(logger);

        // Find a test audio file
        var testAudioPath = FindTestAudio();
        if (testAudioPath == null)
        {
            Console.WriteLine("❌ No test audio file found!");
            Console.WriteLine("   Expected: voices/previews/*.mp3");
            return;
        }

        Console.WriteLine($"✅ Using test audio: {Path.GetFileName(testAudioPath)}\n");

        // Test 1: Play original
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Test 1: Original (No Effects)");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        await engine.PlayWithEffects(testAudioPath, new List<IAudioEffect>());

        // Test 2: Volume adjustment
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 2: Volume -6dB (Half Volume)");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var volumeTest = new List<IAudioEffect>
        {
            AudioEffectsEngine.EffectFactory.CreateVolume(-6.0)
        };
        await engine.PlayWithEffects(testAudioPath, volumeTest);

        // Test 3: Low-pass filter (muffled/underwater)
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 3: Low-Pass Filter (Underwater Sound)");
        Console.WriteLine("Cutoff: 1500Hz, Resonance: 0.7");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var underwaterTest = AudioEffectsEngine.EffectFactory.CreateUnderwaterPreset();
        await engine.PlayWithEffects(testAudioPath, underwaterTest);

        // Test 4: High-pass filter (telephone)
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 4: Telephone Effect");
        Console.WriteLine("High-Pass 300Hz + Low-Pass 3000Hz");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var telephoneTest = AudioEffectsEngine.EffectFactory.CreateTelephonePreset();
        await engine.PlayWithEffects(testAudioPath, telephoneTest);

        // Test 5: Whisper effect
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 5: Whisper Effect");
        Console.WriteLine("High-Pass 200Hz + Volume -8dB");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var whisperTest = AudioEffectsEngine.EffectFactory.CreateWhisperPreset();
        await engine.PlayWithEffects(testAudioPath, whisperTest);

        // Test 6: Custom chain
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 6: Custom Effect Chain");
        Console.WriteLine("Low-Pass 2000Hz + High-Pass 400Hz + Volume -3dB");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var customTest = new List<IAudioEffect>
        {
            AudioEffectsEngine.EffectFactory.CreateLowPassFilter(2000f, 0.6f),
            AudioEffectsEngine.EffectFactory.CreateHighPassFilter(400f, 0.4f),
            AudioEffectsEngine.EffectFactory.CreateVolume(-3.0)
        };
        await engine.PlayWithEffects(testAudioPath, customTest);

        // Test 7: Disabled effect (should skip)
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 7: Disabled Effect Test");
        Console.WriteLine("Volume -10dB (DISABLED) + Low-Pass 3000Hz");
        Console.WriteLine("(Should only hear low-pass filter)");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var disabledTest = new List<IAudioEffect>
        {
            new VolumeEffect
            {
                Enabled = false,
                Parameters = new Dictionary<string, object> { ["gain_db"] = -10.0 }
            },
            AudioEffectsEngine.EffectFactory.CreateLowPassFilter(3000f, 0.5f)
        };
        await engine.PlayWithEffects(testAudioPath, disabledTest);

        // Test 8: Echo effect
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 8: Echo Effect");
        Console.WriteLine("Delay: 400ms, Decay: 0.6, Wet: 0.4");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var echoTest = new List<IAudioEffect>
        {
            AudioEffectsEngine.EffectFactory.CreateEcho(400f, 0.6f, 0.4f)
        };
        await engine.PlayWithEffects(testAudioPath, echoTest);

        // Test 9: Reverb effect
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 9: Reverb Effect (Large Room)");
        Console.WriteLine("Room: 0.8, Damping: 0.5, Wet: 0.5, Dry: 0.6");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var reverbTest = new List<IAudioEffect>
        {
            AudioEffectsEngine.EffectFactory.CreateReverb(0.8f, 0.5f, 0.5f, 0.6f)
        };
        await engine.PlayWithEffects(testAudioPath, reverbTest);

        // Test 10: Cave Echo preset (Reverb + Echo combined!)
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 10: 🏔️ CAVE ECHO PRESET 🏔️");
        Console.WriteLine("Massive reverb + long echo tail");
        Console.WriteLine("This is the full environmental effect!");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var caveEchoPreset = AudioEffectsEngine.EffectFactory.CreateCaveEchoPreset();
        await engine.PlayWithEffects(testAudioPath, caveEchoPreset);

        // Test 11: Cathedral preset
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 11: ⛪ Cathedral Preset");
        Console.WriteLine("Huge space with long reverb tail");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var cathedralPreset = AudioEffectsEngine.EffectFactory.CreateCathedralPreset();
        await engine.PlayWithEffects(testAudioPath, cathedralPreset);

        // Test 12: Small Room preset
        Console.WriteLine("\n═══════════════════════════════════════");
        Console.WriteLine("Test 12: 🏠 Small Room Preset");
        Console.WriteLine("Intimate space with quick reflections");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("Press Enter to play...");
        Console.ReadLine();

        var smallRoomPreset = AudioEffectsEngine.EffectFactory.CreateSmallRoomPreset();
        await engine.PlayWithEffects(testAudioPath, smallRoomPreset);

        Console.WriteLine("\n✅ All tests complete!");
        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
    }

    static string? FindTestAudio()
    {
        // Look for test audio in common locations
        var searchPaths = new[]
        {
            "voices/previews/fable_1.2.mp3",
            "voices/previews/shimmer_0.8.mp3",
            "../voices/previews/fable_1.2.mp3",
            "../../voices/previews/fable_1.2.mp3",
            "../../../voices/previews/fable_1.2.mp3",
            "../../../../voices/previews/fable_1.2.mp3"
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }
}
