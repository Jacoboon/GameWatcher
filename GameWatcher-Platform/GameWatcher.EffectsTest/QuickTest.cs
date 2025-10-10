using Microsoft.Extensions.Logging;
using GameWatcher.Engine.Audio;
using GameWatcher.Engine.Audio.Effects;

// Quick test of new Reverb and Echo effects
Console.WriteLine("🎵 Testing Reverb and Echo Effects\n");

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<AudioEffectsEngine>();
var engine = new AudioEffectsEngine(logger);

// Find test audio
var testPath = "../../../../voices/previews/fable_1.2.mp3";
if (!File.Exists(testPath))
{
    testPath = "../../../voices/previews/fable_1.2.mp3";
}
if (!File.Exists(testPath))
{
    Console.WriteLine("❌ Test audio not found!");
    return;
}

Console.WriteLine($"✅ Using: {Path.GetFileName(testPath)}\n");

// Test Cave Echo preset
Console.WriteLine("Playing CAVE ECHO preset...");
Console.WriteLine("(Reverb + Echo combined)\n");

var caveEcho = AudioEffectsEngine.EffectFactory.CreateCaveEchoPreset();
await engine.PlayWithEffects(testPath, caveEcho);

Console.WriteLine("\n✅ Cave Echo test complete!");
Console.WriteLine("Effects are working! 🎉");
