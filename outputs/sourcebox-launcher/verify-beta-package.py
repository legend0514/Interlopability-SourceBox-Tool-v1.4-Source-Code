"""Audit the distributable without launching or modifying installed games."""
import hashlib
import json
from pathlib import PurePosixPath
import struct
import sys
import zipfile

with zipfile.ZipFile(sys.argv[1]) as archive:
    names = archive.namelist()
    files = [name for name in names if not name.endswith('/')]
    normalized = [name.replace('\\', '/') for name in files]
    assert len(set(name.casefold() for name in normalized)) == len(files), 'Duplicate archive paths'
    for name in normalized:
        path = PurePosixPath(name)
        assert not path.is_absolute() and '..' not in path.parts and ':' not in name, name
    entries = json.loads(archive.read('package-manifest.json').decode('utf-8-sig'))
    manifest = {entry['Path'].replace('\\', '/'): entry['Sha256'].lower() for entry in entries}
    assert len(entries) == len(manifest), 'Duplicate manifest paths'
    assert set(normalized) == set(manifest) | {'package-manifest.json'}, 'Unmanifested files'
    first_party = 'runtime/sourcebox/shared/'
    allowed_art = {first_party + 'models/sourcebox/portal2/portal_ring' + suffix
                   for suffix in ('.mdl', '.vvd', '.dx90.vtx', '.dx80.vtx', '.sw.vtx')}
    allowed_art.add(first_party + 'materials/sourcebox/portal2/portal_ring.vmt')
    fallback='runtime/sourcebox/fallback/'
    aliases=json.loads(archive.read(fallback+'aliases.json'))
    for model in list(aliases)+['models/sourcebox/fallback/person.mdl','models/sourcebox/fallback/tool.mdl']:
        for suffix in ('.mdl','.vvd','.dx90.vtx','.dx80.vtx','.sw.vtx'):
            allowed_art.add(fallback+model[:-4]+suffix)
    allowed_art.add(fallback+'materials/sourcebox/fallback/fallback.vmt')
    allowed_art.add(fallback+'materials/sourcebox/fallback/cyan.vtf')
    for original, name in zip(files, normalized):
        data = archive.read(original)
        if name in manifest:
            assert hashlib.sha256(data).hexdigest() == manifest[name], 'Hash mismatch: ' + name
        assert PurePosixPath(name).name.lower() not in {'settings.json', 'asset-setup.log', 'setup-check.json'}, name
        suffix = PurePosixPath(name).suffix.lower()
        if suffix in {'.mdl', '.vvd', '.vtx', '.vmt', '.vtf', '.gma', '.blend', '.vpk', '.pdb'}:
            assert name in allowed_art, 'Unreviewed asset: ' + name
        if suffix == '.bsp':
            assert data[:4] == b'VBSP' and struct.unpack_from('<ii', data, 8 + 40 * 16) == (0, 0), name
            assert all(struct.unpack_from('<ii', data, 8 + index * 16)[1] > 0 for index in (8, 53)), 'Missing room lighting: ' + name
    readiness = json.loads(archive.read('release-readiness.json').decode('utf-8-sig'))
    assert readiness['publishable'] is False, 'Fresh-PC acceptance is still outstanding'
print(json.dumps({'archiveIntegrity': 'passed', 'distributionAllowlist': 'passed',
                  'files': len(files), 'freshPcGameplay': 'not-tested', 'publishable': False}, indent=2))
