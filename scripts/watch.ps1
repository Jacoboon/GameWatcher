param(
  [Parameter(Mandatory=$true)][string]$Title,
  [int]$Fps = 20,
  [string]$TesseractExe
)

if ($TesseractExe) { $env:TESSERACT_EXE = (Resolve-Path $TesseractExe) }

Write-Host "Watching window: $Title @ ${Fps}fps"
Write-Host "Press Q in the console window to quit."

dotnet run --project src/GameWatcher.App -- --watch "$Title" --fps $Fps

