param([switch]$WithoutPlugin,[string[]]$Games=@('hl2','tf2'),[switch]$PortalInventoryProbe)
$ErrorActionPreference='Stop'
foreach($id in $Games){
 $name=if($id -eq 'hl2'){'hl2'}else{'tf_win64'}
 if(Get-Process -Name $name -ErrorAction SilentlyContinue){throw "$name is already running. Close the test game before launching again."}
}
& "$PSScriptRoot\prepare-labs.ps1" -Games $Games
if(($PortalInventoryProbe -or (Get-Content "$PSScriptRoot/sheets/portals.json" -Raw | ConvertFrom-Json).enabled) -and $Games -contains 'hl2'){
 @('give weapon_cubemap','use weapon_cubemap','echo SOURCEBOX_PORTAL_INVENTORY_PROBE') | Add-Content "$PSScriptRoot/labs/hl2/cfg/sourcebox_loadout.cfg"
}
New-Item -ItemType Directory -Force "$PSScriptRoot\logs" | Out-Null
$node=(Get-Command node).Source
$listener=Get-NetTCPConnection -LocalPort 27841 -State Listen -ErrorAction SilentlyContinue
if(!$listener){
 $core=Start-Process -FilePath $node -ArgumentList 'core.mjs' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -RedirectStandardOutput "$PSScriptRoot\logs\core.jsonl" -RedirectStandardError "$PSScriptRoot\logs\core-error.log" -PassThru
 Start-Sleep -Milliseconds 500
 if($core.HasExited){throw 'Core did not start; inspect core-error.log'}
}else{$core=Get-Process -Id $listener.OwningProcess}
$launched=@()
foreach($id in $Games){
 if($id -notin @('hl2','tf2')){throw 'Unknown game'}
 if($WithoutPlugin -and $id -eq 'hl2'){Rename-Item -LiteralPath "$PSScriptRoot\labs\$id\addons\sourcebox.vdf" -NewName 'sourcebox.disabled' -Force}
 $base=if($id -eq 'hl2'){'C:\Program Files (x86)\Steam\steamapps\common\Half-Life 2'}else{'C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2'}
 $exe=if($id -eq 'hl2'){'hl2.exe'}else{'tf_win64.exe'}
 $phase=(Get-Content "$PSScriptRoot\sheets\connectivity.json" -Raw | ConvertFrom-Json).phase
 $map=if($phase -in @('B','C')){'sourcebox_room_001'}elseif($id -eq 'hl2'){'d1_trainstation_01'}else{'cp_dustbowl'}
 if($phase -in @('B','C')){
  $mapDest=if($id -eq 'hl2'){"$PSScriptRoot\labs\hl2\maps"}else{"$base\tf\maps"}
  New-Item -ItemType Directory -Force $mapDest | Out-Null
  Copy-Item "$PSScriptRoot\maps\$id\sourcebox_room_001.bsp" "$mapDest\sourcebox_room_001.bsp" -Force
 }
 $gameDir=if($id -eq 'hl2'){"$PSScriptRoot\labs\hl2"}else{'tf'}
 $plugin=if($id -eq 'tf2' -and !$WithoutPlugin){'+plugin_load addons/sourcebox_tf2 +exec sourcebox_controls.cfg'}else{''}
 $args="-game `"$gameDir`" -multirun -insecure -windowed -noborder -w 640 -h 540 -x $(if($id -eq 'hl2'){640}else{0}) -y 0 -novid -console -condebug -nojoy -port $(if($id -eq 'hl2'){27016}else{27015}) +sv_lan 1 +sv_cheats 1 +sv_allow_point_servercommand always +engine_no_focus_sleep 0 $plugin +map $map"
 if($id -eq 'tf2'){$args+=" +script_execute sourcebox_portals"}
 $p=Start-Process -FilePath "$base\$exe" -ArgumentList $args -WorkingDirectory $base -PassThru
 $launched+=@{game=$id;pid=$p.Id;exe="$base\$exe"}
}
@{corePid=$core.Id;games=$launched;started=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json -Depth 4 | Set-Content "$PSScriptRoot\logs\launch.json"
$launched | Format-Table

