# Building and testing

Keep this archive's directory layout. Start with Windows x64, a .NET 8 SDK,
Node.js and Python 3.13. The CS2 adapter targets .NET 10 separately. Game code
requires Visual Studio 2022 C++ tools and installed games/SDK tools.

## Launcher

From the archive root:

```powershell
dotnet build outputs/sourcebox-launcher/src/SourceBox.csproj -c Release
dotnet publish outputs/sourcebox-launcher/src/SourceBox.csproj -c Release -r win-x64 --self-contained false -o build/launcher
$env:SOURCEBOX_HOME = "$PWD/build/test-home"
New-Item -ItemType Directory -Force $env:SOURCEBOX_HOME
dotnet outputs/sourcebox-launcher/src/bin/Release/net8.0-windows/SourceBox.dll --self-test
```

Some launcher integration tests need a configured runtime/Node path. The full
game DLLs and converted assets are intentionally absent from this source ZIP.

## Coordinator

```powershell
cd outputs/sourcebox
node preflight.mjs
node --test test/core.test.mjs test/portal-math.test.mjs
```

The default sheets match the portable download. The expanded development
configuration is kept separately in `development/sheets`; it requires installed
and converted assets that this source archive deliberately omits. Other tests may require local SDK files, compiled
adapters or assets that are not distributed here.

## Valve SDK modifications

Obtain https://github.com/ValveSoftware/source-sdk-2013 at commit `be57e3d01ac7d7aa6a2cacbfef187c0b46616db0`
in `work/sourcebox-sdk`. From that checkout apply the absolute path to
`sdk/sourcebox-sdk.patch` with `git apply --check` then `git apply`. Copy the
contents of `sdk/overlay` into the checkout. Retain Valve's license and notices.
The upstream build instructions are included at `licenses/source-sdk/README.md`.

Generate Visual Studio solutions using the SDK's VPC tools. The included
development build scripts reference `sourcebox-hl2.sln` and `sourcebox-tf2.sln`;
these are generated build files, not included binaries. Configure local tool
paths and output directories before using those scripts.

## Native and CS2 adapters

Review `outputs/sourcebox/build.ps1`, `build-portal2.ps1` and
`build-hl1-preview.ps1`. They accept SDK/tool paths and run configuration checks.
For CS2, obtain the CounterStrikeSharp API version referenced in
`CsLoaderPreparation.cs`, supply its DLL at the project HintPath and build
`outputs/sourcebox/adapters/cs2/SourceboxCS2.csproj` with a .NET 10 SDK.
GMod adapter Lua files are source and need no native compilation.

## Full package

`outputs/sourcebox-launcher/package-beta.ps1` and `build-beta-runtime.py` record
the packaging process and asset allowlist. They require the separately built
SDK binaries, adapter binaries, first-party fallback models and compiled Shared
Room BSPs at their expected paths. These generated artifacts are omitted here.
Tools for generating them are included, but the entire six-game build is not
yet a verified one-command clean-machine build. Original Shared Room VMFs are
included; imported commercial maps and Workshop models are not.

The public binary's SHA-256 is recorded for comparison of downloads, not as
proof of source/binary equivalence. Do not claim reproducible builds until an
independent full rebuild has been compared.
