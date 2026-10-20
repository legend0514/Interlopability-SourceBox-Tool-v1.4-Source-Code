"""Audit and zip the prepared source snapshot; never sweep build outputs in."""
from pathlib import Path
import hashlib, json, re, zipfile

ROOT = Path(__file__).resolve().parents[2]
stage = ROOT / 'work/source-publication/sourcebox-beta-v1.5-source'
allowed = {'.cs', '.csproj', '.cpp', '.c', '.h', '.hpp', '.inc', '.lua', '.nut',
           '.mjs', '.cjs', '.js', '.py', '.ps1', '.json', '.vpc', '.fx', '.hlsl',
           '.vmf', '.md', '.txt', '.patch', '.ico', '.mp4', '.mit'}
def digest(data): return hashlib.sha256(data).hexdigest()
files = []
for file in sorted(stage.rglob('*')):
    if not file.is_file(): continue
    relative = file.relative_to(stage)
    if set(relative.parts) & {'bin', 'obj', '__pycache__', 'node_modules', '.git'}: continue
    if file.name == 'source-manifest.json': continue
    if file.suffix.lower() not in allowed and file.name not in ('LICENSE', '.gitignore'):
        raise ValueError('Unexpected source archive file: ' + str(relative))
    data = file.read_bytes()
    if data[:2] == b'MZ': raise ValueError('Executable hidden in source archive')
    if file.suffix not in ('.ico', '.mp4'):
        text = data.decode('utf-8-sig')
        private = r'(?i)[a-z]:[/\\]+Users[/\\]+|\bmicha\b|-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----|ghp_[a-zA-Z0-9]{30}|github_pat_[a-zA-Z0-9_]{30}|sk-proj-[a-zA-Z0-9_-]{30}|AKIA[A-Z0-9]{16}'
        if re.search(private, text): raise ValueError('Privacy review required: ' + str(relative))
    files.append((relative.as_posix(), data))
manifest = [{'path': name, 'sha256': digest(data), 'bytes': len(data)} for name, data in files]
manifest_data = json.dumps(manifest, indent=2).encode()
(stage / 'source-manifest.json').write_bytes(manifest_data)
files.append(('source-manifest.json', manifest_data))
out = ROOT / 'outputs/sourcebox-launcher/release-candidates/sourcebox-beta-v1.5-source.zip'
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for name, data in files: archive.writestr(name, data)
with zipfile.ZipFile(out) as archive:
    assert archive.testzip() is None
    assert len(archive.namelist()) == len(set(archive.namelist()))
    for entry in manifest: assert digest(archive.read(entry['path'])) == entry['sha256']
sha = digest(out.read_bytes())
out.with_suffix('.zip.sha256').write_text(sha + '  ' + out.name + '\n')
audit = {'archiveIntegrity':'passed','sourceManifest':'passed','privatePathAndSecretPatternScan':'passed',
         'compiledExecutablesIncluded':False,'gameAssetsIncluded':False,'files':len(files),
         'bytes':out.stat().st_size,'sha256':sha,'independentSecurityAudit':False}
out.with_suffix('.zip.audit.json').write_text(json.dumps(audit, indent=2))
print(json.dumps({'zip':str(out), **audit}, indent=2))
