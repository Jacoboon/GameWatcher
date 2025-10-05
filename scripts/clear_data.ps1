param(
  [switch]$All
)

$root = Get-Location
$data = Join-Path $root 'data'

if (Test-Path $data) {
  Write-Host "Clearing data/ catalog..."
  Get-ChildItem -Recurse $data | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  New-Item -ItemType Directory -Path (Join-Path $data 'crops') -Force | Out-Null
  New-Item -ItemType Directory -Path (Join-Path $data 'pre') -Force | Out-Null
  New-Item -ItemType Directory -Path (Join-Path $data 'text') -Force | Out-Null
  New-Item -ItemType Directory -Path (Join-Path $data 'events') -Force | Out-Null
}

if ($All) {
  $out = Join-Path $root 'out'
  if (Test-Path $out) {
    Write-Host "Clearing out/ debug..."
    Get-ChildItem -Recurse $out | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  }
}

Write-Host "Done."

