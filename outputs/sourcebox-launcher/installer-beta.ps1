param([switch]$CheckOnly,[switch]$DependenciesOnly)
$ErrorActionPreference='Stop'
function Find-DesktopRuntime {
 # Setup can be started by a 32-bit shell. Read the 64-bit install registry
 # explicitly rather than treating Program Files (x86) as the runtime root.
 $registered=$null
 $registry=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry64)
 try{$key=$registry.OpenSubKey('SOFTWARE\dotnet\Setup\InstalledVersions\x64');if($key){try{$registered=$key.GetValue('InstallLocation')}finally{$key.Dispose()}}}finally{$registry.Dispose()}
 foreach($root in @($env:DOTNET_ROOT_X64,$env:DOTNET_ROOT,$registered,"$env:ProgramW6432/dotnet","$env:ProgramFiles/dotnet")){
  if(!$root){continue};$exe=Join-Path $root 'dotnet.exe'
  if(Test-Path -LiteralPath $exe){
   $stream=[IO.File]::OpenRead($exe);$reader=New-Object IO.BinaryReader($stream)
   try{$stream.Position=0x3c;$offset=$reader.ReadInt32();$stream.Position=$offset+4;$machine=$reader.ReadUInt16()}finally{$reader.Dispose();$stream.Dispose()}
   if($machine -ne 0x8664){continue}
   $versions=& $exe --list-runtimes 2>$null;if($LASTEXITCODE -eq 0 -and ($versions -match '^Microsoft.WindowsDesktop.App 8\.0\.')){return $true}
  }
 }
 return $false
}
function Find-Node {
 $paths=@("$env:ProgramFiles/nodejs/node.exe")
 $command=Get-Command node.exe -ErrorAction SilentlyContinue
 if($command){$paths+=$command.Source}
 foreach($exe in $paths){if(Test-Path -LiteralPath $exe){$version=& $exe -p 'process.versions.node' 2>$null;if($LASTEXITCODE -eq 0 -and $version -match '^(\d+)\.' -and [int]$Matches[1] -ge 22){return $exe}}}
 return $null
}
function Install-Dependency([string]$Id,[string]$Fallback){
 if(!(Get-Command winget.exe -ErrorAction SilentlyContinue)){
  Write-Host "Windows Package Manager is unavailable. Install the prerequisite from $Fallback then run setup again."
  Start-Process $Fallback
  throw 'Required dependency is not installed.'
 }
 & winget.exe install --id $Id --exact --source winget --architecture x64 --accept-source-agreements --accept-package-agreements
 if($LASTEXITCODE -ne 0){throw "Dependency installation failed: $Id. Complete the installer or try again."}
}
try{
 $desktop=Find-DesktopRuntime;$node=Find-Node
 if($CheckOnly){[pscustomobject]@{DesktopRuntime=$desktop;NodeAvailable=[bool]$node;RuntimePayload=(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'runtime/sourcebox/core.mjs'))}|ConvertTo-Json;exit 0}
 if(!$desktop){Write-Host 'SourceBox requires Microsoft .NET Desktop Runtime 8 (x64). The standard runtime, x86 edition and other major versions do not satisfy this requirement. Installing the required desktop runtime...';Install-Dependency 'Microsoft.DotNet.DesktopRuntime.8' 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0';if(!(Find-DesktopRuntime)){throw 'Microsoft .NET Desktop Runtime 8 (x64) was not detected after installation. Restart setup when that installation finishes.'}}
 if(!$node){Write-Host 'Installing Node.js LTS...';Install-Dependency 'OpenJS.NodeJS.LTS' 'https://nodejs.org/en/download';$node=Find-Node;if(!$node){throw 'Restart setup after the Node.js installation finishes.'}}
 $env:PATH=(Split-Path -Parent $node)+[IO.Path]::PathSeparator+$env:PATH
 if($DependenciesOnly){Write-Host 'SourceBox prerequisites are ready.';exit 0}
 $app=Join-Path $PSScriptRoot 'SourceBox.exe'
 $process=Start-Process -FilePath $app -ArgumentList '--install' -Wait -PassThru
 if($process.ExitCode -ne 0){throw 'SourceBox installation failed.'}
 Write-Host 'SourceBox is installed. Open it from your desktop. Game-content preparation is a separate step inside the app.'
}catch{Write-Host $_.Exception.Message;exit 1}
