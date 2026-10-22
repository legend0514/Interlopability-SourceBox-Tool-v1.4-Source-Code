"""Build an unpublished runtime candidate from an explicit code-only allowlist.

Game archives, imported world geometry, extracted models and personal settings
are never swept into this package. The original test room must have no pak data.
"""
import hashlib
import json
from pathlib import Path
import re
import shutil
import struct
import sys
import uuid
import zipfile

launcher = Path(__file__).resolve().parent
project = launcher.parent / 'sourcebox'
workspace = launcher.parent.parent
stage = workspace / 'work' / 'beta-runtime' / uuid.uuid4().hex
core = stage / 'sourcebox'
sdk = stage / 'sdk'
stage.mkdir(parents=True)


def copy(source, target):
    if not source.is_file() or source.is_symlink():
        raise ValueError(f'Missing or linked input: {source}')
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)


# Follow static relative imports; do not include development or launch scripts.
pending = ['core.mjs', 'gmod-bridge.mjs', 'preflight.mjs']
included = set()
while pending:
    relative = pending.pop()
    if relative in included:
        continue
    if '/' in relative or '\\' in relative or not relative.endswith('.mjs'):
        raise ValueError(f'Unexpected runtime import: {relative}')
    source = project / relative
    text = source.read_text(encoding='utf-8-sig')
    if re.search(r'(?<![A-Za-z0-9_])[A-Za-z]:[/\\]', text):
        raise ValueError(f'Machine path in runtime code: {relative}')
    included.add(relative)
    copy(source, core / relative)
    pending.extend(re.findall(r"from\s+['\"]\./([^'\"]+)['\"]", text))


def scrub(value):
    if isinstance(value, dict):
        return {k: ('Not tested in the portable beta' if k == 'acceptance' else scrub(v)) for k, v in value.items()}
    if isinstance(value, list):
        return [scrub(v) for v in value]
    if isinstance(value, str) and re.match(r'^[A-Za-z]:[/\\]', value):
        # These are provenance hints only. Setup resolves actual installed paths.
        return 'installed-content/' + value.replace('\\', '/').split('/')[-1]
    return value


manifest = json.loads((project / 'sheets/connectivity.json').read_text())
sheet_names = {'room-upgrades.json', 'connectivity.json', 'cs2-scene-assets.json', 'cs2-models.json', *manifest['sheets'].values()}
for name in sorted(sheet_names):
    data = scrub(json.loads((project / 'sheets' / name).read_text()))
    if name == 'combat.json':
        data['ragdolls']['enabled'] = False
    if name == 'test-cases.json':
        for test in data:
            test['status'] = 'not-run-portable-beta'
    if name in ('worlds.json', 'worlds-expanded.json', 'map-travel.json'):
        data['enabled'] = False
    if name == 'worlds.json':
        data['nativeGeometry'] = False
    (core / 'sheets').mkdir(exist_ok=True)
    (core / 'sheets' / name).write_text(json.dumps(data, indent=2), encoding='utf-8')
(core / 'sheets/native-world-hosts.json').write_text('{}', encoding='utf-8')

for game in ('hl2', 'tf2', 'hl1mp', 'gmod', 'portal2'):
    bsp = project / 'maps' / game / 'sourcebox_room_001.bsp'
    data = bsp.read_bytes()
    if data[:4] != b'VBSP':
        raise ValueError('Invalid room BSP')
    for lighting_lump in (8, 53):
        if struct.unpack_from('<ii', data, 8 + lighting_lump * 16)[1] <= 0:
            raise ValueError(f'Missing LDR/HDR room lighting: {game}')
    # Lump 40 is the embedded pak archive. Even empty ZIP metadata is reviewed.
    offset, length = struct.unpack_from('<ii', data, 8 + 40 * 16)
    if length:
        import io
        with zipfile.ZipFile(io.BytesIO(data[offset:offset + length])) as pak:
            expected = {'materials/maps/sourcebox_room_001/cubemapdefault.vtf', 'materials/maps/sourcebox_room_001/cubemapdefault.hdr.vtf'}
            if not set(pak.namelist()).issubset(expected):
                raise ValueError(f'Unexpected embedded assets in {game} room: {pak.namelist()}')
        # Remove the embedded default cubemaps from the distributed copy. Native
        # game materials remain references to the recipient's installed content.
        patched = bytearray(data)
        patched[offset:offset + length] = b'\0' * length
        struct.pack_into('<ii', patched, 8 + 40 * 16, 0, 0)
        data = bytes(patched)
    target = core / 'maps' / game / bsp.name
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)

for relative in ('bin/sourcebox_hl1mp.dll', 'bin/sourcebox_portal2.dll',
                 'bin/sourcebox_preview.dll', 'bin/sdk/sourcebox_hl2.dll',
                 'bin/sdk/sourcebox_tf2.dll',
                 'adapters/tf2/sourcebox_portals.nut',
                 'adapters/hl1mp/sourcebox_prop_portals.nut',
                 'adapters/cs2/bin/Release/net10.0/SourceboxCS2.dll',
                 'adapters/cs2/bin/Release/net10.0/SourceboxCS2.deps.json'):
    copy(project / relative, core / relative)
for file in (project / 'adapters/gmod/lua').rglob('*.lua'):
    copy(file, core / file.relative_to(project))
for name in ('prepare-portable-tf-assets.cjs', 'prepare-portable-hl1-assets.cjs', 'prepare-portable-portal-assets.mjs', 'prepare-portable-p2-peers.py', 'prepare-portable-chell-input.cjs', 'prepare-portable-cs2-room.ps1', 'prepare-portable-room-materials.cjs', 'cs2-room-native.cjs', 'prepare-portable-cs2-models.cjs', 'prepare-portable-cs2-textures.py', 'prepare-portable-cs2-portal-surfaces.py', 'installed-assets.cjs'):
    copy(project / 'tools' / name, core / 'tools' / name)
copy(project / 'maps/cs2/sourcebox_room_001.vmf', core / 'maps/cs2/sourcebox_room_001.vmf')
for game in ('hl2','tf2','portal2'):
    copy(project / f'maps/{game}/sourcebox_room_001.vmf', core / f'maps/{game}/sourcebox_room_001.vmf')
for name in ('prepare-portable-worlds.py','combine-source-worlds.py','export-worlds.py','verify-world-lighting.py','cs2-worlds-portable.cjs','verify-portable-worlds.mjs'):
    copy(project / 'tools' / name, core / 'tools' / name)
# First-party ring geometry from tools/make-portal-ring.mjs, not a Valve model.
for suffix in ('.mdl', '.vvd', '.dx90.vtx', '.dx80.vtx', '.sw.vtx'):
    relative = 'models/sourcebox/portal2/portal_ring' + suffix
    copy(workspace / 'work/runtime-assets/hl1mp' / relative, core / 'shared' / relative)
relative = 'materials/sourcebox/portal2/portal_ring.vmt'
copy(workspace / 'work/runtime-assets/hl1mp' / relative, core / 'shared' / relative)
copy(project / 'tools/make-portal-ring.mjs', core / 'setup/first-party-ring-source.mjs')
# Only our procedural placeholder models are distributable here.
for file in (project / 'fallback').rglob('*'):
    if file.is_file() and (file.suffix in {'.mdl', '.vvd', '.vtx', '.vmt', '.vtf'} or file.name in {'aliases.json','PROVENANCE.txt'}):
        copy(file, core / 'fallback' / file.relative_to(project / 'fallback'))
copy(project / 'tools/build-fallback-models.py',core / 'setup/build-fallback-models.py')
requirements = [
    {'Game': 'portal2', 'Path': 'models/sourcebox/portal2/peers/' + name + '.mdl',
     'Description': 'Locally converted Portal 2 peer: ' + name}
    for name in ('heavy', 'soldier', 'gmod')
]
for model in ('hl2/player', 'tf2/heavy/heavy', 'gmod/kleiner/kleiner', 'hl1mp/gordon',
              'portal2/chell/chell', 'portals/world/world', 'portals/view/view',
              'portals/ring/ring', 'portals/monitor/monitor'):
    requirements.append({'Game': 'cs2', 'Path': 'models/sourcebox/' + model + '.vmdl_c',
                         'Description': 'Locally converted CS2 content: ' + model})
(core / 'setup/local-content-requirements.json').write_text(json.dumps(requirements, indent=2), encoding='utf-8')
copy(workspace / 'work/sourcebox-sdk/game/mod_hl2/scripts/weapon_sourcebox_portalgun.txt', core / 'shared/scripts/weapon_sourcebox_portalgun.txt')
for game, mod in (('hl2', 'mod_hl2'), ('tf2', 'mod_tf')):
    copy(workspace / 'work/sourcebox-sdk/game' / (mod + '_win64.exe'), sdk / (mod + '_win64.exe'))
    for name in ('client.dll', 'server.dll'):
        copy(workspace / 'work/sourcebox-sdk/game' / mod / 'bin/x64' / name,
             sdk / mod / 'bin/x64' / name)
# Stock public SDK UI overrides must accompany its client DLL. Retail TF2's
# interfaces alone are not the complete SDK mod resource set.
for relative in ('resource/mod_tf_english.txt', 'resource/ui/CharInfoLoadoutSubPanel.res',
                 'resource/ui/CharInfoPanel.res', 'resource/ui/MatchMakingDashboard.res'):
    copy(workspace / 'work/sourcebox-sdk/game/mod_tf' / relative, sdk / 'mod_tf' / relative)
for name in ('LICENSE', 'thirdpartylegalnotices.txt'):
    copy(workspace / 'work/sourcebox-sdk' / name, stage / 'licenses' / ('SOURCE-SDK-' + name + '.txt'))

(stage / 'README.txt').write_text('''SOURCEBOX RUNTIME CANDIDATE - NOT RELEASED

Contains the synchronization service, first-party adapters, modified SDK game
code and the original Shared Room geometry. Source SDK notices are in licenses/.
Distribution of the SDK-derived code must be free of charge.

No extracted Valve models, materials, maps or Workshop addon content is included.
The imported multiverse maps are disabled in this candidate configuration only.
Your existing configured runtime is unchanged.

This is not a complete installer. Local asset preparation, third-party loaders,
Node runtime acquisition and clean-PC acceptance are required before release.
Do not upload this candidate as a working public beta.
''', encoding='utf-8')
entries = []
for file in sorted(stage.rglob('*')):
    if file.is_file():
        entries.append({'Path': file.relative_to(stage).as_posix(),
                        'Sha256': hashlib.sha256(file.read_bytes()).hexdigest()})
(stage / 'runtime-manifest.json').write_text(json.dumps(entries, indent=2), encoding='utf-8')
archive = stage.parent / ('sourcebox-runtime-candidate-' + stage.name + '.zip')
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
    for file in sorted(stage.rglob('*')):
        if file.is_file():
            bundle.write(file, file.relative_to(stage).as_posix())
print(json.dumps({'stage': str(stage), 'archive': str(archive), 'bytes': archive.stat().st_size,
                  'files': len(entries), 'sha256': hashlib.sha256(archive.read_bytes()).hexdigest()}, indent=2))
