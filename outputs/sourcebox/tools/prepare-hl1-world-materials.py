"""Local-only diffuse material variants for the older HL1 Source shaders.

Keep the installed textures and geometry. Avoid combinations of newer bump,
detail and cubemap features which the HL1 console reports failing to compile.
No proprietary content is included in the distributable launcher.
"""
import importlib.util, json, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('keyvalues', pathlib.Path(__file__).with_name('combine-source-worlds.py'))
kv = importlib.util.module_from_spec(spec)
spec.loader.exec_module(kv)
SOURCE = ROOT / 'work/runtime-assets/worlds-source1/materials'
DEST = ROOT / 'work/runtime-assets/worlds-hl1-materials/materials'

def resolve(name, seen=()):
    name = name.replace('\\', '/').lower().removeprefix('materials/').removesuffix('.vmt')
    if name in seen or '..' in pathlib.PurePosixPath(name).parts or ':' in name:
        raise ValueError('Invalid or cyclic material include: ' + name)
    tree = kv.parse((SOURCE / (name + '.vmt')).read_text(errors='replace'))
    shader, body = next((k, v) for k, v in tree if isinstance(v, list))
    body = [(k.lower(), v) for k, v in body]
    if shader.lower() == 'patch':
        shader, base = resolve(kv.value(body, 'include'), (*seen, name))
        for key in ('insert', 'replace'):
            for k, v in kv.value(body, key, []):
                kv.setval(base, k.lower(), v)
        return shader, base
    return shader, body

def compatible(shader, body):
    # These two brush shaders cover ordinary map surfaces; special shaders are
    # left native rather than silently changing water, glass or tools behavior.
    if shader.lower() not in ('lightmappedgeneric', 'worldvertextransition'):
        return None
    allowed = {'$basetexture', '$basetexture2', '$surfaceprop', '$surfaceprop2',
               '$alphatest', '$alphatestreference', '$translucent', '$nocull',
               '$basetexturetransform', '$basetexturetransform2', '$color', '$color2',
               '$alpha', '$additive', '$selfillum', '$selfillumtint'}
    values = [(k, v) for k, v in body if k in allowed and isinstance(v, str)]
    if not kv.value(values, '$basetexture'):
        raise ValueError('No base texture for ordinary brush material')
    return [(shader, values)]

def main():
    registry = json.loads((ROOT / 'outputs/sourcebox/sheets/worlds-expanded.json').read_text())
    names = set()
    for world in registry['worlds']:
        data = json.loads((ROOT / 'outputs/sourcebox/worlds' / (world['id'] + '.json')).read_text())
        names.update(c['material'] for c in data['chunks'])
    report = {'converted': [], 'nativeShader': [], 'failed': [], 'nativeGameplayVerified': False}
    for name in sorted(names):
        try:
            shader, body = resolve(name)
            tree = compatible(shader, body)
            if tree is None:
                report['nativeShader'].append(name)
                continue
            target = DEST / (name + '.vmt')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(kv.serialize(tree) + '\n')
            report['converted'].append(name)
        except Exception as error:
            report['failed'].append({'material': name, 'error': str(error)})
    output = ROOT / 'work/worlds/hl1-material-compatibility.json'
    output.write_text(json.dumps(report, indent=2))
    print(json.dumps({k: len(v) for k, v in report.items() if isinstance(v, list)}))
    if report['failed']:
        raise SystemExit(1)

if __name__ == '__main__':
    main()
