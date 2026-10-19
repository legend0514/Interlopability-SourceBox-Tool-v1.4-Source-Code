param([Parameter(Mandatory=$true)][string]$Request)
$ErrorActionPreference='Stop'
$r=Get-Content -LiteralPath $Request -Raw | ConvertFrom-Json
foreach($p in @($r.csRoot,$r.hl2Root,$r.outputRoot,$r.nodePath)){if(![IO.Path]::IsPathRooted($p)){throw 'Absolute setup paths required'}}
$bin=Join-Path $r.csRoot 'game/bin/win64'
foreach($name in @('resourcecompiler.exe','source1import.exe','dmxconvert.exe')){if(!(Test-Path "$bin/$name")){throw 'Install Counter-Strike 2 Workshop Tools in Steam, then retry setup.'}}
$addon='sourcebox_setup_'+[Guid]::NewGuid().ToString('N')
$content=Join-Path $r.csRoot "content/csgo_addons/$addon"
$compiled=Join-Path $r.csRoot "game/csgo_addons/$addon"
$input=Join-Path $r.outputRoot 'import'
New-Item -ItemType Directory -Force "$input/maps",$r.outputRoot | Out-Null
$vmf=Get-Content "$PSScriptRoot/../maps/cs2/sourcebox_room_001.vmf" -Raw
$vmf+="`nvisgroups { }`nviewsettings { `"bSnapToGrid`" `"1`" `"nGridSpacing`" `"32`" }`ncameras { `"activecamera`" `"-1`" }`ncordons { `"active`" `"0`" }`n"
Set-Content "$input/maps/sourcebox_room_001.vmf" $vmf
'y' | & "$bin/source1import.exe" -retail -nop4 -nop4sync -src1gameinfodir "$($r.hl2Root)/hl2" -src1contentdir $input -s2addon $addon -game "$($r.csRoot)/game/csgo" maps/sourcebox_room_001.vmf *> "$($r.outputRoot)/map-import.log"
if($LASTEXITCODE -ne 0 -or !(Select-String "$($r.outputRoot)/map-import.log" -Pattern 'OK: 1 imported, 0 failed' -SimpleMatch -Quiet)){throw 'Shared Room import failed; see map-import.log'}
foreach($map in Get-ChildItem "$content/maps" -Filter '*.vmap' -Recurse){& "$bin/dmxconvert.exe" -i $map.FullName -oe keyvalues2 | Out-Null;if($LASTEXITCODE -ne 0){throw 'Map text conversion failed'}}
& $r.nodePath "$PSScriptRoot/cs2-room-native.cjs" $content
if($LASTEXITCODE -ne 0){throw 'Room lighting preparation failed'}
& $r.nodePath "$PSScriptRoot/prepare-portable-room-materials.cjs" $content
if($LASTEXITCODE -ne 0){throw 'Room materials preparation failed'}
foreach($material in Get-ChildItem "$content/materials/sourcebox/room" -Filter '*.vmat'){
 & "$bin/resourcecompiler.exe" -retail -nop4 -game "$($r.csRoot)/game/csgo" -i $material.FullName *> "$($r.outputRoot)/$($material.BaseName)-compile.log"
 if($LASTEXITCODE -ne 0){throw 'Room material compilation failed'}
}
& "$bin/resourcecompiler.exe" -retail -nop4 -game "$($r.csRoot)/game/csgo" -i "$content/maps/sourcebox_room_001.vmap" *> "$($r.outputRoot)/map-compile.log"
if($LASTEXITCODE -ne 0 -or !(Select-String "$($r.outputRoot)/map-compile.log" -Pattern '0 failed' -SimpleMatch -Quiet)){throw 'Room compilation failed; see map-compile.log'}
if(Select-String "$($r.outputRoot)/map-compile.log" -Pattern 'referencing missing material|RESOURCE COMPILE ERROR' -Quiet){throw 'Room has unresolved dependencies'}
foreach($folder in @('maps','materials','lighting','models')){if(Test-Path "$compiled/$folder"){Copy-Item "$compiled/$folder" $r.outputRoot -Recurse -Force}}
if(!(Test-Path "$($r.outputRoot)/maps/sourcebox_room_001.vpk")){throw 'Compiled Shared Room is missing'}
# Retain only local generated build folders for diagnostics. Never touch an existing addon.
@{content=$content;compiled=$compiled;localOnly=$true} | ConvertTo-Json | Set-Content "$($r.outputRoot)/build-locations.json"
Write-Output 'Shared Room compiled locally.'
