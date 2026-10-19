param(
 [string]$Sdk='C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/source-sdk-2013/src',
 [string]$BuildTools='C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/BuildTools'
)
$ErrorActionPreference='Stop'
& node "$PSScriptRoot/generate.mjs"
if($LASTEXITCODE -ne 0){throw 'Configuration preflight failed'}
$lines=@(
 "@call `"$BuildTools/VC/Auxiliary/Build/vcvarsall.bat`" x64 >nul",
 '@if errorlevel 1 exit /b 1',
 "cl /nologo /LD /O2 /MD /EHsc /std:c++17 /DCOMPILER_MSVC64 /DCOMPILER_MSVC /DWIN32 /D_WIN32 /DNDEBUG /D_CRT_SECURE_NO_WARNINGS /I`"$Sdk/public`" /I`"$Sdk/public/tier0`" /I`"$Sdk/public/tier1`" /I`"$Sdk/common`" /I`"$PSScriptRoot/generated`" adapters/hl1-preview.cpp /Fo`"bin/hl1-preview.obj`" /link /LIBPATH:`"$Sdk/lib/public/x64`" tier2.lib tier1.lib tier0.lib vstdlib.lib mathlib.lib /NODEFAULTLIB:LIBCMT /OUT:`"bin/sourcebox_preview.dll`"",
 '@exit /b %errorlevel%'
)
$lines|Set-Content "$PSScriptRoot/generated/build-hl1-preview.cmd"
Push-Location $PSScriptRoot
try{& $env:ComSpec /d /c 'generated\build-hl1-preview.cmd';if($LASTEXITCODE -ne 0){throw 'HL1 preview tool build failed'}}finally{Pop-Location}
