$ErrorActionPreference='Stop'
& node "$PSScriptRoot\preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Preflight failed'}
& node "$PSScriptRoot/tools/prepare-gmod-world-metadata.mjs"
if($LASTEXITCODE -ne 0){throw 'Native world metadata preparation failed'}
$base='C:\Program Files (x86)\Steam\steamapps\common\GarrysMod'
if(Get-Process gmod -ErrorAction SilentlyContinue){throw 'Garrys Mod is already running'}
$portalSettings=Get-Content "$PSScriptRoot/sheets/portals.json" -Raw | ConvertFrom-Json
if($portalSettings.enabled -and $portalSettings.gmodBackend.backend -eq 'successor'){
 $dependency='C:/Program Files (x86)/Steam/steamapps/workshop/content/4000/1800764828/1800764828_via_crowbar.gma'
 if(!(Test-Path -LiteralPath $dependency)){throw 'Download Workshop 1800764828 before launching the successor portal backend'}
 $dependencyMount="$base/garrysmod/addons/sourcebox_portal_dependency"
 if(!(Test-Path -LiteralPath $dependencyMount)){
  New-Item -ItemType Directory -Force $dependencyMount | Out-Null
 }
 # Extracted original dependency remains separate and unmodified in work/.
 $dependencySource=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/phase6/portal-successor")
 if(!(Test-Path "$dependencySource/lua/entities/prop_portal/init.lua")){throw 'Portal successor dependency has not been extracted and reviewed'}
 foreach($folder in @('lua','models','materials','sound','particles')){
  if((Test-Path "$dependencySource/$folder") -and !(Test-Path "$dependencyMount/$folder")){
   New-Item -ItemType Junction -Path "$dependencyMount/$folder" -Target "$dependencySource/$folder" | Out-Null
  }
 }
}
$addonConfig=(Get-Content "$PSScriptRoot\sheets\gmod-scene.json" -Raw | ConvertFrom-Json).addon
if($addonConfig){
 $goblinLink="$base\garrysmod\addons\sourcebox_goblin_3597824658"
 if(!(Test-Path -LiteralPath $goblinLink)){New-Item -ItemType Junction -Path $goblinLink -Target $addonConfig.source | Out-Null}
}
$data="$base\garrysmod\data\sourcebox"
$addon="$base\garrysmod\addons\sourcebox_local"
New-Item -ItemType Directory -Force "$addon\lua\autorun\server","$addon\lua\sourcebox",$data | Out-Null
New-Item -ItemType Directory -Force "$addon/lua/weapons" | Out-Null
New-Item -ItemType Directory -Force "$addon/lua/autorun/client" | Out-Null
Copy-Item "$PSScriptRoot/adapters/gmod/lua/autorun/sourcebox_maptravel.lua" "$addon/lua/autorun/sourcebox_maptravel.lua" -Force
Copy-Item "$PSScriptRoot/adapters/gmod/lua/autorun/sourcebox_worlds.lua" "$addon/lua/autorun/sourcebox_worlds.lua" -Force
Copy-Item "$PSScriptRoot/sheets/worlds.json" "$data/worlds.json" -Force
New-Item -ItemType Directory -Force "$data/worlds" | Out-Null
Copy-Item "$PSScriptRoot/worlds/*.json" "$data/worlds/" -Force
Copy-Item "$PSScriptRoot/sheets/map-travel.json" "$data/map-travel.json" -Force
Copy-Item "$PSScriptRoot/adapters/gmod/lua/autorun/client/sourcebox_performance.lua" "$addon/lua/autorun/client/sourcebox_performance.lua" -Force
Copy-Item "$PSScriptRoot/adapters/gmod/lua/autorun/sourcebox_portal_momentum.lua" "$addon/lua/autorun/sourcebox_portal_momentum.lua" -Force
Copy-Item "$PSScriptRoot/sheets/gmod.json" "$data/gmod.json" -Force
Copy-Item "$PSScriptRoot/adapters/gmod/lua/weapons/weapon_sourcebox_portalgun.lua" "$addon/lua/weapons/weapon_sourcebox_portalgun.lua" -Force
Copy-Item "$PSScriptRoot/adapters/gmod/lua/sourcebox/portal-acceptance.lua" "$addon/lua/sourcebox/portal-acceptance.lua" -Force
Copy-Item "$PSScriptRoot/adapters/gmod/lua/sourcebox/portals.lua" "$addon/lua/sourcebox/portals.lua" -Force
Copy-Item "$PSScriptRoot/adapters/gmod/lua/sourcebox/portal-successor.lua" "$addon/lua/sourcebox/portal-successor.lua" -Force
Copy-Item "$PSScriptRoot/sheets/portals.json" "$data/portals.json" -Force
Copy-Item "$PSScriptRoot\adapters\gmod\lua\autorun\server\sourcebox.lua" "$addon\lua\autorun\server\sourcebox.lua" -Force
Copy-Item "$PSScriptRoot\adapters\gmod\lua\sourcebox\ragdolls.lua" "$addon\lua\sourcebox\ragdolls.lua" -Force
Copy-Item "$PSScriptRoot\adapters\gmod\lua\sourcebox\physics.lua" "$addon\lua\sourcebox\physics.lua" -Force
Copy-Item "$PSScriptRoot\adapters\gmod\lua\sourcebox\scene.lua" "$addon\lua\sourcebox\scene.lua" -Force
Copy-Item "$PSScriptRoot\sheets\gmod-scene.json" "$data\scene.json" -Force
Copy-Item "$PSScriptRoot\sheets\physics-objects.json" "$data\physics.json" -Force
Copy-Item "$PSScriptRoot\sheets\cs2.json" "$data\cs2.json" -Force
Copy-Item "$PSScriptRoot\sheets\hl1mp.json" "$data\hl1mp.json" -Force
Copy-Item "$PSScriptRoot\sheets\asset-references.json" "$data\assets.json" -Force
& node "$PSScriptRoot\prepare-hl1mp-room.mjs"
if($LASTEXITCODE -ne 0){throw 'Room preparation failed'}
$worldSettings=Get-Content "$PSScriptRoot/sheets/worlds.json" -Raw|ConvertFrom-Json
if($worldSettings.enabled){
 $worldCache=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/worlds/assets")
 if(!(Test-Path -LiteralPath $worldCache)){throw 'Import installed world assets before launch'}
 $worldLink="$base/garrysmod/addons/sourcebox_world_assets"
 if(!(Test-Path -LiteralPath $worldLink)){New-Item -ItemType Junction -Path $worldLink -Target $worldCache | Out-Null}
}
$roomSource=if($worldSettings.nativeGeometry){[IO.Path]::GetFullPath("$PSScriptRoot/../../work/worlds/source1-host/hl2/sourcebox_room_001.bsp")}elseif($worldSettings.enabled){"$PSScriptRoot/maps/gmod-worlds/sourcebox_room_001.bsp"}else{"$PSScriptRoot/maps/gmod/sourcebox_room_001.bsp"}
if($worldSettings.nativeGeometry){
 foreach($entry in @(@{name='sourcebox_world_native';relative='work/runtime-assets/worlds-source1'},@{name='sourcebox_world_scenery';relative='work/worlds/scenery/source'})){
  $target=[IO.Path]::GetFullPath("$PSScriptRoot/../../$($entry.relative)")
  if(!(Test-Path -LiteralPath $target)){throw 'Native destination assets missing'}
  $link="$base/garrysmod/addons/$($entry.name)"
  if(!(Test-Path -LiteralPath $link)){New-Item -ItemType Junction -Path $link -Target $target|Out-Null}
 }
}
Copy-Item $roomSource "$base/garrysmod/maps/sourcebox_room_001.bsp" -Force
if($worldSettings.nativeGeometry){
 & node "$PSScriptRoot/tools/prepare-gmod-native-host.mjs" $roomSource "$base/garrysmod/maps/sourcebox_room_001.bsp"
 if($LASTEXITCODE -ne 0){throw 'GMod native host preparation failed'}
}
$cache=(Resolve-Path "$PSScriptRoot\..\..\work\runtime-assets\tf2-soldier").Path.Replace('\','/')
$mount="$base\garrysmod\cfg\mount.cfg"
$content=Get-Content $mount -Raw
if($content -notmatch 'sourcebox_local_assets'){
 Copy-Item $mount "$PSScriptRoot\logs\gmod-mount-before.cfg" -Force
 $content=$content -replace '(?m)^}',("`t`"sourcebox_local_assets`" `"$cache`"`r`n}")
 Set-Content $mount $content
}
$hl1Cache=(Resolve-Path "$PSScriptRoot\..\..\work\runtime-assets\hl1mp").Path.Replace('\','/')
$content=Get-Content $mount -Raw
if($content -notmatch 'sourcebox_hl1_assets'){$content=$content -replace '(?m)^}',("`t`"sourcebox_hl1_assets`" `"$hl1Cache`"`r`n}");Set-Content $mount $content}
$node=(Get-Command node).Source
# The game may have closed while its previous bridge stayed alive.
Get-CimInstance Win32_Process | Where-Object {
 $_.Name -eq 'node.exe' -and $_.ExecutablePath -eq $node -and $_.CommandLine -match 'gmod-bridge\.mjs' -and $_.CommandLine.Replace('/','\') -match [regex]::Escape($data)
} | ForEach-Object {
 $old=Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue
 if($old){Stop-Process -Id $old.Id -Force;if(!$old.WaitForExit(5000)){throw 'Previous GMod bridge did not exit'}}
}
$bridge=Start-Process $node -ArgumentList "gmod-bridge.mjs `"$data`"" -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput "$PSScriptRoot\logs\gmod-bridge.jsonl" -RedirectStandardError "$PSScriptRoot\logs\gmod-bridge-errors.log" -PassThru
Start-Sleep -Milliseconds 500
if($bridge.HasExited){throw 'GMod bridge failed to start; inspect gmod-bridge-errors.log'}
$game=Start-Process "$base\gmod.exe" -ArgumentList '-insecure -multirun -windowed -noborder -w 640 -h 540 -x 0 -y 540 -novid +fps_max 60 +mat_picmip 1 +mat_antialias 0 +mat_forceaniso 2 -console -condebug -nojoy -noworkshop -port 27017 +maxplayers 1 +sv_lan 1 +fps_max_nofocus 30 +fps_max_menu 60 +gamemode sandbox +map sourcebox_room_001' -WorkingDirectory $base -PassThru
# gmod.exe hands off to bin/win64/gmod.exe. Never record the bootstrapper
# as the game: its PID may be reused by an unrelated process after it exits.
$launchRecord=@{gamePid=$null;bootstrapperPid=$game.Id;bridgePid=$bridge.Id;started=(Get-Date).ToUniversalTime().ToString('o')}
$launchRecord | ConvertTo-Json | Set-Content "$PSScriptRoot\logs\gmod-launch.json"
$mainPath=[IO.Path]::GetFullPath("$base/bin/win64/gmod.exe")
for($attempt=0;$attempt -lt 30;$attempt++){
 $main=Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'gmod.exe' -and $_.ExecutablePath -eq $mainPath -and $_.CommandLine -match 'sourcebox_room_001' -and $_.CommandLine -notmatch '--type=' } | Select-Object -First 1
 if($main){$launchRecord.gamePid=$main.ProcessId;$launchRecord.gamePath=$main.ExecutablePath;break}
 Start-Sleep -Milliseconds 500
}
$launchRecord | ConvertTo-Json | Set-Content "$PSScriptRoot\logs\gmod-launch.json"
if(!$launchRecord.gamePid){Write-Warning 'GMod main process not observed yet; gamePid is intentionally unset. Inspect the executable path before any later restart.'}



