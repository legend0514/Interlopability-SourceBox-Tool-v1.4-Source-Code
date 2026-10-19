param([ValidateSet('hl2','tf2')][string[]]$Games=@('hl2','tf2'),[switch]$ReplaceRetail,[switch]$WithoutPlugin,[switch]$PortalProbe,[switch]$MomentumProbe,[switch]$WorldProbe)
$ErrorActionPreference='Stop'
& node "$PSScriptRoot/preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Preflight failed'}
$sdk=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/sourcebox-sdk/game")
foreach($id in $Games){
 $mod=if($id -eq 'hl2'){'mod_hl2'}else{'mod_tf'}
 $running=Get-CimInstance Win32_Process | Where-Object {
  ($_.Name -eq "${mod}_win64.exe" -and $_.ExecutablePath -eq "$sdk\${mod}_win64.exe") -or
  ($id -eq 'hl2' -and $_.Name -eq 'hl2.exe' -and $_.CommandLine -like "*$PSScriptRoot\labs\hl2*") -or
  ($id -eq 'tf2' -and $_.Name -eq 'tf_win64.exe' -and $_.CommandLine -like '*+plugin_load addons/sourcebox_tf2*' -and $_.CommandLine -like '*sourcebox_room_001*')
 }
 if($running -and !$ReplaceRetail){throw "Existing $id session; use -ReplaceRetail for the test switch"}
 foreach($p in $running){$process=Get-Process -Id $p.ProcessId -ErrorAction SilentlyContinue;if($process){Stop-Process -Id $p.ProcessId -Force;if(!$process.WaitForExit(5000)){throw 'SDK process did not exit; refusing to overwrite loaded libraries'}}}
 & node "$PSScriptRoot/tools/prepare-sdk-mods.mjs" $id
 if($LASTEXITCODE -ne 0){throw 'SDK preparation failed'}
 $port=if($id -eq 'hl2'){27016}else{27015}
 $x=if($id -eq 'hl2'){640}else{0}
 $args="-game `"$sdk/$mod`" -multirun -insecure -windowed -noborder -w 640 -h 540 -x $x -y 0 -novid +fps_max 60 -console -condebug -nojoy -port $port +sv_lan 1 +sv_cheats 1 +sv_allow_point_servercommand always +engine_no_focus_sleep 0 +plugin_load addons/sourcebox_$id +exec sourcebox_controls.cfg +map sourcebox_room_001"
 if($id -eq 'hl2'){$args=$args.Replace('+map sourcebox_room_001','+maxplayers 2 +sv_pure -1 +map sourcebox_room_001')}
 if($WithoutPlugin){$args=$args.Replace("+plugin_load addons/sourcebox_$id",'')}
 if($PortalProbe){$args+=' +sourcebox_portal_probe 1';if($id -eq 'tf2'){$args+=' +jointeam red +joinclass soldier'}}
 if($MomentumProbe){$args+=' +sourcebox_portal_probe 2';if($id -eq 'tf2'){$args+=' +jointeam red +joinclass soldier'}}
 if($WorldProbe){& node "$PSScriptRoot/tools/write-world-probe.mjs" $id "$sdk/$mod/cfg/sourcebox_world_probe.cfg";if($LASTEXITCODE -ne 0){throw 'World probe preparation failed'};Set-Content "$PSScriptRoot/logs/native-animation.log-world-fixture-$id" 'run';if($id -eq 'tf2'){$args+=' +jointeam red +joinclass soldier'}}
 $p=Start-Process -FilePath "$sdk/${mod}_win64.exe" -ArgumentList $args -WorkingDirectory $sdk -PassThru
 @{game=$id;pid=$p.Id;kind='sdk-baseline';portalPortComplete=$false;started=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json | Set-Content "$PSScriptRoot/logs/sdk-$id-launch.json"
 Write-Output "$id SDK launched: $($p.Id)"
}
