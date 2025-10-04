param(
  [string]$Source = "Artifacts/templates",
  [string]$Target = "assets/templates"
)

$src = Resolve-Path $Source -ErrorAction SilentlyContinue
if (-not $src) {
  Write-Error "Source not found: $Source"
  exit 1
}

$dest = Join-Path (Get-Location) $Target
New-Item -ItemType Directory -Path $dest -Force | Out-Null

Get-ChildItem -Path $src -Filter *.png | ForEach-Object {
  Copy-Item $_.FullName -Destination (Join-Path $dest $_.Name) -Force
}

Write-Host "Copied templates to $dest"

