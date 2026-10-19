param(
 [string]$Sdk='C:/SourceBox-dev\Documents\Codex\2026-10-10\make\work\source-sdk-2013\src',
 [string]$BuildTools='C:/SourceBox-dev\Documents\Codex\2026-10-10\make\work\BuildTools',
 [switch]$SdkMods
)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
 & node generate.mjs
 if($LASTEXITCODE -ne 0){throw 'Configuration preflight failed'}
 & node verify-assets.mjs
 if($LASTEXITCODE -ne 0){throw 'Installed asset preflight failed'}
 & node verify-model-aliases.cjs
 if($LASTEXITCODE -ne 0){throw 'Shared model identity preflight failed'}
 if((Get-Content sheets/portals.json -Raw | ConvertFrom-Json).enabled){
  & node tools/verify-portal-ring.mjs
  if($LASTEXITCODE -ne 0){throw 'Portal ring geometry preflight failed'}
 }
 $phase=(Get-Content sheets/connectivity.json -Raw | ConvertFrom-Json).phase
 $source=if($phase -eq 'A'){'connectivity.cpp'}else{'movement.cpp'}
 New-Item -ItemType Directory -Force bin | Out-Null
 $targets=if($SdkMods){@('hl2','tf2')}else{@('hl2','tf2','hl1mp')}
 if($SdkMods){New-Item -ItemType Directory -Force bin/sdk | Out-Null}
 foreach($game in $targets) {
  $arch=if($game -eq 'hl2' -and !$SdkMods){'x86'}else{'x64'}
  $outputDir=if($SdkMods){'bin\sdk'}else{'bin'}
  $compiler=if($arch -eq 'x86'){'COMPILER_MSVC32'}else{'COMPILER_MSVC64'}
  if($SdkMods){$compiler+=' /DSOURCEBOX_SDK'}
  $lines=@(
   "@call `"$BuildTools\VC\Auxiliary\Build\vcvarsall.bat`" $arch >nul",
   '@if errorlevel 1 exit /b 1',
   "cl /nologo /LD /O2 /MD /EHsc /std:c++17 /D$compiler /DCOMPILER_MSVC /DWIN32 /D_WIN32 /DNDEBUG /D_CRT_SECURE_NO_WARNINGS /DSOURCEBOX_GAME=\`"$game\`" /I`"$Sdk\public`" /I`"$Sdk\public\tier0`" /I`"$Sdk\public\tier1`" /I`"$Sdk\common`" /I`"$PSScriptRoot\generated`" adapters\$source /Fo`"$outputDir\$game.obj`" /link ws2_32.lib /OUT:`"$outputDir\sourcebox_$game.dll`"",
   '@exit /b %errorlevel%'
  )
  $lines | Set-Content -Encoding ascii "generated\build-$game.cmd"
  & $env:ComSpec /d /c "generated\build-$game.cmd"
  if($LASTEXITCODE -ne 0){throw "Build failed: $game"}
 }
} finally {Pop-Location}




