"""Generate original sky enclosures around the locally imported world bounds.
The mesh exporter intentionally skips Source 1 sky faces. Source 2 still needs
sky surfaces for its sky rendering and outdoor lighting, not just env_sky.
"""
import importlib.util, json, pathlib
root = pathlib.Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('compose', pathlib.Path(__file__).with_name('combine-source-worlds.py'))
compose = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compose)
registry = json.loads((root/'outputs/sourcebox/sheets/worlds-expanded.json').read_text())
world = [('id','1'),('classname','worldspawn'),('skyname','sky_day01_01')]
counter = 2
for entry in registry['worlds']:
    bounds = json.loads((root/f"outputs/sourcebox/worlds/{entry['id']}.json").read_text())['bounds']
    x,y,z = [n-256 for n in bounds[0]]
    X,Y,Z = [n+256 for n in bounds[1]]
    z = min(z,entry['sourceZ'][0]+entry['offset'][2]-256)
    t = 16
    for lo,hi in [([x,y,z-t],[X,Y,z]),([x,y,Z],[X,Y,Z+t]),([x-t,y,z],[x,Y,Z]),([X,y,z],[X+t,Y,Z]),([x,y-t,z],[X,y,Z]),([x,Y,z],[X,Y+t,Z])]:
        solid = [('id',str(counter))];counter+=1
        for key, side in compose.box(lo,hi):
            solid.append((key,[('id',str(counter)),*side]));counter+=1
        world.append(('solid',solid))
document = [('versioninfo',[('editorversion','400'),('editorbuild','1'),('mapversion','1'),('formatversion','100'),('prefab','0')]),('world',world),('visgroups',[]),('viewsettings',[]),('cameras',[('activecamera','-1')]),('cordons',[('active','0')])]
target = root/'work/worlds/sky-import/maps/sourcebox_world_skies.vmf'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(compose.serialize(document)+'\n')
print(target)
