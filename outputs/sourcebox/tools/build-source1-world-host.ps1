param([ValidateSet('hl2','tf2','gmod','hl1mp','portal2')][string]$Game,[switch]$ResumeLighting,[string]$ContentProfile)
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath("$PSScriptRoot/..")
$workspace=[IO.Path]::GetFullPath("$project/../..")
$hostRoot="$workspace/work/worlds/source1-host"
$mapRoot=$hostRoot
$profileArgs=@()
if($ContentProfile){
 $ContentProfile=[IO.Path]::GetFullPath($ContentProfile)
 $profile=Get-Content -LiteralPath $ContentProfile -Raw | ConvertFrom-Json
 if($profile.version -ne 1){throw 'Unsupported content profile'}
 $profileArgs=@('--content-profile',$ContentProfile)
 if(@($profile.worlds | Where-Object mode -eq 'topology-placeholder').Count){$mapRoot="$workspace/work/worlds/source1-fallback"}
}
$common='C:/Program Files (x86)/Steam/steamapps/common'
& node "$project/preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Configuration preflight failed'}
if($Game -eq 'hl1mp'){
 & 'C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/python313/python.exe' "$PSScriptRoot/prepare-hl1-world-materials.py"
 if($LASTEXITCODE -ne 0){throw 'HL1 material compatibility preparation failed'}
}
if(!$ResumeLighting){
 & 'C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/python313/python.exe' "$PSScriptRoot/combine-source-worlds.py" $Game @profileArgs
 if($LASTEXITCODE -ne 0){throw 'World composition failed'}
}
$compiler=if($Game -eq 'hl1mp'){"$common/Half-Life 1 Source Deathmatch/bin"}elseif($Game -eq 'portal2'){"$common/Portal 2/bin"}else{"$common/Team Fortress 2/bin"}
$map="$mapRoot/$Game/sourcebox_room_001"
if($Game -eq 'portal2'){
 $stage="$common/Portal 2/sdk_content/maps/sourcebox/worlds"
 New-Item -ItemType Directory -Force $stage|Out-Null
 Copy-Item "$map.vmf" "$stage/sourcebox_room_001.vmf" -Force
 $map="$stage/sourcebox_room_001"
}
$stages=if($ResumeLighting){@('vvis','vrad')}else{@('vbsp','vvis','vrad')}
foreach($tool in $stages){
 $flags=if($tool -eq 'vbsp'){@('-threads','2')}elseif($tool -eq 'vvis'){@('-fast','-threads','2')}else{@('-fast','-bounce','2','-threads','2','-both')}
 $log="$mapRoot/$Game/reduced-$tool.log"
 & "$compiler/$tool.exe" @flags -game $hostRoot $map *> $log
 if($LASTEXITCODE -ne 0){throw "$Game $tool failed with exit $LASTEXITCODE. See $log"}
 if(Select-String $log -Pattern '\*\*\*\* leaked|MAX_MAP_|Error:' -Quiet){throw "$Game $tool reported a map error. See $log"}
 if(Select-String $log -Pattern 'MEMORY OVERFLOW' -Quiet){
  # Portal 2 can report exhaustion of its small-block allocator, then finish
  # successfully. Preserve the warning; require its normal completion footer.
  if(!(Select-String $log -Pattern 'seconds elapsed' -Quiet)){throw "$Game compiler did not finish after allocator warning"}
  Write-Warning "$Game compiler reported a small-block heap warning; completed output requires native loading verification. See $log"
 }
}
if($Game -eq 'portal2'){Copy-Item "$map.bsp" "$mapRoot/$Game/sourcebox_room_001.bsp" -Force}
& 'C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/python313/python.exe' "$PSScriptRoot/verify-world-lighting.py" "$mapRoot/$Game/sourcebox_room_001.bsp"
if($LASTEXITCODE -ne 0){throw 'Incomplete world lighting; map is not ready to install'}
if($mapRoot -ne $hostRoot){
 $compiled="$mapRoot/$Game/sourcebox_room_001.bsp"
 & "$compiler/bspzip.exe" -game $hostRoot -addfile $compiled 'materials/sourcebox/fallback/topology.vmt' "$hostRoot/materials/sourcebox/fallback/topology.vmt" "$mapRoot/$Game/sourcebox_room_001.packed.bsp"
 if($LASTEXITCODE -ne 0){throw 'Fallback material packing failed'}
 Copy-Item "$mapRoot/$Game/sourcebox_room_001.packed.bsp" $compiled -Force
}
@{game=$Game;compiled=(Get-Date).ToUniversalTime().ToString('o');contentProfile=$ContentProfile;maps=@('sourcebox_room_001','gm_construct','gm_flatgrass','ctf_2fort','portal_triple_laser','hl1_frenzy');nativeGameplayVerified=$false}|ConvertTo-Json|Set-Content "$mapRoot/$Game/build-result.json"
Write-Output "${Game}: six destinations compiled; native gameplay verification pending."

