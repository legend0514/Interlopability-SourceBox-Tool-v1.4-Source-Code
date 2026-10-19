$ErrorActionPreference='Stop'
& node "$PSScriptRoot\room.mjs"
if($LASTEXITCODE -ne 0){throw 'Room preflight failed'}
foreach($id in @('hl2','tf2')){
 $base=if($id -eq 'hl2'){'C:\Program Files (x86)\Steam\steamapps\common\Half-Life 2'}else{'C:\Program Files (x86)\Steam\steamapps\common\Team Fortress 2'}
 $bin=if($id -eq 'hl2'){"$base\bin"}else{"$base\bin\x64"}
 $game=if($id -eq 'hl2'){"$base\hl2_complete"}else{"$base\tf"}
 $map="$PSScriptRoot\maps\$id\sourcebox_room_001"
 foreach($tool in @('vbsp','vvis','vrad')){
  & "$bin\$tool.exe" -game $game $map *> "$map-$tool.log"
  if($LASTEXITCODE -ne 0){throw "$id $tool failed"}
 }
 if(Select-String -Path "$map-vbsp.log" -Pattern 'leaked|error' -Quiet){throw 'Map compiler reported leak/error'}
}
