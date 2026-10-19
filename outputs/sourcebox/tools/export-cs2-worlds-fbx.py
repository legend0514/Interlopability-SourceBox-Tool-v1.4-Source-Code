"""Run in Blender after export-cs2-worlds.mjs preflight. Local game assets only."""
import bpy, json, pathlib, re
root=pathlib.Path(__file__).resolve().parents[3]
stage=root/'work/worlds/cs2-content'
palette=json.loads((root/'outputs/sourcebox/sheets/cs2-world-materials.json').read_text())
material_keys=list(palette['materials'])
override_file=root/'outputs/sourcebox/sheets/cs2-world-material-overrides.json'
overrides=json.loads(override_file.read_text()) if override_file.exists() else {}
material_paths=list(dict.fromkeys([*palette['materials'].values(),*overrides.values()]))
for entry in json.loads((stage/'worlds.json').read_text()):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 data=json.loads((root/'outputs/sourcebox/worlds'/f"{entry['id']}.json").read_text())
 vertices=[];faces=[];indices=[];uvs=[];lookup={}
 for chunk in data['chunks']:
  name=chunk['material'].lower()
  category=next((rule['material'] for rule in palette['rules'] if re.search(rule['pattern'],name)),palette['fallback'])
  material=material_paths.index(overrides.get(name,palette['materials'][category]))
  for start in range(0,len(chunk['vertices']),3):
   face=[]
   for v in chunk['vertices'][start:start+3]:
    # CS2's FBX importer maps this Blender basis to (-Y, X, Z).
    # Invert that basis here; compiled render AND PHYS bounds must match JSON.
    point=(v[1],-v[0],v[2])
    if point not in lookup:lookup[point]=len(vertices);vertices.append(point)
    face.append(lookup[point]);uvs.append(v[6:8])
   faces.append(face);indices.append(material)
 mesh=bpy.data.meshes.new(entry['id']);mesh.from_pydata(vertices,[],faces);mesh.update()
 obj=bpy.data.objects.new(entry['id'],mesh);bpy.context.collection.objects.link(obj)
 for source_path in material_paths:
  material_path=source_path.removesuffix('.vmat')
  mesh.materials.append(bpy.data.materials.get(material_path) or bpy.data.materials.new(material_path))
 for poly,index in zip(mesh.polygons,indices):poly.material_index=index
 uv=mesh.uv_layers.new(name='UVMap')
 for loop,value in zip(uv.data,uvs):loop.uv=value
 obj.select_set(True);bpy.context.view_layer.objects.active=obj
 target=stage/pathlib.Path(entry['model']).parent/'mesh.fbx'
 bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,add_leaf_bones=False,bake_anim=False,apply_unit_scale=False,global_scale=0.01,object_types={'MESH'},axis_forward='-Y',axis_up='Z')
 # Decorative non-solid brushes must never become invisible collision walls.
 obj.select_set(False);cv=[];cf=[];clookup={}
 for chunk in data['collision']:
  for start in range(0,len(chunk['vertices']),3):
   face=[]
   for v in chunk['vertices'][start:start+3]:
    point=(v[1],-v[0],v[2])
    if point not in clookup:clookup[point]=len(cv);cv.append(point)
    face.append(clookup[point])
   cf.append(face)
 cm=bpy.data.meshes.new(entry['id']+'_collision');cm.from_pydata(cv,[],cf);cm.update()
 co=bpy.data.objects.new(entry['id']+'_collision',cm);bpy.context.collection.objects.link(co);co.select_set(True);bpy.context.view_layer.objects.active=co
 bpy.ops.export_scene.fbx(filepath=str(target.with_name('collision.fbx')),use_selection=True,add_leaf_bones=False,bake_anim=False,apply_unit_scale=False,global_scale=0.01,object_types={'MESH'},axis_forward='-Y',axis_up='Z')
 model=stage/entry['model'];model.write_text(model.read_text().replace('mesh.obj','mesh.fbx'))
 print('EXPORTED_WORLD',entry['id'],len(vertices),len(faces),flush=True)
