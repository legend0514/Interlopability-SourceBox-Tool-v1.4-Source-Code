$ErrorActionPreference='Stop'
& node "$PSScriptRoot/room.mjs"
if($LASTEXITCODE -ne 0){throw 'Room preflight failed'}
$base='C:\Program Files (x86)\Steam\steamapps\common\Portal 2'
# Portal 2's compiler refuses output paths outside its install (Valve issue #430).
$stage="$base\sdk_content\maps\sourcebox"
New-Item -ItemType Directory -Force $stage|Out-Null
Copy-Item "$PSScriptRoot/maps/portal2/sourcebox_room_001.vmf" $stage -Force
foreach($tool in @('vbsp','vvis','vrad')){
 & "$base\bin\$tool.exe" -game "$base\portal2" "$stage\sourcebox_room_001" *> "$PSScriptRoot/logs/portal2-$tool.log"
 if($LASTEXITCODE -ne 0){throw "Portal 2 $tool failed"}
}
if(Select-String "$PSScriptRoot/logs/portal2-vbsp.log" -Pattern 'leaked|Error:' -Quiet){throw 'Portal 2 room compile error'}
Copy-Item "$stage/sourcebox_room_001.bsp" "$PSScriptRoot/maps/portal2/sourcebox_room_001.bsp" -Force
