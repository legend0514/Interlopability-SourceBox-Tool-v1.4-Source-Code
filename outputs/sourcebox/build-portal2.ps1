$ErrorActionPreference='Stop'
& node "$PSScriptRoot/generate.mjs"
if($LASTEXITCODE -ne 0){throw 'Preflight failed'}
$sdk='C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/source-sdk-2013/src'
$vc='C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/BuildTools/VC/Auxiliary/Build/vcvarsall.bat'
$log="$PSScriptRoot/logs/portal2-adapter.log".Replace('\','/')
$lines=@("@call `"$vc`" x86 >nul",'@if errorlevel 1 exit /b 1',"cl /nologo /LD /O2 /MD /EHsc /std:c++17 /DCOMPILER_MSVC32 /DCOMPILER_MSVC /DWIN32 /D_WIN32 /DNDEBUG /D_CRT_SECURE_NO_WARNINGS /DSOURCEBOX_GAME=\`"portal2\`" /DSOURCEBOX_PORTAL2_LOG=\`"$log\`" /I`"$sdk/public`" /I`"$sdk/public/tier0`" /I`"$sdk/public/tier1`" /I`"$PSScriptRoot/generated`" adapters/portal2/adapter.cpp /Fo`"bin/portal2.obj`" /link ws2_32.lib /OUT:`"bin/sourcebox_portal2.dll`"",'@exit /b %errorlevel%')
$lines | Set-Content -Encoding ascii "$PSScriptRoot/generated/build-portal2.cmd"
Push-Location $PSScriptRoot
try{& $env:ComSpec /d /c generated\build-portal2.cmd;if($LASTEXITCODE -ne 0){throw 'Portal 2 adapter build failed'}}finally{Pop-Location}

