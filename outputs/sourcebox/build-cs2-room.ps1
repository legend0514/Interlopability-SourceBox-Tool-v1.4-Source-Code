$ErrorActionPreference='Stop'
$root='C:/Program Files (x86)/Steam/steamapps/common/Counter-Strike Global Offensive'
$bin="$root/game/bin/win64"
$content="$root/content/csgo_addons/sourcebox"
if(!(Test-Path "$bin/resourcecompiler.exe")){throw 'Install CS2 Workshop Tools in Steam first'}
& node "$PSScriptRoot/room.mjs"
if($LASTEXITCODE -ne 0){throw 'Room preflight/generation failed'}
$stage=[IO.Path]::GetFullPath("$PSScriptRoot/../../work/cs2/import")
New-Item -ItemType Directory -Force "$stage/maps" | Out-Null
$vmf=Get-Content "$PSScriptRoot/maps/cs2/sourcebox_room_001.vmf" -Raw
$vmf += "`nvisgroups`n{`n}`nviewsettings`n{`n`"bSnapToGrid`" `"1`"`n`"nGridSpacing`" `"32`"`n}`ncameras`n{`n`"activecamera`" `"-1`"`n}`ncordons`n{`n`"active`" `"0`"`n}`n"
Set-Content "$stage/maps/sourcebox_room_001.vmf" $vmf
'y' | & "$bin/source1import.exe" -retail -nop4 -nop4sync -src1gameinfodir 'C:/Program Files (x86)/Steam/steamapps/common/Half-Life 2/hl2' -src1contentdir $stage -s2addon sourcebox -game "$root/game/csgo" maps/sourcebox_room_001.vmf *> "$PSScriptRoot/logs/cs2-map-import.log"
if(!(Select-String -Path "$PSScriptRoot/logs/cs2-map-import.log" -Pattern 'OK: 1 imported, 0 failed' -SimpleMatch -Quiet)){throw 'Source 2 import failed; inspect log'}
Get-ChildItem "$content/maps" -Filter *.vmap -Recurse | ForEach-Object {
 & "$bin/dmxconvert.exe" -i $_.FullName -oe keyvalues2 | Out-Null
 if($LASTEXITCODE -ne 0){throw 'DMX conversion failed'}
}
& node "$PSScriptRoot/tools/cs2-room-native.cjs" $content
if($LASTEXITCODE -ne 0){throw 'Native material/lighting preparation failed'}
foreach($name in @('dev_measurewall01a','dev_measuregeneric01b')){
 & "$bin/resourcecompiler.exe" -retail -nop4 -game "$root/game/csgo" -i "$content/materials/sourcebox/room/$name.vmat" *> "$PSScriptRoot/logs/cs2-room-$name.log"
 if($LASTEXITCODE -ne 0 -or !(Test-Path "$root/game/csgo_addons/sourcebox/materials/sourcebox/room/$name.vmat_c")){throw 'Required room material failed to compile'}
}
& "$bin/resourcecompiler.exe" -retail -nop4 -game "$root/game/csgo" -i "$content/maps/sourcebox_room_001.vmap" *> "$PSScriptRoot/logs/cs2-map-build.log"
if($LASTEXITCODE -ne 0 -or !(Select-String -Path "$PSScriptRoot/logs/cs2-map-build.log" -Pattern '0 failed' -SimpleMatch -Quiet)){throw 'CS2 room compilation failed; inspect log'}
if(Select-String -Path "$PSScriptRoot/logs/cs2-map-build.log" -Pattern 'referencing missing material|RESOURCE COMPILE ERROR' -Quiet){throw 'Missing map dependencies; refusing deployment'}
& "$bin/resourcecompiler.exe" -retail -nop4 -game "$root/game/csgo" -i "$content/lighting/postprocessing/sourcebox_room_001/sourcebox_room_001.vpost" *> "$PSScriptRoot/logs/cs2-post-build.log"
if($LASTEXITCODE -ne 0){throw 'Postprocess compile failed'}
foreach($d in @('lighting','materials','models')){if(Test-Path "$root/game/csgo_addons/sourcebox/$d"){Copy-Item "$root/game/csgo_addons/sourcebox/$d" "$root/game/csgo" -Recurse -Force}}
Copy-Item "$root/game/csgo_addons/sourcebox/maps/sourcebox_room_001.vpk" "$root/game/csgo/maps/sourcebox_room_001.vpk" -Force
Write-Output 'CS2 shared room compiled and deployed locally. Gameplay acceptance remains separate.'
