param(
  [Parameter(Mandatory=$true)][string]$Title,
  [int]$Fps = 20,
  [int]$IntervalSec = 20,
  [int]$MaxPerPass = 10,
  [switch]$DryRun,
  [switch]$Overwrite,
  [string]$TesseractExe
)

if ($TesseractExe) { $env:TESSERACT_EXE = (Resolve-Path $TesseractExe) }

Write-Host "Starting live authoring loop..." -ForegroundColor Cyan
Write-Host "- Watch window: $Title @ ${Fps}fps"
Write-Host "- Poll interval: $IntervalSec sec, max/pass: $MaxPerPass"
if ($DryRun) { Write-Host "- Dry run (no TTS calls)" }

$watchJob = Start-Job -Name 'gw-watch' -ScriptBlock {
  param($Title, $Fps)
  dotnet run --project src/GameWatcher.App -- --watch $Title --fps $Fps
} -ArgumentList $Title, $Fps

try {
  while ($true) {
    Write-Host "[author] scan + generate..." -ForegroundColor Yellow
    $genArgs = @('-Max', $MaxPerPass)
    if ($DryRun) { $genArgs += '-DryRun' }
    if ($Overwrite) { $genArgs += '-Overwrite' }
    & $PSScriptRoot/author_gen.ps1 @genArgs | Write-Host
    Start-Sleep -Seconds $IntervalSec
  }
}
finally {
  Write-Host "Stopping watch job..."
  Stop-Job $watchJob -Force -ErrorAction SilentlyContinue | Out-Null
  Receive-Job $watchJob -ErrorAction SilentlyContinue | Out-Null
  Remove-Job $watchJob -Force -ErrorAction SilentlyContinue | Out-Null
}
