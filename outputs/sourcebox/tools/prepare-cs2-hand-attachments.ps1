param([string]$GameRoot='C:/Program Files (x86)/Steam/steamapps/common/Counter-Strike Global Offensive')
$ErrorActionPreference='Stop'
& node "$PSScriptRoot/../preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Preflight failed'}
$models=Get-Content "$PSScriptRoot/../sheets/cs2-models.json" -Raw | ConvertFrom-Json
foreach($entry in $models.PSObject.Properties){
 $model=$entry.Value
 if($model.weaponAttachment -notmatch '^[a-zA-Z0-9_]+$' -or $model.weaponBone -notmatch '^[a-zA-Z0-9_.]+$'){throw 'Invalid weapon attachment sheet'}
 $file=Join-Path "$GameRoot/content/csgo_addons/sourcebox" $model.model
 $source=Get-Content -LiteralPath $file -Raw
 if($source -notmatch 'name\s*=\s*"sourcebox_hand"'){
  $node='{ _class = "AttachmentList" children = [{ _class = "Attachment" name = "'+$model.weaponAttachment+'" parent_bone = "'+$model.weaponBone+'" relative_origin = [0,0,0] relative_angles = [0,0,0] weight = 1 ignore_rotation = false }] },'
  $source=[regex]::new('children\s*=\s*\[').Replace($source,('children = [ '+$node),1)
  Set-Content -LiteralPath $file -Value $source
 }
 $log="$PSScriptRoot/../logs/cs2-hand-$($entry.Name)-build.log"
 & "$GameRoot/game/bin/win64/resourcecompiler.exe" -retail -nop4 -game "$GameRoot/game/csgo" -i $file *> $log
 $result=Get-Content -LiteralPath $log -Raw
 if($LASTEXITCODE -ne 0 -or $result -notmatch '0 failed' -or $result -match '(?im)^.*Attachment.*(unknown|missing|not found|invalid|error).*|^.*(unknown|missing|not found|invalid|error).*sourcebox_hand.*'){throw "Hand attachment build failed: $($entry.Name); inspect $log"}
 $compiled=Join-Path "$GameRoot/game/csgo_addons/sourcebox" ($model.model+'_c')
 $destination=Join-Path "$GameRoot/game/csgo" ($model.model+'_c')
 Copy-Item -LiteralPath $compiled -Destination $destination -Force
 Write-Output "Compiled hand attachment: $($entry.Name)"
}

