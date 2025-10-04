param(
  [string]$TesseractExe
)

if ($TesseractExe) {
  $env:TESSERACT_EXE = (Resolve-Path $TesseractExe)
}

Write-Host "Running OCR smoke..."
dotnet run --project src/GameWatcher.App

