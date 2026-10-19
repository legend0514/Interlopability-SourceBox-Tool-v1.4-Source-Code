param([string]$DotNet='dotnet')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$release=Join-Path $root ('releases/small-build-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $release | Out-Null
& $DotNet publish "$root/src/SourceBox.csproj" -c Release -r win-x64 --self-contained false -o $release --no-restore
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
@'
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install SourceBox.ps1"
if errorlevel 1 pause
'@ | Set-Content "$release/Install SourceBox.cmd" -Encoding ascii
@'
$ErrorActionPreference='Stop'
function HasDesktopRuntime {
 $roots=@("$env:ProgramFiles/dotnet",[Environment]::GetEnvironmentVariable('DOTNET_ROOT_X64'),[Environment]::GetEnvironmentVariable('DOTNET_ROOT'))
 foreach($root in $roots){
  if(!$root){continue}
  $hostPath=Join-Path $root 'dotnet.exe'
  if(Test-Path -LiteralPath $hostPath){
   $versions=& $hostPath --list-runtimes 2>$null
   if($LASTEXITCODE -eq 0 -and ($versions -match '^Microsoft.WindowsDesktop.App 8\.0\.')){return $true}
  }
 }
 return $false
}
try {
 if(!(HasDesktopRuntime)){
  Write-Host 'Installing Microsoft .NET Desktop Runtime 8 (x64)...'
  if(!(Get-Command winget.exe -ErrorAction SilentlyContinue)){
   Write-Host 'Install .NET 8 Desktop Runtime for Windows x64, then run this installer again.'
   Start-Process 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0'
   exit 1
  }
  & winget.exe install --id Microsoft.DotNet.DesktopRuntime.8 --exact --source winget --architecture x64 --accept-source-agreements --accept-package-agreements
  if(!(HasDesktopRuntime)){throw 'The runtime is not ready. Complete its installation, then run setup again.'}
 }
 $process=Start-Process -FilePath (Join-Path $PSScriptRoot 'SourceBox.exe') -ArgumentList '--install' -Wait -PassThru
 if($process.ExitCode -ne 0){throw 'SourceBox installation did not finish.'}
}catch{Write-Host $_.Exception.Message;exit 1}
'@ | Set-Content "$release/Install SourceBox.ps1" -Encoding utf8
@'
SOURCEBOX 0.1.1 - SMALL DOWNLOAD
Windows x64

Extract the complete ZIP, then run Install SourceBox.cmd.
It checks for Microsoft .NET Desktop Runtime 8 (x64) and installs it through
Windows Package Manager if needed. This requires internet access and may show
a Windows permission prompt. If Package Manager is unavailable, it opens the
Microsoft download page; install Desktop Runtime 8 x64 and run setup again.

The launcher, splash and icon are included. The Microsoft runtime is downloaded
separately instead of being bundled. This is not an offline installer.

Steam games are detected automatically. Crossplay still requires the separate
SourceBox runtime, prepared adapters and locally generated game assets. Those
are not included. This download is not a complete crossplay installer.

No personal settings, logs or proprietary game assets are included.
The app is unsigned. Existing SourceBox settings are preserved during updates.
Closing SourceBox leaves your games running.

Microsoft runtime instructions:
https://learn.microsoft.com/en-us/dotnet/core/install/windows
'@ | Set-Content "$release/READ ME FIRST.txt" -Encoding utf8
$files=Get-ChildItem -LiteralPath $release -Recurse -File
$manifest=@($files | Sort-Object FullName | ForEach-Object {
 @{Path=[IO.Path]::GetRelativePath($release,$_.FullName);Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
ConvertTo-Json -InputObject $manifest -Depth 3 | Set-Content "$release/package-manifest.json" -Encoding utf8NoBOM
$zip=Join-Path $root 'releases/SourceBox-0.1.1-windows-x64-small.zip'
Compress-Archive -Path "$release/*" -DestinationPath $zip -Force
if((Get-Item -LiteralPath $zip).Length -ge 25000000){throw 'Small download exceeds 25 MB'}
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content "$zip.sha256" -Encoding ascii
Write-Output $release
Write-Output $zip
