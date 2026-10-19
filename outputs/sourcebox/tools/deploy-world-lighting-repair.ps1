param([Parameter(Mandatory=$true)][string]$Python)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$stage=Join-Path $workspace 'work/worlds/lighting-repair'
$settings=Get-Content "$env:LOCALAPPDATA/SourceBox/settings.json" -Raw | ConvertFrom-Json
$common=Join-Path $settings.SteamRoot 'steamapps/common'
# This local repair targets the known six-game installation, never a release ZIP.
$destinations=@{
 hl2=(Join-Path $settings.SdkRoot 'mod_hl2/maps/sourcebox_room_001.bsp')
 tf2=(Join-Path $settings.SdkRoot 'mod_tf/maps/sourcebox_room_001.bsp')
 hl1mp=(Join-Path $common 'Half-Life 1 Source Deathmatch/hl1mp/maps/sourcebox_room_001.bsp')
 portal2=(Join-Path $common 'Portal 2/portal2/maps/sourcebox_room_001.bsp')
 gmod=(Join-Path $common 'GarrysMod/garrysmod/maps/sourcebox_room_001.bsp')
 cs2=(Join-Path $common 'Counter-Strike Global Offensive/game/csgo/maps/sourcebox_room_001.vpk')
}
$running=@(Get-Process -Name hl2,hl2_win64,mod_hl2_win64,mod_tf_win64,tf_win64,gmod,portal2,cs2 -ErrorAction SilentlyContinue)
if($running.Count){throw 'Close the game sessions before replacing maps. No processes were stopped.'}
$plan=@()
foreach($game in $destinations.Keys){
 $destination=[IO.Path]::GetFullPath($destinations[$game])
 $source=Join-Path $stage "$game/$([IO.Path]::GetFileName($destination))"
 if(!(Test-Path -LiteralPath $source) -or !(Test-Path -LiteralPath $destination)){throw "Missing staged or installed map for $game"}
 if($game -ne 'cs2'){
  & $Python "$PSScriptRoot/verify-world-lighting.py" $source --original $destination
  if($LASTEXITCODE -ne 0){throw "Lighting/geometry validation failed for $game; nothing installed"}
 }
 $plan+=@{game=$game;source=$source;destination=$destination}
}
foreach($game in @('hl2','tf2','hl1mp','portal2')){
 $canonical=Join-Path $workspace "work/worlds/source1-host/$game/sourcebox_room_001.bsp"
 $source=Join-Path $stage "$game/sourcebox_room_001.bsp"
 if(!(Test-Path -LiteralPath $canonical)){throw "Missing existing compiled host for $game"}
 $plan+=@{game="compiled-$game";source=$source;destination=$canonical}
}
$diagnostic=Join-Path $common 'GarrysMod/garrysmod/addons/sourcebox_local/lua/autorun/client/sourcebox_performance.lua'
if(Test-Path -LiteralPath $diagnostic){$plan+=@{game='gmod-lighting-diagnostics';source=(Join-Path $workspace 'outputs/sourcebox/adapters/gmod/lua/autorun/client/sourcebox_performance.lua');destination=$diagnostic}}
if(!(Select-String "$stage/cs2-build.log" -Pattern '0 failed' -SimpleMatch -Quiet)){throw 'CS2 compile has not passed'}
$backup=Join-Path $stage ('installed-backup-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $backup | Out-Null
$changed=@()
try{
 foreach($item in $plan){
  $item.backup=Join-Path $backup ($item.game+[IO.Path]::GetExtension($item.destination))
  Copy-Item -LiteralPath $item.destination -Destination $item.backup
  $changed+=$item
  Copy-Item -LiteralPath $item.source -Destination $item.destination -Force
  if((Get-FileHash -LiteralPath $item.source).Hash -ne (Get-FileHash -LiteralPath $item.destination).Hash){throw 'Installed map hash mismatch'}
 }
}catch{
 foreach($item in $changed){Copy-Item -LiteralPath $item.backup -Destination $item.destination -Force}
 throw
}
@{installed=$plan;nativeGameplayVerified=$false;installedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 5 | Set-Content "$stage/deployment.json"
'Six repaired maps installed with backups. Native visual verification remains pending.'
