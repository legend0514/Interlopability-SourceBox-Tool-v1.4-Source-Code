param([switch]$OpenClient)
$ErrorActionPreference='Stop'
& node "$PSScriptRoot/preflight.mjs"
if($LASTEXITCODE -ne 0){throw 'Preflight failed'}
$base='C:/Program Files (x86)/Steam/steamapps/common/Counter-Strike Global Offensive/game'
$room=Join-Path $base 'csgo/maps/sourcebox_room_001.vpk'
if(!(Test-Path -LiteralPath $room)){throw 'Shared room is not compiled/deployed; run build-cs2-room.ps1 first'}
$gameinfo="$base/csgo/gameinfo.gi"
$before=[IO.File]::ReadAllBytes($gameinfo)
$node=(Get-Command node).Source
if(!(Get-NetTCPConnection -LocalPort 27841 -State Listen -ErrorAction SilentlyContinue)){throw 'Start Sourcebox Core first'}
if(Get-NetUDPEndpoint -LocalPort 27019 -ErrorAction SilentlyContinue){throw 'CS2 test port already in use'}
# Only the dedicated-server startup sees this documented Metamod search path.
# Restore the exact original bytes even when startup fails.
try {
 $text=[Text.Encoding]::UTF8.GetString($before)
 if($text -match 'Game\s+csgo/addons/metamod'){throw 'Unexpected pre-existing Metamod configuration; inspect manually'}
 $text=$text -replace '(?m)^(\s*Game_LowViolence\s+csgo_lv.*)$',"`$1`r`n`t`t`tGame csgo/addons/metamod"
 [IO.File]::WriteAllText($gameinfo,$text)
 $p=Start-Process "$base/bin/win64/cs2.exe" -ArgumentList '-dedicated -insecure -condebug -ip 127.0.0.1 -port 27019 +sv_lan 1 +mp_autokick 0 +bot_quota 0 +game_type 0 +game_mode 0 +map sourcebox_room_001' -WorkingDirectory $base -WindowStyle Hidden -RedirectStandardOutput "$PSScriptRoot/logs/cs2-server.log" -RedirectStandardError "$PSScriptRoot/logs/cs2-server-errors.log" -PassThru
 $ready=$false
 for($i=0;$i -lt 45;$i++){Start-Sleep -Seconds 1;if($p.HasExited){throw 'CS2 server exited'};if(Select-String -Path "$PSScriptRoot/logs/cs2-server.log" -Pattern '[Sourcebox] Core connected' -SimpleMatch -Quiet){$ready=$true;break}}
 if(!$ready){Stop-Process -Id $p.Id -ErrorAction SilentlyContinue;throw 'CS2 adapter did not connect; inspect logs'}
}finally{[IO.File]::WriteAllBytes($gameinfo,$before)}
$record=@{serverPid=$p.Id;gameInfoTemporarilyPatched=$false}
if($OpenClient){$client=Start-Process "$base/bin/win64/cs2.exe" -ArgumentList '-insecure -multirun -windowed -noborder -w 640 -h 540 -x 640 -y 540 -novid -condebug +connect 127.0.0.1:27019' -WorkingDirectory $base -PassThru;$record.clientPid=$client.Id}
$record | ConvertTo-Json | Set-Content "$PSScriptRoot/logs/cs2-launch.json"


