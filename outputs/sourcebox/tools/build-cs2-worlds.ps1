$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$csRoot='C:/Program Files (x86)/Steam/steamapps/common/Counter-Strike Global Offensive'
$content="$csRoot/content/csgo_addons/sourcebox"
$generated="$csRoot/game/csgo_addons/sourcebox"
$stage=[IO.Path]::GetFullPath("$project/../../work/worlds/cs2-content")
& node "$project/preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Project preflight failed'}
& node "$PSScriptRoot/export-cs2-worlds.mjs"
if($LASTEXITCODE -ne 0){throw 'World configuration preflight failed'}
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python "$PSScriptRoot/export-cs2-worlds-fbx.py"
if($LASTEXITCODE -ne 0){throw 'FBX world conversion failed'}
Copy-Item "$stage/models/sourcebox/worlds" "$content/models/sourcebox/" -Recurse -Force
Copy-Item "$stage/materials/sourcebox/worlds" "$content/materials/sourcebox/" -Recurse -Force
$worlds=Get-Content "$stage/worlds.json" -Raw | ConvertFrom-Json
foreach($world in $worlds){
 $log="$project/logs/cs2-world-$($world.id).log"
 & "$csRoot/game/bin/win64/resourcecompiler.exe" -retail -nop4 -game "$csRoot/game/csgo" -i "$content/$($world.model)" *> $log
 if($LASTEXITCODE -ne 0 -or !(Test-Path "$generated/$($world.model)_c")){throw "World compilation failed: $($world.id); inspect $log"}
 if(Select-String -Path $log -Pattern 'RESOURCE COMPILE ERROR|referencing missing material' -Quiet){throw "Unresolved dependency: $($world.id)"}
 $physics=& "$csRoot/game/bin/win64/resourceinfo.exe" -i "$generated/$($world.model)_c" -b PHYS
 $physics | Set-Content "$project/logs/cs2-world-$($world.id)-physics.txt"
 $text=$physics -join "`n"
 if($text -notmatch 'm_meshes\s*=\s*\[\s*\{'){throw "Missing triangle collision mesh: $($world.id)"}
 foreach($pair in @(@('Min',0),@('Max',1))){
  $match=[regex]::Match($text,"m_v$($pair[0])\s*=\s*\[([^]]+)\]")
  if(!$match.Success){throw "Missing compiled bounds: $($world.id)"}
  $actual=$match.Groups[1].Value.Split(',') | ForEach-Object {[double]::Parse($_,[Globalization.CultureInfo]::InvariantCulture)}
  for($axis=0;$axis -lt 3;$axis++){
   if([Math]::Abs($actual[$axis]-$world.collisionBounds[$pair[1]][$axis]) -gt 0.15){throw "Compiled world coordinate mismatch: $($world.id) $($pair[0]) axis $axis"}
  }
 }
}
# Build output stays in the addon. Runtime activation requires native collision
# and visibility verification before advertising portal destinations to peers.
Write-Output 'World models compiled in the local addon; runtime destinations remain disabled.'
