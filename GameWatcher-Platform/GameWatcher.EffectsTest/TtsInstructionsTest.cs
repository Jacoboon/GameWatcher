using System;
using System.IO;
using System.Threading.Tasks;
using GameWatcher.Engine.Voices;

namespace GameWatcher.EffectsTest
{
    /// <summary>
    /// Quick test of the new TTS instructions feature with gpt-4o-mini-tts.
    /// </summary>
    public static class TtsInstructionsTest
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("🎤 Testing OpenAI TTS with Instructions Parameter\n");

            var ttsService = new OpenAiTtsService();
            if (!ttsService.IsConfigured)
            {
                Console.WriteLine("❌ OpenAI API key not configured!");
                return;
            }

            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "tts_instruction_tests");
            Directory.CreateDirectory(outputDir);

            var testLine = "The kingdom is in grave danger.";
            var voice = "onyx"; // Deep voice for King

            // Test 1: No instructions (baseline)
            Console.WriteLine("📝 Test 1: Baseline (no instructions)");
            Console.WriteLine($"   Text: \"{testLine}\"");
            var path1 = Path.Combine(outputDir, "test1_baseline.wav");
            await ttsService.GenerateWavAsync(testLine, voice, path1);
            Console.WriteLine($"   ✅ Generated: {path1}\n");

            // Test 2: Role-based instructions
            Console.WriteLine("📝 Test 2: Role-based (King)");
            var instructions2 = TtsInstructionsBuilder.Build(speakerRole: "King");
            Console.WriteLine($"   Instructions: \"{instructions2}\"");
            Console.WriteLine($"   Text: \"{testLine}\"");
            var path2 = Path.Combine(outputDir, "test2_king_role.wav");
            await ttsService.GenerateWavAsync(testLine, voice, instructions2, path2);
            Console.WriteLine($"   ✅ Generated: {path2}\n");

            // Test 3: Role + Emotion
            Console.WriteLine("📝 Test 3: Role (King) + Emotion (worried)");
            var instructions3 = TtsInstructionsBuilder.Build(speakerRole: "King", emotion: "worried");
            Console.WriteLine($"   Instructions: \"{instructions3}\"");
            Console.WriteLine($"   Text: \"{testLine}\"");
            var path3 = Path.Combine(outputDir, "test3_king_worried.wav");
            await ttsService.GenerateWavAsync(testLine, voice, instructions3, path3);
            Console.WriteLine($"   ✅ Generated: {path3}\n");

            // Test 4: Audition style (your original idea!)
            Console.WriteLine("📝 Test 4: Audition Style (your brilliant idea!)");
            var instructions4 = TtsInstructionsBuilder.BuildAuditionStyle(
                speakerName: "King of Corneria", 
                speakerRole: "King", 
                text: testLine, 
                emotion: "worried"
            );
            Console.WriteLine($"   Instructions: \"{instructions4}\"");
            var path4 = Path.Combine(outputDir, "test4_audition_style.wav");
            await ttsService.GenerateWavAsync(testLine, voice, instructions4, path4);
            Console.WriteLine($"   ✅ Generated: {path4}\n");

            // Test 5: With silence (as you tested)
            Console.WriteLine("📝 Test 5: With Silence");
            var instructions5 = TtsInstructionsBuilder.Build(speakerRole: "King", emotion: "worried");
            instructions5 = TtsInstructionsBuilder.WithPause(instructions5, secondsAtEnd: 1.0);
            Console.WriteLine($"   Instructions: \"{instructions5}\"");
            Console.WriteLine($"   Text: \"{testLine}\"");
            var path5 = Path.Combine(outputDir, "test5_with_silence.wav");
            await ttsService.GenerateWavAsync(testLine, voice, instructions5, path5);
            Console.WriteLine($"   ✅ Generated: {path5}\n");

            // Test 6: Princess (different character)
            Console.WriteLine("📝 Test 6: Princess (grateful + excited)");
            var princessLine = "Thank you for saving me!";
            var instructions6 = TtsInstructionsBuilder.Build(speakerRole: "Princess", emotion: "grateful");
            Console.WriteLine($"   Instructions: \"{instructions6}\"");
            Console.WriteLine($"   Text: \"{princessLine}\"");
            var path6 = Path.Combine(outputDir, "test6_princess_grateful.wav");
            await ttsService.GenerateWavAsync(princessLine, "nova", instructions6, path6);
            Console.WriteLine($"   ✅ Generated: {path6}\n");

            // Test 7: Villain
            Console.WriteLine("📝 Test 7: Villain (Garland)");
            var villainLine = "I, Garland, will knock you all down!";
            var instructions7 = TtsInstructionsBuilder.Build(speakerRole: "Garland", emotion: "angry");
            Console.WriteLine($"   Instructions: \"{instructions7}\"");
            Console.WriteLine($"   Text: \"{villainLine}\"");
            var path7 = Path.Combine(outputDir, "test7_garland_angry.wav");
            await ttsService.GenerateWavAsync(villainLine, "onyx", instructions7, path7);
            Console.WriteLine($"   ✅ Generated: {path7}\n");

            Console.WriteLine("🎬 All tests complete!");
            Console.WriteLine($"📁 Check output folder: {outputDir}");
            Console.WriteLine("\n🎧 Listen to the files and compare quality!");
            Console.WriteLine("   - Baseline should be flat");
            Console.WriteLine("   - Role-based should have character");
            Console.WriteLine("   - Emotion should add feeling");
            Console.WriteLine("   - Audition style should be the most 'acted'");
        }
    }
}
