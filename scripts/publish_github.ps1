param(
  [Parameter(Mandatory=$true)][string]$Owner,
  [Parameter(Mandatory=$true)][string]$Repo,
  [switch]$Force
)

if (-not $env:GH_TOKEN) {
  Write-Error "GH_TOKEN env var not set (needs repo admin:delete_repo, repo)."
  exit 1
}

$ErrorActionPreference = 'Stop'

$repoFull = "$Owner/$Repo"
$api = "https://api.github.com/repos/$repoFull"
$headers = @{ Authorization = "token $($env:GH_TOKEN)"; 'User-Agent' = 'GameWatcher-Publisher'; 'Accept' = 'application/vnd.github+json' }

try {
  Write-Host "Deleting remote repo $repoFull (if exists)..."
  Invoke-RestMethod -Method Delete -Uri $api -Headers $headers -ErrorAction SilentlyContinue | Out-Null
} catch {}

Write-Host "Creating remote repo $repoFull..."
Invoke-RestMethod -Method Post -Uri "https://api.github.com/user/repos" -Headers $headers -ContentType 'application/json' -Body (@{ name = $Repo; private = $false } | ConvertTo-Json) | Out-Null

if (-not (Test-Path .git)) { git init }
git add -A
if (-not (git log -1 2>$null)) { git commit -m "Rebuild: clean slate GameWatcher" } else { git commit -m "Update" }
git branch -M main

$remoteUrl = "https://$($env:GH_TOKEN)@github.com/$repoFull.git"
if (git remote get-url origin 2>$null) { git remote set-url origin $remoteUrl } else { git remote add origin $remoteUrl }

if ($Force) { git push -f origin main } else { git push origin main }

# Sanitize remote (remove embedded token)
git remote set-url origin "https://github.com/$repoFull.git"

Write-Host "Published to https://github.com/$repoFull"
