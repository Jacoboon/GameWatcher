param(
  [int]$Max = 25,
  [switch]$DryRun,
  [switch]$Overwrite,
  [string]$Misses = "data/misses.json",
  [string]$Map = "assets/maps/dialogue.en.json",
  [string]$Speakers = "assets/maps/speakers.json",
  [string]$Voices = "assets/voices",
  [string]$Persona = "assets/voices/persona.json"
)

Write-Host "Authoring: generating up to $Max voices from $Misses"

$argsList = @(
  'gen-voices',
  '--misses', $Misses,
  '--map', $Map,
  '--speakers', $Speakers,
  '--voices', $Voices,
  '--persona', $Persona,
  '--max', $Max
)
if ($DryRun) { $argsList += '--dry-run' }
if ($Overwrite) { $argsList += '--overwrite' }

dotnet run --project src/GameWatcher.Tools -- @argsList

