param([switch]$Deploy,[ValidateSet('sourcebox_worlds_probe','sourcebox_room_001')][string]$MapName='sourcebox_worlds_probe')
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$csRoot='C:/Program Files (x86)/Steam/steamapps/common/Counter-Strike Global Offensive'
$content="$csRoot/content/csgo_addons/sourcebox"
& "$PSScriptRoot/prepare-cs2-world-sky.ps1"
if($LASTEXITCODE -ne 0){throw 'Native sky enclosure preparation failed'}
& "$PSScriptRoot/build-cs2-worlds.ps1"
if($LASTEXITCODE -ne 0){throw 'World model build failed'}
& 'C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/python313/python.exe' "$PSScriptRoot/export-cs2-world-lights.py"
if($LASTEXITCODE -ne 0){throw 'Light export failed'}
& node "$PSScriptRoot/create-cs2-world-probe.mjs" $content $MapName
if($LASTEXITCODE -ne 0){throw 'Map generation failed'}
$log="$project/logs/cs2-world-map-build.log"
& "$csRoot/game/bin/win64/resourcecompiler.exe" -retail -nop4 -f -game "$csRoot/game/csgo" -i "$content/maps/$MapName.vmap" *> $log
if($LASTEXITCODE -ne 0 -or !(Select-String -Path $log -Pattern '0 failed' -SimpleMatch -Quiet)){throw 'World map build failed'}
if(Select-String -Path $log -Pattern 'RESOURCE COMPILE ERROR|referencing missing material' -Quiet){throw 'Unresolved map dependency'}
if($Deploy){
 Copy-Item "$csRoot/game/csgo_addons/sourcebox/maps/$MapName.vpk" "$csRoot/game/csgo/maps/" -Force
 Copy-Item "$csRoot/game/csgo_addons/sourcebox/models/sourcebox/worlds" "$csRoot/game/csgo/models/sourcebox/" -Recurse -Force
}
Write-Output "Native CS2 destination map built: $MapName. Deployment requested: $Deploy."
