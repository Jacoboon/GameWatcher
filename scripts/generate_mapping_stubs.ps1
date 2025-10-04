param(
  [string]$Misses = "data/misses.json",
  [string]$ExistingMap = "assets/maps/dialogue.en.json",
  [string]$OutFile = "assets/maps/dialogue.en.todo.json"
)

if (-not (Test-Path $Misses)) { Write-Error "Misses file not found: $Misses"; exit 1 }

$miss = Get-Content -Raw $Misses | ConvertFrom-Json
if (-not $miss -or $miss.Count -eq 0) { Write-Host "No misses found. Nothing to generate."; exit 0 }

$existing = @{}
if (Test-Path $ExistingMap) { $existing = (Get-Content -Raw $ExistingMap | ConvertFrom-Json) }

$out = @{}
$i = 1
foreach ($m in $miss) {
  $key = $m.normalized
  if (-not $key) { continue }
  if ($existing.ContainsKey($key)) { continue }
  if (-not $out.ContainsKey($key)) {
    $stub = ""
    # Suggest a name based on order if desired: "line_miss_$i.wav"
    # $stub = "line_miss_$i.wav"
    $out[$key] = $stub
    $i++
  }
}

if ($out.Count -eq 0) { Write-Host "All misses already mapped. Nothing to generate."; exit 0 }

New-Item -ItemType Directory -Path (Split-Path $OutFile) -Force | Out-Null
($out | ConvertTo-Json -Depth 3) | Set-Content -NoNewline $OutFile
Write-Host "Wrote stub mapping to $OutFile"

