param([string]$SteamCommon='C:\Program Files (x86)\Steam\steamapps\common',[string[]]$Games=@('hl2','tf2'))
$ErrorActionPreference='Stop'
& node "$PSScriptRoot\preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Preflight failed'}
foreach($id in $Games){
 if($id -eq 'tf2'){
  # TF2's retail client crashes with an external game directory on this installation.
  # Install only an inert DLL. No VDF: load explicitly on the local test command line.
  $hl1Cache=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/runtime-assets/hl1mp")
  $hl1Mount="$SteamCommon\Team Fortress 2\tf\custom\sourcebox_hl1"
  if((Test-Path $hl1Cache)-and !(Test-Path $hl1Mount)){New-Item -ItemType Directory -Force "$SteamCommon\Team Fortress 2\tf\custom" | Out-Null;New-Item -ItemType Junction -Path $hl1Mount -Target $hl1Cache | Out-Null}
  $dest="$SteamCommon\Team Fortress 2\tf\addons"
  New-Item -ItemType Directory -Force $dest | Out-Null
  Copy-Item "$PSScriptRoot\bin\sourcebox_tf2.dll" "$dest\sourcebox_tf2.dll" -Force
  @('alias +sourcebox_use "+use; cmd sourcebox_take"','alias -sourcebox_use "-use"','bind e +sourcebox_use','bind F7 "cmd sourcebox_take"','bind F6 "cmd sourcebox_resetprops"') | Set-Content -Encoding ascii "$SteamCommon\Team Fortress 2\tf\cfg\sourcebox_controls.cfg"
  New-Item -ItemType Directory -Force "$SteamCommon/Team Fortress 2/tf/scripts/vscripts" | Out-Null
  Copy-Item "$PSScriptRoot/adapters/tf2/sourcebox_portals.nut" "$SteamCommon/Team Fortress 2/tf/scripts/vscripts/sourcebox_portals.nut" -Force
  continue
 }
 $folder=if($id -eq 'hl2'){'Half-Life 2'}else{'Team Fortress 2'}
 $mod=if($id -eq 'hl2'){'hl2_complete'}else{'tf'}
 $base="$SteamCommon\$folder"
 $lab="$PSScriptRoot\labs\$id"
 New-Item -ItemType Directory -Force "$lab\addons","$lab\cfg" | Out-Null
 $info=Get-Content "$base\$mod\gameinfo.txt" -Raw
 # Keep original installed game code/assets, isolate configs/saves and plugin.
 $info=$info -replace '(?m)^\s*game\+mod(?:\+custom_mod)?\s+\S*/custom/\*.*$',''
 $info=$info -replace '(?m)^(\s*game\+game_write\s+).*$','$1"|gameinfo_path|."'
 # Resolve every relative installed-content path explicitly for an external -game folder.
 $baseUnix=$base.Replace('\','/')
 $info=[regex]::Replace($info,'(?m)^(\s*[a-z_+]+\s+)(\|all_source_engine_paths\|)?((?:hl2_complete|hl2|tf|ep2|episodic|platform)/[^\s]+)\s*$',{param($m) $m.Groups[1].Value+'"'+$baseUnix+'/'+$m.Groups[3].Value+'"'})
 $info=[regex]::Replace($info,'(?m)^(\s*(?:game|platform)\s+)\|all_source_engine_paths\|(hl2|episodic|platform)\s*$',{param($m) $m.Groups[1].Value+'"'+$baseUnix+'/'+$m.Groups[2].Value+'"'})
 $info=$info -replace '(SearchPaths\s*\{)',('$1' + "`n`t`t`tgame `"|gameinfo_path|.`"`n`t`t`tgame `"$($base.Replace('\','/'))/$mod`"")
 $hl1Cache=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/runtime-assets/hl1mp").Replace('\','/')
 if(Test-Path $hl1Cache){$info=$info -replace '(SearchPaths\s*\{)',('$1'+"`n`t`t`tgame `"$hl1Cache`"")}
 # Mount only a local model/material cache, never TF2 scripts or binaries.
 $phase=(Get-Content "$PSScriptRoot\sheets\connectivity.json" -Raw | ConvertFrom-Json).phase
 if($id -eq 'hl2' -and $phase -eq 'C'){
  & node "$PSScriptRoot/prepare-soldier-assets.cjs" $SteamCommon
  if($LASTEXITCODE -ne 0){throw 'Soldier asset preparation failed'}
  $tfContent=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/runtime-assets/tf2-soldier").Replace('\','/')
  $mount="`t`t`tgame `"$tfContent`"`n"
  $info=[regex]::Replace($info,'(?m)^(\s*root_mod\s+)',$mount+'$1')
 }
 & node "$PSScriptRoot/prepare-hl2-hud.cjs" $SteamCommon
 if($LASTEXITCODE -ne 0){throw 'HL2 HUD preparation failed'}
 $hudCache=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/runtime-assets/hl2-hud").Replace('\','/')
 $info=$info -replace '(SearchPaths\s*\{)',('$1'+"`n`t`t`tgame `"$hudCache`"")
 Set-Content -Encoding ascii "$lab\gameinfo.txt" $info
 Copy-Item "$PSScriptRoot\bin\sourcebox_$id.dll" "$lab\addons\sourcebox_$id.dll" -Force
 @('Plugin','{',"  file `"addons/sourcebox_$id`"",'}') | Set-Content -Encoding ascii "$lab\addons\sourcebox.vdf"
 @('sv_lan 1','sv_cheats 1','engine_no_focus_sleep 0') | Set-Content -Encoding ascii "$lab\cfg\autoexec.cfg"
 if($id -eq 'hl2'){
  & node "$PSScriptRoot/prepare-portal-inventory.mjs"
  if($LASTEXITCODE -ne 0){throw 'Portal inventory preparation failed'}
  $gameplay=Get-Content "$PSScriptRoot\sheets\hl2-gameplay.json" -Raw | ConvertFrom-Json
  $loadout=@('// Generated from sheets/hl2-gameplay.json; local shared-room equipment only.')+$gameplay.commands
  foreach($binding in $gameplay.bindings.PSObject.Properties){$loadout+='bind "'+$binding.Name+'" "'+$binding.Value+'"'}
  $loadout | Set-Content -Encoding ascii "$lab\cfg\sourcebox_loadout.cfg"
 }
}
