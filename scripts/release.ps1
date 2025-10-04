param(
  [string]$Version = "v0.1.0",
  [string]$Owner = "Jacoboon",
  [string]$Repo = "GameWatcher"
)

if (-not $env:GH_TOKEN) {
  Write-Error "GH_TOKEN env var not set"
  exit 1
}

$ErrorActionPreference = 'Stop'

# Tag locally if tag doesn't exist
git tag -a -f $Version -m "Release $Version"
git push -f origin $Version

$headers = @{ Authorization = "token $($env:GH_TOKEN)"; 'User-Agent' = 'GameWatcher-Release'; 'Accept' = 'application/vnd.github+json' }

# Create GitHub release if not exists
$existing = $null
try {
  $existing = Invoke-RestMethod -Method Get -Uri "https://api.github.com/repos/$Owner/$Repo/releases/tags/$Version" -Headers $headers
} catch { $existing = $null }
if (-not $existing) {
  $body = @{ tag_name = $Version; name = $Version; body = "Initial rebuild and OCR smoke working."; draft = $false; prerelease = $false; target_commitish = "main" } | ConvertTo-Json
  Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$Owner/$Repo/releases" -Headers $headers -ContentType 'application/json' -Body $body | Out-Null
}

Write-Host "Release $Version published"
