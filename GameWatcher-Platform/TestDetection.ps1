# Quick test script to verify textbox detection is working
# This will take a screenshot and test the detection logic

Write-Host "Testing GameWatcher textbox detection..." -ForegroundColor Green

# Build the test project
Write-Host "Building Engine..." -ForegroundColor Yellow
dotnet build GameWatcher.Engine/GameWatcher.Engine.csproj

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful" -ForegroundColor Green
Write-Host "🔍 AuthorStudio should now use the static fallback area when no textbox borders are detected" -ForegroundColor Cyan
Write-Host "📊 Discovery service will log frame info every 100 frames" -ForegroundColor Cyan
Write-Host ""
Write-Host "To test:" -ForegroundColor White
Write-Host "1. Open AuthorStudio" -ForegroundColor Gray
Write-Host "2. Go to Discovery tab" -ForegroundColor Gray  
Write-Host "3. Click 'Start Discovery'" -ForegroundColor Gray
Write-Host "4. Watch the Activity Log for frame info and textbox detection" -ForegroundColor Gray
Write-Host ""
Write-Host "Expected behavior:" -ForegroundColor White
Write-Host "• Frame info logged every ~7 seconds" -ForegroundColor Gray
Write-Host "• If no FF1 textbox found, should use static fallback area" -ForegroundColor Gray
Write-Host "• OCR should run on the fallback area" -ForegroundColor Gray