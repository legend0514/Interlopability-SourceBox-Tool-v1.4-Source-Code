$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$cs='C:/Program Files (x86)/Steam/steamapps/common/Counter-Strike Global Offensive'
& 'C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/python313/python.exe' "$PSScriptRoot/prepare-cs2-world-sky.py"
if($LASTEXITCODE -ne 0){throw 'Sky enclosure generation failed'}
$log="$root/outputs/sourcebox/logs/cs2-world-sky-import.log"
'y' | & "$cs/game/bin/win64/source1import.exe" -retail -nop4 -nop4sync -src1gameinfodir 'C:/Program Files (x86)/Steam/steamapps/common/Half-Life 2/hl2' -src1contentdir "$root/work/worlds/sky-import" -s2addon sourcebox -game "$cs/game/csgo" maps/sourcebox_world_skies.vmf *> $log
if(!(Select-String -LiteralPath $log -Pattern 'OK: 1 imported, 0 failed' -SimpleMatch -Quiet)){throw 'Sky enclosure import failed; inspect log'}
$maps="$cs/content/csgo_addons/sourcebox/maps"
$files=@(Get-Item -LiteralPath "$maps/sourcebox_world_skies.vmap")+@(Get-ChildItem -LiteralPath "$maps/prefabs/sourcebox_world_skies" -Filter '*.vmap' -Recurse)
foreach($file in $files){
 & "$cs/game/bin/win64/dmxconvert.exe" -i $file.FullName -oe keyvalues2 | Out-Null
 if($LASTEXITCODE -ne 0){throw 'Imported sky map conversion failed'}
 $text=[IO.File]::ReadAllText($file.FullName)
 $text=$text.Replace('materials/skybox/sky_day01_01_hdr.vmat','materials/skybox/sky_de_dust2.vmat')
 [IO.File]::WriteAllText($file.FullName,$text)
}
