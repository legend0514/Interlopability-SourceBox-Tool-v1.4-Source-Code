param([string]$DotNet='dotnet',[string]$RuntimeNotices)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$version='0.1.1'
$release=Join-Path $root "releases/SourceBox-$version-windows-x64"
New-Item -ItemType Directory -Force $release | Out-Null
& $DotNet publish "$root/src/SourceBox.csproj" -c Release -r win-x64 --self-contained true -o $release --no-restore
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
if(!$RuntimeNotices -or !(Test-Path -LiteralPath "$RuntimeNotices/ThirdPartyNotices.txt")){throw 'Supply the .NET distribution folder with LICENSE.txt and ThirdPartyNotices.txt'}
Copy-Item -LiteralPath "$RuntimeNotices/LICENSE.txt" -Destination "$release/DOTNET-LICENSE.txt" -Force
Copy-Item -LiteralPath "$RuntimeNotices/ThirdPartyNotices.txt" -Destination "$release/DOTNET-NOTICES.txt" -Force
@'
@echo off
start "" /wait "%~dp0SourceBox.exe" --install
'@ | Set-Content "$release/Install SourceBox.cmd" -Encoding ascii
@'
SOURCEBOX 0.1.1
Windows x64

INSTALL
Extract the ZIP, then run Install SourceBox.cmd.
Open SourceBox from your desktop or Start menu.
To run without installing, open SourceBox.exe in this folder.

SETUP
This download contains the launcher. Crossplay requires a prepared SourceBox
installation, the games you own and their game integrations.
Steam and its additional libraries are scanned automatically on startup.
SourceBox detects installed games and repairs stale runtime paths when a local
or portable runtime is available. Games marked Available can be launched.
Automatic crossplay installation and missing-game content fallbacks are not
available yet. Installing the games alone does not install their adapters.

SHARE
Send the complete ZIP. In SourceBox, Settings > Export app ZIP makes a new copy
without your settings, logs or game files. The .sha256 file can be used to check
the download. This release is unsigned.

YOUR FILES
App: %LOCALAPPDATA%\Programs\SourceBox\app
Settings and logs: %LOCALAPPDATA%\SourceBox
Updates keep your settings and retain the previous app version.
Closing SourceBox leaves games running.

REMOVE
Close SourceBox, delete its Programs\SourceBox folder and remove its shortcuts.
Your settings can be removed separately from %LOCALAPPDATA%\SourceBox.

Includes the .NET runtime. Its license and third-party notices are included.
Games, game assets and the crossplay runtime are installed separately.

'@ | Set-Content "$release/READ ME FIRST.txt" -Encoding utf8
$files=Get-ChildItem -LiteralPath $release -Recurse -File | Where-Object { $_.Name -ne 'package-manifest.json' }
foreach($file in $files){
 if($file.Extension -notin @('.exe','.dll','.json','.mp4','.txt','.cmd','.ico')){throw "Unexpected release file: $($file.Name)"}
 if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked release file is not allowed'}
}
$manifest=@($files | Sort-Object FullName | ForEach-Object {
 @{Path=[IO.Path]::GetRelativePath($release,$_.FullName);Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
ConvertTo-Json -InputObject $manifest -Depth 3 | Set-Content "$release/package-manifest.json" -Encoding utf8NoBOM
$zip=Join-Path $root "releases/SourceBox-$version-windows-x64.zip"
Compress-Archive -Path "$release/*" -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content "$zip.sha256" -Encoding ascii
Write-Output $zip
