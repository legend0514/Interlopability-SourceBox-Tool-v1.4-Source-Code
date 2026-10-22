param([Parameter(Mandatory=$true)][string]$DotNet,[Parameter(Mandatory=$true)][string]$Python,[ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$CandidateName='sourcebox-beta-v1.5.3',[switch]$ReleaseApproved)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$runtimeJson=& $Python "$root/build-beta-runtime.py"
if($LASTEXITCODE -ne 0){throw 'Runtime candidate build failed'}
$runtime=$runtimeJson|ConvertFrom-Json
Push-Location (Join-Path $runtime.stage 'sourcebox')
try {
 & node preflight.mjs
 if($LASTEXITCODE -ne 0){throw 'Configuration preflight failed; package build stopped'}
} finally { Pop-Location }
$stage=Join-Path $root ('release-candidates/build-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
& $DotNet publish "$root/src/SourceBox.csproj" -c Release -r win-x64 --self-contained false -o $stage --no-restore
if($LASTEXITCODE -ne 0){throw 'Launcher publish failed'}
Copy-Item -LiteralPath $runtime.stage -Destination "$stage/runtime" -Recurse
$testHome=Join-Path $root ('../../work/package-checks/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $testHome | Out-Null
@{ProjectRoot="$stage/runtime/sourcebox";SdkRoot="$stage/runtime/sdk";NodePath=(Get-Command node).Source}|ConvertTo-Json|Set-Content "$testHome/settings.json" -Encoding utf8NoBOM
$priorHome=$env:SOURCEBOX_HOME
$priorDotnet=$env:DOTNET_ROOT_X64
try {
 $env:SOURCEBOX_HOME=[IO.Path]::GetFullPath($testHome)
 $env:DOTNET_ROOT_X64=Split-Path ([IO.Path]::GetFullPath($DotNet))
 $translation=Start-Process "$stage/SourceBox.exe" -ArgumentList '--translation-check' -WindowStyle Hidden -PassThru -Wait
 if($translation.ExitCode -ne 0){throw 'Translation checks failed'}
 $check=Start-Process "$stage/SourceBox.exe" -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
 if($check.ExitCode -ne 0){throw "Launcher tests failed. Details: $testHome/self-tests.txt"}
 Copy-Item "$testHome/self-tests.txt" "$root/evidence/package-self-tests.txt" -Force
} finally {
 $env:SOURCEBOX_HOME=$priorHome
 $env:DOTNET_ROOT_X64=$priorDotnet
}
Copy-Item -LiteralPath "$root/installer-beta.ps1" -Destination "$stage/Install SourceBox.ps1"
@'
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install SourceBox.ps1"
if errorlevel 1 pause
'@ | Set-Content "$stage/Install SourceBox.cmd" -Encoding ascii
@'
SOURCEBOX BETA v1.5.3 - SHARED ROOM
Windows x64. Experimental beta. See release-readiness.json for publication status.

Extract the whole ZIP and run Install SourceBox.cmd.
Setup installs .NET Desktop Runtime 8 and Node.js through Windows Package
Manager if missing. Internet and Windows installation approval may be needed.
Open SourceBox from the desktop and select installed games under Characters.
Games tile across the monitor containing SourceBox. Ctrl+Alt+Space releases
the mouse to the launcher. Ctrl+Alt+Right switches to the next game.

Changes in 1.5.3: portable destination preparation, disk-space checks before
installation, clearer CS2 compiler diagnostics and recovery from late connections.
Use Prepare destinations in the Destinations tab with all game sessions stopped.
Preparation needs installed maps from GMod, TF2, Portal 2 and HL Deathmatch: Source,
native map compilers and extra disk space. CS2 requires Workshop Tools.
The combined GMod host and a CS2 chamber smoke test compiled locally. Native
gameplay of these new hosts and a full CS2 destination build are not verified.
External TF2 class-selection and Portal 2 startup crashes remain under investigation.
Ragdolls remain disabled in this public beta.

Included: launcher, synchronization service, six adapter implementations,
modified HL2/TF2 SDK binaries and original Shared Room geometry.
No commercial game assets or Workshop addon content are included.

HL2 and TF2 require Source SDK Base 2013 Multiplayer (Steam app 243750).
Only installed games can run. Setup uses each game's supported local loaders.
Sessions are private and local to one PC; this is not internet matchmaking.

First-run setup extracts installed Source 1 content and converts Portal 2 peers
and core CS2 models locally. CS2 requires its Workshop Tools installed in Steam.
Conversion tools and missing CS2 loaders are downloaded from their upstream
projects and verified by checksum. First-run downloads can exceed the ZIP size
substantially and conversion needs additional disk space.
The Portal Gun addon must already be installed through Steam Workshop:
https://steamcommunity.com/sharedfiles/filedetails/?id=1800764828
A missing dependency
blocks launch rather than being reported as ready. The expanded imported worlds
are not included in this Shared Room candidate.

Fresh-PC gameplay acceptance has not passed. This is an experimental public beta,
not a guarantee of compatibility on every PC. Existing configured installations
retain their settings and maps. Back up your configuration before upgrading.
Missing character sources use original cyan debug figures. Portal 2 no longer
requires TF2, GMod or HL1 just to prepare its peer models. Real converted models
take priority. Placeholders are static and are not finished character integrations.
Expanded-map topology fallback and every reduced-install CS2 combination remain
unverified. Source SDK Base tools are required for CS2 conversion without HL2.

Source SDK license and third-party notices are in runtime/licenses.
SDK-derived distribution must be free of charge. SourceBox is unsigned.
'@ | Set-Content "$stage/READ ME FIRST.txt" -Encoding utf8
@{publishable=[bool]$ReleaseApproved;publicationApproved=[bool]$ReleaseApproved;version='1.5.3-beta.1';status=$(if($ReleaseApproved){'public-beta'}else{'candidate'});scope='shared-room-and-experimental-local-destination-preparation';freshPcGameplay='not-tested';gameAssetPreparation='partial';supportedGames=@('hl2','tf2','hl1mp','gmod','portal2','cs2');missingCharacterFallback='original-static-figures';reducedInstallGameplay='not-tested'}|ConvertTo-Json|Set-Content "$stage/release-readiness.json" -Encoding utf8
$entries=@(Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName | ForEach-Object {@{Path=[IO.Path]::GetRelativePath($stage,$_.FullName);Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
ConvertTo-Json -InputObject $entries -Depth 3 | Set-Content "$stage/package-manifest.json" -Encoding utf8NoBOM
$zip=Join-Path $root ("release-candidates/"+$CandidateName+".zip")
Compress-Archive -Path "$stage/*" -DestinationPath $zip -Force
if((Get-Item $zip).Length -ge 25000000){throw 'Candidate exceeds the 25 MB upload limit'}
& $Python "$root/verify-beta-package.py" $zip | Set-Content "$zip.audit.json" -Encoding utf8
if($LASTEXITCODE -ne 0){throw 'Package integrity or distribution audit failed'}
"$((Get-FileHash $zip).Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zip))"|Set-Content "$zip.sha256" -Encoding ascii
@{stage=$stage;zip=$zip;bytes=(Get-Item $zip).Length;publishable=[bool]$ReleaseApproved}|ConvertTo-Json
