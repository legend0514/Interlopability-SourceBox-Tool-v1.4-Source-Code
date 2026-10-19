param([switch]$PortalProbe,[switch]$PreviewProbe,[switch]$WorldProbe)
$ErrorActionPreference='Stop'
& node "$PSScriptRoot/preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Configuration preflight failed'}
if(Get-Process portal2 -ErrorAction SilentlyContinue){throw 'Portal 2 is already running'}
$base='C:\Program Files (x86)\Steam\steamapps\common\Portal 2'
# Add only local model/material caches after native Portal 2 content.
$infoPath="$base/portal2/gameinfo.txt"
$info=Get-Content -LiteralPath $infoPath -Raw
$cache=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/runtime-assets").Replace('\','/')
if($info -notmatch 'sourcebox-assets-begin'){
 Copy-Item -LiteralPath $infoPath -Destination "$PSScriptRoot/logs/portal2-gameinfo-original.txt" -Force
 $mount="`r`n`t`t`t// sourcebox-assets-begin`r`n`t`t`tGame `"$cache/hl1mp`"`r`n`t`t`tGame `"$cache/tf2-soldier`"`r`n`t`t`t// sourcebox-assets-end"
 $info=$info -replace '(Game\s+\|gameinfo_path\|\.)',('$1'+$mount)
 Set-Content -LiteralPath $infoPath -Value $info -Encoding ascii
}
if(!(Get-NetTCPConnection -LocalPort 27841 -State Listen -ErrorAction SilentlyContinue)){throw 'Start Sourcebox Core first'}
New-Item -ItemType Directory -Force "$base/portal2/addons","$base/portal2/maps"|Out-Null
Copy-Item "$PSScriptRoot/bin/sourcebox_portal2.dll" "$base/portal2/addons/sourcebox_portal2.dll" -Force
Copy-Item "$PSScriptRoot/maps/portal2/sourcebox_room_001.bsp" "$base/portal2/maps/sourcebox_room_001.bsp" -Force
& node "$PSScriptRoot/tools/prepare-native-world-host.mjs" portal2 "$base/portal2"
if($LASTEXITCODE -ne 0){throw 'Destination host preparation failed'}
$launchArgs='-applaunch 620 -insecure -multirun -allowmultiple -windowed -noborder -w 640 -h 540 -x 1280 -y 540 -novid +fps_max 60 -console -condebug -nojoy -port 27020 +sv_lan 1 +sv_cheats 1 +engine_no_focus_sleep 0 +plugin_load addons/sourcebox_portal2 +map sourcebox_room_001'
$rendering=(Get-Content "$PSScriptRoot/sheets/portal2.json" -Raw|ConvertFrom-Json).rendering
# The native fast path supports only four visible portals. Six shared pairs
# require the general stencil path; apply locally, not via server commands.
$launchArgs=$launchArgs.Replace('+plugin_load',"+r_portal_fastpath $($rendering.fastPath) +r_portal_stencil_depth $($rendering.stencilDepth) +plugin_load")
if($PreviewProbe){
 $fixture=@('wait 600','r_portal_fastpath','r_portal_stencil_depth','jpeg sourcebox_portal_stencil_check','wait 180','jpeg sourcebox_portal_stencil_check_later')
 Set-Content -LiteralPath "$base/portal2/cfg/sourcebox_preview_probe.cfg" -Value $fixture -Encoding ascii
 $launchArgs+=' +exec sourcebox_preview_probe.cfg'
}
if($PortalProbe){
 # Exercise the actual native player against the existing shared HL2 portal pair.
 $fixture=@('wait 600','setpos 350 -320 1','setang 0 0 0','wait 120','jpeg sourcebox_native_portal_before','getpos','+forward','wait 160','-forward','getpos','jpeg sourcebox_native_portal_after')
 Set-Content -LiteralPath "$base/portal2/cfg/sourcebox_portal_probe.cfg" -Value $fixture -Encoding ascii
 $launchArgs+=' +exec sourcebox_portal_probe.cfg'
}
# Portal 2's supported -allowmultiple flag requires VPROJECT in the game's
# environment. An already-running Steam process does not inherit this value.
$launchArgs=$launchArgs.Replace('-applaunch 620','-game portal2 -steam')
if($WorldProbe){& node "$PSScriptRoot/tools/write-world-probe.mjs" portal2 "$base/portal2/cfg/sourcebox_world_probe.cfg";if($LASTEXITCODE -ne 0){throw 'World probe preparation failed'};Set-Content "$PSScriptRoot/logs/native-animation.log-world-fixture-portal2" 'run'}
$p=Start-Process "$base/portal2.exe" -ArgumentList $launchArgs -WorkingDirectory $base -Environment @{VPROJECT="$base/portal2";SteamAppId='620'} -PassThru
@{launcherPid=$p.Id;started=(Get-Date).ToUniversalTime().ToString('o');stage='integration-probe'}|ConvertTo-Json|Set-Content "$PSScriptRoot/logs/portal2-launch.json"

