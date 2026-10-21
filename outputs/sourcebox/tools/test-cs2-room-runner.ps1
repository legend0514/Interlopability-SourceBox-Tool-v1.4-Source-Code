param([string]$Node='node.exe')
$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/prepare-portable-cs2-room.ps1",[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Room script does not parse'}
$definition=$ast.Find({param($item) $item -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $item.Name -eq 'Invoke-RoomTool'},$true)
. ([scriptblock]::Create($definition.Extent.Text))
$log=Join-Path $env:TEMP ('sourcebox-room-tool-'+[Guid]::NewGuid().ToString('N')+'.log')
try {
 Invoke-RoomTool $Node @('-e',"console.error('warning: fixture');console.log('complete')") $log
 if(!(Select-String -LiteralPath $log -Pattern 'complete' -Quiet)){throw 'Successful tool was interrupted by stderr'}
 $failed=$false
 try {Invoke-RoomTool $Node @('-e',"console.error('ERROR: fixture input');process.exit(7)") $log} catch {$failed=$_.Exception.Message.Contains('exit 7') -and $_.Exception.Message.Contains('fixture input')}
 if(!$failed){throw 'Nonzero exit was not reported with compiler detail'}
 Write-Output 'PASS: native stderr warnings are retained; nonzero exit remains fatal with detail'
} finally {Remove-Item -LiteralPath $log -ErrorAction SilentlyContinue}
