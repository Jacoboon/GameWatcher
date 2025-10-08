param(
    [string[]]$Voices = @("alloy","amber","copper","onyx","shimmer","fable","echo","nova","verse","sage","aria","coral"),
    [double]$Start = 0.5,
    [double]$End = 1.4,
    [double]$Step = 0.1,
    [string]$Format = "mp3"
)

$ErrorActionPreference = "Stop"

# Resolve engine Voices output directory
$engineVoicesDir = Join-Path $PSScriptRoot "..\GameWatcher-Platform\GameWatcher.Engine\Voices"
New-Item -ItemType Directory -Path $engineVoicesDir -Force | Out-Null

# Load key from env or Secrets fallback
$key = [Environment]::GetEnvironmentVariable("GWS_OPENAI_API_KEY", "User")
if (-not $key) { $key = [Environment]::GetEnvironmentVariable("GWS_OPENAI_API_KEY", "Process") }
if (-not $key) {
  $secrets = Join-Path $PSScriptRoot "..\Secrets\openai-api-key.txt"
  if (Test-Path $secrets) { $key = (Get-Content $secrets -Raw).Trim() }
}
if (-not $key) { throw "No API key. Set GWS_OPENAI_API_KEY or Secrets/openai-api-key.txt." }

Write-Host "Generating previews to: $engineVoicesDir" -ForegroundColor Cyan
Write-Host "Voices: $($Voices -join ', ')" -ForegroundColor DarkGray

$total = 0; $skipped = 0; $failed = 0

function Save-Preview($voice, $speed) {
  $safeVoice = ($voice -replace '[\\/\:\*\?\"<>\|]', '_')
  $fileName = "{0}-{1:N1}.{2}" -f $safeVoice, $speed, $Format
  $outPath = Join-Path $engineVoicesDir $fileName
  if (Test-Path $outPath) { $script:skipped++; return }

  $payload = @{ model = "gpt-4o-mini-tts"; voice = $voice; input = "Hi! I'm $voice. Calm. Excited! Curious? Let's begin."; format = $Format; speed = [Math]::Round($speed,2) } | ConvertTo-Json -Depth 3
  $headers = @{ Authorization = "Bearer $key" }
  $tmp = [System.IO.Path]::GetTempFileName()
  try {
    Invoke-WebRequest -Uri "https://api.openai.com/v1/audio/speech" -Method Post -Headers $headers -ContentType "application/json" -Body $payload -OutFile $tmp | Out-Null
    Move-Item -Force $tmp $outPath
    $script:total++
  }
  catch {
    $script:failed++
    if (Test-Path $tmp) { Remove-Item $tmp -Force }
    Write-Warning "Failed: $voice@$speed — $($_.Exception.Message)"
  }
}

for ($s = $Start; $s -le $End + 1e-9; $s += $Step) {
  foreach ($v in $Voices) { Save-Preview -voice $v -speed $s }
}

Write-Host "Generated: $total  Skipped: $skipped  Failed: $failed" -ForegroundColor Green
