param([switch]$PortalProbe,[switch]$TraversalProbe,[switch]$WorldProbe)
$ErrorActionPreference='Stop'
& node "$PSScriptRoot/tools/prepare-hl1-portal-carrier.cjs"
if($LASTEXITCODE -ne 0){throw 'Portal carrier preparation failed'}
& node "$PSScriptRoot\preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Preflight failed'}
if(Get-Process hl1mp,hl1mp_win64 -ErrorAction SilentlyContinue){throw 'HL1MP already running'}
$base='C:\Program Files (x86)\Steam\steamapps\common\Half-Life 1 Source Deathmatch'
New-Item -ItemType Directory -Force "$base/hl1mp/scripts/vscripts" | Out-Null
Copy-Item "$PSScriptRoot/adapters/hl1mp/sourcebox_prop_portals.nut" "$base/hl1mp/scripts/vscripts/sourcebox_prop_portals.nut" -Force
& node "$PSScriptRoot\prepare-hl1mp-assets.cjs"
if($LASTEXITCODE -ne 0){throw 'Asset preparation failed'}
& node "$PSScriptRoot/verify-model-aliases.cjs"
if($LASTEXITCODE -ne 0){throw 'Model alias verification failed'}
& node "$PSScriptRoot\prepare-hl1mp-room.mjs"
if($LASTEXITCODE -ne 0){throw 'Room preparation failed'}
New-Item -ItemType Directory -Force "$base\hl1mp\addons","$base\hl1mp\custom" | Out-Null
Copy-Item "$PSScriptRoot\bin\sourcebox_hl1mp.dll" "$base\hl1mp\addons\sourcebox_hl1mp.dll" -Force
if(!(Test-Path "$PSScriptRoot/bin/sourcebox_preview.dll")){throw 'Build the native HL1 preview tool with build-hl1-preview.ps1 first'}
New-Item -ItemType Directory -Force "$base/bin/x64/tools"|Out-Null
Copy-Item "$PSScriptRoot/bin/sourcebox_preview.dll" "$base/bin/x64/tools/sourcebox_preview.dll" -Force
foreach($dir in @("$base/bin","$base/bin/x64")){
 $file="$dir/enginetools.txt"
 $text=if(Test-Path $file){Get-Content $file -Raw}else{'"enginetools" { }'}
 if($text -notmatch 'tools/sourcebox_preview\.dll'){
  $end=$text.LastIndexOf('}');if($end -lt 0){throw 'Malformed engine tool registry'}
  $text=$text.Insert($end,'"library" "tools/sourcebox_preview.dll"'+"`r`n")
  Set-Content $file $text
 }
}
Copy-Item "$PSScriptRoot\maps\hl1mp\sourcebox_room_001.bsp" "$base\hl1mp\maps\sourcebox_room_001.bsp" -Force
& node "$PSScriptRoot/tools/prepare-native-world-host.mjs" hl1mp "$base/hl1mp"
if($LASTEXITCODE -ne 0){throw 'Destination host preparation failed'}
$cache=(Resolve-Path "$PSScriptRoot\..\..\work\runtime-assets\hl1mp").Path
$tfCache=(Resolve-Path "$PSScriptRoot\..\..\work\runtime-assets\tf2-soldier").Path
foreach($link in @(@{Name='sourcebox_hl1';Target=$cache},@{Name='sourcebox_tf';Target=$tfCache})){
 $path="$base\hl1mp\custom\$($link.Name)"
 if(!(Test-Path -LiteralPath $path)){New-Item -ItemType Junction -Path $path -Target $link.Target | Out-Null}
}
@('sv_cheats 1','give item_suit','impulse 101','hud_draw 1','r_drawviewmodel 1','give weapon_egon','hud_fastswitch 1','bind 3 slot3','alias +sourcebox_use "+use; cmd sourcebox_take"','alias -sourcebox_use "-use"','bind e +sourcebox_use','bind F6 "cmd sourcebox_resetprops"','bind F8 "exec sourcebox_loadout.cfg"') | Set-Content "$base\hl1mp\cfg\sourcebox_loadout.cfg"
# game.cfg runs on map startup and otherwise overrides +sv_lan 1.
$gameConfig="$base\hl1mp\cfg\game.cfg"
$backup="$PSScriptRoot\logs\hl1mp-game-before.cfg"
if(!(Test-Path $backup)){Copy-Item $gameConfig $backup}
$config=Get-Content $gameConfig -Raw
$config=$config -replace '(?m)^sv_lan\s+.*$','sv_lan 1'
Set-Content $gameConfig $config
$launchArgs='-game hl1mp -insecure -multirun -tools -windowed -noborder -w 640 -h 540 -x 1280 -y 0 -novid +fps_max 60 -console -condebug -nojoy -port 27018 +sv_lan 1 +sv_cheats 1 +sv_allow_point_servercommand always +engine_no_focus_sleep 0 +plugin_load addons/sourcebox_hl1mp +map sourcebox_room_001'
if($PortalProbe){
 @('wait 600','give weapon_egon','use weapon_egon','setpos 0 -320 2','setang 0 0 0','wait 90','+attack','wait 15','-attack','setang 0 180 0','wait 90','+attack2','wait 15','-attack2','setpos 350 -320 2','setang 0 0 0','wait 90','jpeg sourcebox_hl1_portal_before','getpos','+forward','wait 120','-forward','getpos','jpeg sourcebox_hl1_portal_after') | Set-Content "$base/hl1mp/cfg/sourcebox_portal_probe.cfg"
 $launchArgs+=' +exec sourcebox_portal_probe.cfg'
}
if($TraversalProbe){
 @('wait 900','setpos 350 320 2','setang 0 0 0','wait 90','getpos','jpeg sourcebox_hl1_travel_before','+forward','wait 180','-forward','getpos','jpeg sourcebox_hl1_travel_after') | Set-Content "$base/hl1mp/cfg/sourcebox_travel_probe.cfg"
 $launchArgs+=' +exec sourcebox_travel_probe.cfg'
}
if($WorldProbe){& node "$PSScriptRoot/tools/write-world-probe.mjs" hl1mp "$base/hl1mp/cfg/sourcebox_world_probe.cfg";if($LASTEXITCODE -ne 0){throw 'World probe preparation failed'};Set-Content "$PSScriptRoot/logs/native-animation.log-world-fixture-hl1mp" 'run'}
$game=Start-Process "$base\hl1mp_win64.exe" -ArgumentList $launchArgs -WorkingDirectory $base -PassThru
@{gamePid=$game.Id;started=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json | Set-Content "$PSScriptRoot\logs\hl1mp-launch.json"




