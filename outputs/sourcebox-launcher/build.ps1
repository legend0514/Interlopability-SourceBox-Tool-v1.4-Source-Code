param([string]$DotNet='dotnet')
$ErrorActionPreference='Stop'
& $DotNet publish "$PSScriptRoot/src/SourceBox.csproj" -c Release -r win-x64 --self-contained true -o "$PSScriptRoot/app"
if($LASTEXITCODE -ne 0){throw 'Launcher build failed'}
Write-Output "Run $PSScriptRoot/app/SourceBox.exe"
