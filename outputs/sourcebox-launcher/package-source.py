"""Create a reviewable source snapshot without game content or personal files."""
from pathlib import Path
import hashlib, json, re, shutil, subprocess, zipfile

ROOT = Path(__file__).resolve().parents[2]
STAGE = ROOT / 'work/source-publication/sourcebox-beta-v1.5-source'
if STAGE.exists():
    raise SystemExit('Source staging directory already exists; review before rebuilding')
STAGE.mkdir(parents=True)
EXT = {'.cs', '.csproj', '.cpp', '.c', '.h', '.hpp', '.inc', '.lua', '.nut',
       '.mjs', '.cjs', '.js', '.py', '.ps1', '.json', '.vpc', '.fx', '.hlsl'}
changed = []
def digest(data): return hashlib.sha256(data).hexdigest()
def clean(text):
    text = re.sub(r'(?i)[a-z]:(?:[/\\]+)Users(?:[/\\]+)[^/\\\s"\']+', 'C:/SourceBox-dev', text)
    text = re.sub(r'\b765611\d{11}\b', '76561100000000000', text)
    return text
def copy(file, relative=None):
    relative = relative or file.relative_to(ROOT)
    out = STAGE / relative
    out.parent.mkdir(parents=True, exist_ok=True)
    raw = file.read_bytes()
    text = raw.decode('utf-8-sig')
    sanitized = clean(text)
    if sanitized != text: changed.append(str(relative).replace('\\', '/'))
    out.write_text(sanitized, encoding='utf-8')
def tree(folder):
    for file in folder.rglob('*'):
        if not file.is_file() or file.is_symlink(): continue
        if set(file.relative_to(folder).parts) & {'bin', 'obj', '__pycache__', 'node_modules', '.git'}: continue
        if file.suffix.lower() in EXT: copy(file)

launcher = ROOT / 'outputs/sourcebox-launcher'
project = ROOT / 'outputs/sourcebox'
tree(launcher / 'src')
for file in launcher.iterdir():
    if file.is_file() and file.suffix in {'.ps1', '.py'}: copy(file)
for name in ('sourcebox.ico', 'sourcebox-intro.mp4'):
    out = STAGE / 'outputs/sourcebox-launcher/src/Assets' / name
    out.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(launcher / 'src/Assets' / name, out)
for folder in ('adapters', 'test', 'tools', 'generated', 'sheets'):
    tree(project / folder)
copy(project / 'adapters/vendor/LICENSE.MIT')
for file in project.iterdir():
    if file.is_file() and file.suffix in {'.mjs', '.cjs', '.json', '.ps1'}: copy(file)
for game in ('hl2', 'tf2', 'portal2', 'cs2'):
    copy(project / 'maps' / game / 'sourcebox_room_001.vmf')

# Use the actual published configuration alongside development sheets.
release = launcher / 'release-candidates/sourcebox-beta-v1.5.zip'
with zipfile.ZipFile(release) as z:
    for name in z.namelist():
        if name.startswith('runtime/sourcebox/sheets/') and name.endswith('.json'):
            target = STAGE / 'release-config/sheets' / Path(name).name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(z.read(name))

shutil.copytree(STAGE / 'outputs/sourcebox/sheets', STAGE / 'development/sheets')
for file in (STAGE / 'release-config/sheets').glob('*.json'):
    shutil.copyfile(file, STAGE / 'outputs/sourcebox/sheets' / file.name)

sdk = ROOT / 'work/sourcebox-sdk'
head = subprocess.check_output(['git', '-C', str(sdk), 'rev-parse', 'HEAD'], text=True).strip()
patch = subprocess.check_output(['git', '-C', str(sdk), 'diff', '--no-ext-diff', '--binary', 'HEAD', '--', 'src/'])
(STAGE / 'sdk').mkdir()
(STAGE / 'sdk/sourcebox-sdk.patch').write_bytes(patch)
untracked = subprocess.check_output(['git', '-C', str(sdk), 'ls-files', '--others', '--exclude-standard', 'src/'], text=True).splitlines()
for name in untracked:
    if Path(name).suffix in {'.inc', '.vpc', '.cpp', '.h'}:
        copy(sdk / name, Path('sdk/overlay') / name)
for name in ('LICENSE', 'thirdpartylegalnotices.txt', 'README.md'):
    copy(sdk / name, Path('licenses/source-sdk') / name)

mit = '''MIT License

Copyright (c) 2026 SourceBox contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
'''
(STAGE / 'licenses/MIT-SourceBox.txt').write_text(mit)
(STAGE / 'LICENSE.txt').write_text('''SourceBox source availability and licensing

The original launcher C# code and engine-independent synchronization service
(top-level .mjs files and their tests) are offered under licenses/MIT-SourceBox.txt.
This does not relicense third-party code or assets.

sdk/sourcebox-sdk.patch, sdk/overlay and the native Source 1 adapters are subject
to the Source 1 SDK license. See licenses/source-sdk/LICENSE and the accompanying
third-party notices. SDK-derived distributions must be free of charge.
Other original SourceBox code is provided under the same Source 1 SDK terms
unless a file has its own license notice. This archive as a whole is NOT an
unrestricted MIT distribution or an OSI-open-source engine release.

nlohmann/json retains its included MIT license in adapters/vendor/LICENSE.MIT.
Separately acquired dependencies retain their own licenses. CounterStrikeSharp
uses GPLv3 with a plugin licensing exception; Metamod:Source uses zlib/libpng.
Their implementations are not bundled here.
Game and Workshop assets are not included or licensed by this source release.
The supplied launcher icon and intro are not covered by the code license.
''')
(STAGE / 'README.md').write_text('''# SourceBox beta v1.5 source

Source snapshot for reviewing the six-game crossplay prototype. This is source
code, not the playable download. Nothing runs merely by extracting this ZIP.

- `outputs/sourcebox-launcher/src`: desktop launcher and its automated tests.
- `outputs/sourcebox`: coordinator, adapters, portable v1.5 sheets and tools.
- `development/sheets`: sanitized expanded-world development configuration.
- `release-config/sheets`: the actual portable v1.5 configuration. Expanded
  worlds are disabled in that download.
- `sdk`: pinned Valve SDK patch and added source files.
- `BUILD.md`: build prerequisites and current reproducibility limits.
- `SECURITY.md`: installation behavior and review entry points.
- `release-provenance.json`: the corresponding download hash and SDK revision.
- `source-manifest.json`: hashes of the source snapshot files.

Personal logs, crash dumps, credentials, installed games and extracted game
assets are excluded. Personal developer paths were replaced with
`C:/SourceBox-dev`; sanitized file names are listed in the provenance file.
Those paths are placeholders to configure, not an installation requirement.

Source availability enables review. It does not certify that the program is
malware-free or establish that every distributed binary was built from this
snapshot. The full release has not passed an independent reproducible-build
comparison or fresh-PC gameplay acceptance. See LICENSE.txt before reuse.
''')
(STAGE / 'BUILD.md').write_text(f'''# Building and testing

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

Obtain https://github.com/ValveSoftware/source-sdk-2013 at commit `{head}`
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
''')
(STAGE / 'SECURITY.md').write_text('''# What to inspect

This is an unsigned experimental game mod. Review the code and build it yourself
where practical. Keep antivirus protections enabled. No malware-free guarantee
or independent security audit is claimed.

## Expected behavior

- The installer uses Windows Package Manager when .NET Desktop Runtime 8 x64
  or Node.js is missing. Installation may require Windows approval.
- The launcher detects Steam libraries and installs adapters/configuration in
  selected game folders. Review CrossplaySetup.cs, SdkConfiguration.cs,
  DesktopInstall.cs and installer-beta.ps1 before granting write access.
- Setup can download Metamod, CounterStrikeSharp, Blender and SourceIO from
  the URLs pinned in CsLoaderPreparation.cs and SetupDownloads.cs. Downloads
  use recorded checksums. Game model conversion reads installed game content.
- Runtime.cs and ServiceHost.cs start game and helper processes. OwnedProcess
  records process identity and may terminate owned processes when Stop is used.
- Core networking is local TCP on 127.0.0.1:27841; GMod uses a local bridge on
  27842. Games also use native local server ports. This is not internet matchmaking.
- GMod uses Lua, Source 1 uses native adapters/modified SDK game modules and
  CS2 uses a CounterStrikeSharp plugin through Metamod. Native preview code
  accesses game-rendering interfaces; review it as code running inside a game.
- GameWindowLayout.cs repositions windows and registers explicit focus hotkeys.
  This includes cursor-release calls; it is not a promise that all focus bugs
  are fixed.
- Settings, setup caches, process receipts and logs are written under the
  SourceBox local application-data directory. ConfigProtection.cs backs up
  selected native cfg files and attempts restoration after exit. It does not
  cover every Steam Cloud/userdata setting or recover from every interruption.
- Support export is user-triggered. It redacts common paths/identifiers but
  users should still inspect an export before sharing it.

## Not included in this source archive

No executable/DLL build outputs, game archives, extracted game models/textures,
Workshop addon implementations, personal logs, crash dumps, saves or settings.
The icon and short intro are the launcher's presentation assets.

## Reporting

Include version, affected game, reproduction steps and a redacted log excerpt.
Do not post Steam credentials, access tokens, save files or private crash dumps.
There is no authenticated vulnerability-reporting service configured in this
archive. Contact the person who distributed your copy privately for sensitive
reports.
''')
(STAGE / '.gitignore').write_text('**/bin/\n**/obj/\nwork/\nbuild/\n**/__pycache__/\n**/node_modules/\n*.mdmp\n*.dmp\n*.log\nsettings.json\n')
provenance = {'version':'1.5.0-beta.1','sdkRepository':'https://github.com/ValveSoftware/source-sdk-2013',
              'sdkCommit':head,'binaryZip':release.name,'binaryZipSha256':digest(release.read_bytes()),
              'independentReproducibleBuild':False,'independentSecurityAudit':False,
              'sanitizedFiles':sorted(changed),'sourceTests':'See source-validation.json when present'}
(STAGE / 'release-provenance.json').write_text(json.dumps(provenance, indent=2))
print(STAGE)
