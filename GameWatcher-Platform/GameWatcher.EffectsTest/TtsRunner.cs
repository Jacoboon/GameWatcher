using System;
using System.IO;
using System.Threading.Tasks;
using GameWatcher.Engine.Voices;

namespace GameWatcher.EffectsTest;

/// <summary>
/// Standalone test runner for TTS instructions - avoids Program.cs conflicts
/// </summary>
class TtsRunner
{
    static async Task Main()
    {
        Console.WriteLine("🎤 Testing OpenAI TTS with Instructions Parameter\n");

        var ttsService = new OpenAiTtsService();
        if (!ttsService.IsConfigured)
        {
            Console.WriteLine("❌ OpenAI API key not configured!");
            Console.WriteLine("   Expected: Secrets/openai-api-key.txt or GWS_OPENAI_API_KEY environment variable");
            return;
        }

        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "tts_instruction_tests");
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"📁 Output directory: {outputDir}\n");

        var testLine = "The kingdom is in grave danger.";
        var voice = "onyx"; // Deep voice for King

        // Test 1: No instructions (baseline)
        Console.WriteLine("📝 Test 1: Baseline (no instructions)");
        Console.WriteLine($"   Text: \"{testLine}\"");
        var path1 = Path.Combine(outputDir, "test1_baseline.wav");
        var success1 = await ttsService.GenerateWavAsync(testLine, voice, path1);
        if (success1)
            Console.WriteLine($"   ✅ Generated: {path1}\n");
        else
            Console.WriteLine($"   ❌ Failed!\n");

        // Test 2: Role-based instructions
        Console.WriteLine("📝 Test 2: Role-based (King)");
        var instructions2 = TtsInstructionsBuilder.Build(speakerRole: "King");
        Console.WriteLine($"   Instructions: \"{instructions2}\"");
        Console.WriteLine($"   Text: \"{testLine}\"");
        var path2 = Path.Combine(outputDir, "test2_king_role.wav");
        var success2 = await ttsService.GenerateWavAsync(testLine, voice, instructions2, path2);
        if (success2)
            Console.WriteLine($"   ✅ Generated: {path2}\n");
        else
            Console.WriteLine($"   ❌ Failed!\n");

        // Test 3: Role + Emotion
        Console.WriteLine("📝 Test 3: Role (King) + Emotion (worried)");
        var instructions3 = TtsInstructionsBuilder.Build(speakerRole: "King", emotion: "worried");
        Console.WriteLine($"   Instructions: \"{instructions3}\"");
        Console.WriteLine($"   Text: \"{testLine}\"");
        var path3 = Path.Combine(outputDir, "test3_king_worried.wav");
        var success3 = await ttsService.GenerateWavAsync(testLine, voice, instructions3, path3);
        if (success3)
            Console.WriteLine($"   ✅ Generated: {path3}\n");
        else
            Console.WriteLine($"   ❌ Failed!\n");

        // Test 4: Audition style (user's brilliant idea!)
        Console.WriteLine("📝 Test 4: 🎭 AUDITION STYLE (your brilliant idea!)");
        var instructions4 = TtsInstructionsBuilder.BuildAuditionStyle(
            speakerName: "King of Corneria", 
            speakerRole: "King", 
            text: testLine, 
            emotion: "worried"
        );
        Console.WriteLine($"   Instructions: \"{instructions4}\"");
        var path4 = Path.Combine(outputDir, "test4_audition_style.wav");
        var success4 = await ttsService.GenerateWavAsync(testLine, voice, instructions4, path4);
        if (success4)
            Console.WriteLine($"   ✅ Generated: {path4}\n");
        else
            Console.WriteLine($"   ❌ Failed!\n");

        // Test 5: With silence (as user tested)
        Console.WriteLine("📝 Test 5: With Silence at End");
        var instructions5 = TtsInstructionsBuilder.Build(speakerRole: "King", emotion: "worried");
        instructions5 = TtsInstructionsBuilder.WithPause(instructions5, secondsAtEnd: 1.0);
        Console.WriteLine($"   Instructions: \"{instructions5}\"");
        Console.WriteLine($"   Text: \"{testLine}\"");
        var path5 = Path.Combine(outputDir, "test5_with_silence.wav");
        var success5 = await ttsService.GenerateWavAsync(testLine, voice, instructions5, path5);
        if (success5)
            Console.WriteLine($"   ✅ Generated: {path5}\n");
        else
            Console.WriteLine($"   ❌ Failed!\n");

        // Test 6: Princess (different character)
        Console.WriteLine("📝 Test 6: Princess (grateful)");
        var princessLine = "Thank you for saving me!";
        var instructions6 = TtsInstructionsBuilder.Build(speakerRole: "Princess", emotion: "grateful");
        Console.WriteLine($"   Instructions: \"{instructions6}\"");
        Console.WriteLine($"   Text: \"{princessLine}\"");
        var path6 = Path.Combine(outputDir, "test6_princess_grateful.wav");
        var success6 = await ttsService.GenerateWavAsync(princessLine, "nova", instructions6, path6);
        if (success6)
            Console.WriteLine($"   ✅ Generated: {path6}\n");
        else
            Console.WriteLine($"   ❌ Failed!\n");

        // Test 7: Villain
        Console.WriteLine("📝 Test 7: Villain (Garland - angry)");
        var villainLine = "I, Garland, will knock you all down!";
        var instructions7 = TtsInstructionsBuilder.Build(speakerRole: "Garland", emotion: "angry");
        Console.WriteLine($"   Instructions: \"{instructions7}\"");
        Console.WriteLine($"   Text: \"{villainLine}\"");
        var path7 = Path.Combine(outputDir, "test7_garland_angry.wav");
        var success7 = await ttsService.GenerateWavAsync(villainLine, "onyx", instructions7, path7);
        if (success7)
            Console.WriteLine($"   ✅ Generated: {path7}\n");
        else
            Console.WriteLine($"   ❌ Failed!\n");

        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("🎬 All tests complete!");
        Console.WriteLine($"📁 Check output folder: {outputDir}");
        Console.WriteLine("\n🎧 Listen to the files and compare quality!");
        Console.WriteLine("   - test1: Baseline (should be flat)");
        Console.WriteLine("   - test2: Role-based (should have character)");
        Console.WriteLine("   - test3: Role + Emotion (more feeling)");
        Console.WriteLine("   - test4: AUDITION STYLE (most 'acted')");
        Console.WriteLine("   - test5: With pause at end");
        Console.WriteLine("   - test6: Princess grateful");
        Console.WriteLine("   - test7: Villain angry");
        Console.WriteLine("═══════════════════════════════════════");
    }
}
