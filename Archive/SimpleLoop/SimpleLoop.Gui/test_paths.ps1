# Test path resolution for catalog files
$appDir = Get-Location
Write-Host "Current directory: $appDir"

$simpleLoopDir = Join-Path (Split-Path $appDir -Parent) "SimpleLoop"
Write-Host "SimpleLoop directory: $simpleLoopDir"

$dialoguePath = Join-Path $simpleLoopDir "dialogue_catalog.json"
$speakerPath = Join-Path $simpleLoopDir "speaker_catalog.json"

Write-Host "Dialogue path: $dialoguePath"
Write-Host "Speaker path: $speakerPath"
Write-Host "Dialogue exists: $(Test-Path $dialoguePath)"  
Write-Host "Speaker exists: $(Test-Path $speakerPath)"

if (Test-Path $dialoguePath) {
    $dialogueContent = Get-Content $dialoguePath | ConvertFrom-Json
    Write-Host "Dialogue entries count: $($dialogueContent.Count)"
}

if (Test-Path $speakerPath) {
    $speakerContent = Get-Content $speakerPath | ConvertFrom-Json
    Write-Host "Speaker entries count: $($speakerContent.Count)"
}