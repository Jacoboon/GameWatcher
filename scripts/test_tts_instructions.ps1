# Test TTS Instructions Feature
# This script runs the TTS instructions test to generate audio samples
Write-Host "🎤 Building TTS Instructions Test..." -ForegroundColor Cyan

cd "C:\Code Projects\GameWatcher\GameWatcher-Platform\GameWatcher.EffectsTest"

# Build with TtsRunner as entry point
dotnet build

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`n🚀 Running TTS test with instructions..." -ForegroundColor Green
Write-Host "This will generate 7 audio samples comparing different instruction styles`n" -ForegroundColor Yellow

# Run using the TtsRunner class
dotnet run --project GameWatcher.EffectsTest.csproj --no-build -- --run-tts-runner

Write-Host "`n✅ Done! Check the tts_instruction_tests folder for output files." -ForegroundColor Green
