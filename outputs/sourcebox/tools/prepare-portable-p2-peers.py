"""Build local Portal 2 peers from installed models; no blend-file prerequisites."""
import sys,json,pathlib,importlib.util,bpy
from mathutils import Matrix
request=json.loads(pathlib.Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'))
sourceio=pathlib.Path(request['sourceioRoot'])
spec=importlib.util.spec_from_file_location('SourceIO',sourceio/'__init__.py',submodule_search_locations=[str(sourceio)])
module=importlib.util.module_from_spec(spec);sys.modules['SourceIO']=module;spec.loader.exec_module(module);module.register()
from SourceIO.library.shared.content_manager import ContentManager
from SourceIO.library.shared.content_manager.providers.loose_files import LooseFilesContentProvider
from SourceIO.library.utils.tiny_path import TinyPath
for cache in [request['tfCache'],request['sharedCache']]:
 ContentManager().add_child(LooseFilesContentProvider(TinyPath(cache)))
output=pathlib.Path(request['outputRoot']);output.mkdir(parents=True,exist_ok=True)
def smd(name,frames=None):
 lines=['version 1','nodes']+[f'{i} "{b.name}" {indices[b.parent.name] if b.parent else -1}' for i,b in enumerate(bones)]+['end','skeleton']
 for tick,frame in enumerate(frames or [0]):
  if frames is not None:bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
  lines.append('time '+str(tick))
  for i,b in enumerate(bones):
   matrix=arm.pose.bones[b.name].matrix.copy() if frames is not None else b.matrix_local.copy()
   if b.parent:
    parent=arm.pose.bones[b.parent.name].matrix if frames is not None else b.parent.matrix_local
    matrix=parent.inverted()@matrix
   else:matrix=arm.matrix_world@matrix
   xyz=matrix.to_translation();ang=matrix.to_euler('XYZ');lines.append(str(i)+' '+' '.join(f'{v:.8f}' for v in [*xyz,*ang]))
 lines.append('end')
 if frames is None:
  lines.append('triangles')
  for obj in meshes:
   mesh=obj.data;mesh.calc_loop_triangles();uv=mesh.uv_layers.active
   for tri in mesh.loop_triangles:
    material=mesh.materials[tri.material_index].name.split('/')[-1];lines.append(material)
    for loop in tri.loops:
     vertex=mesh.vertices[mesh.loops[loop].vertex_index];pos=obj.matrix_world@vertex.co;normal=(obj.matrix_world.to_3x3()@mesh.loops[loop].normal).normalized();tex=uv.data[loop].uv if uv else (0,0)
     weights=[(indices[obj.vertex_groups[g.group].name],g.weight) for g in vertex.groups if obj.vertex_groups[g.group].name in indices and g.weight>0]
     weights=sorted(weights,key=lambda w:-w[1])[:3];total=sum(w for _,w in weights)
     weights=[(i,w/total) for i,w in weights] if total else [(0,1)]
     lines.append('0 '+' '.join(f'{v:.8f}' for v in [*pos,*normal,*tex])+' '+str(len(weights))+' '+' '.join(f'{i} {w:.8f}' for i,w in weights))
  lines.append('end')
 (out/(name+'.smd')).write_text('\n'.join(lines)+'\n')

jobs=[
 ('heavy',request['tfCache'],'models/player/heavy.mdl',{'idle':'stand_PRIMARY','run':'a_runN_PRIMARY','air':'jumpfloat_PRIMARY'}),
 ('soldier',request['tfCache'],'models/player/soldier.mdl',{'idle':'stand_PRIMARY','run':'a_runN_PRIMARY','air':'jumpfloat_PRIMARY'}),
 ('gmod',request['sharedCache'],'models/player/kleiner.mdl',{'idle':'@idle_all_01','run':'a_RunN','air':'@jump_physgun'})]
if request.get('chellCache'):
 jobs.append(('chell',request['chellCache'],'models/player/chell/player.mdl',{'idle':'portalgun_standing_aimmatrix_aimMat_neutralPose','run':'portalgun_runCenter','air':'@defaultPose'}))
for name,cache,model,clips in jobs:
 if request.get('only') and name not in request['only']:continue
 bpy.ops.wm.read_factory_settings(use_empty=True)
 for provider in [cache,request['tfCache'],request['sharedCache']]:
  ContentManager().add_child(LooseFilesContentProvider(TinyPath(provider)))
 # Keep the owning provider in the first lookup; SourceIO's automatic priority
 # discovery otherwise omits it for the initial include-model lookup.
 ContentManager().priority_list=list(ContentManager().children)
 model=pathlib.Path(cache)/model
 bpy.ops.sourceio.mdl(filepath=str(model),directory=str(model.parent),files=[{'name':model.name}],scale=1.0,import_textures=False,import_physics=False,import_animations=True,import_include_animations=True,create_flex_drivers=False)
 out=output/name;out.mkdir(exist_ok=True)
 arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
 meshes=[o for o in bpy.data.objects if o.type=='MESH' and not o.hide_get()]
 bones=list(arm.data.bones);indices={b.name:i for i,b in enumerate(bones)}
 arm.animation_data_clear();arm.data.pose_position='REST';bpy.context.view_layer.update();smd('mesh')
 arm.data.pose_position='POSE';arm.animation_data_create()
 if name=='chell':
  for action in bpy.data.actions:
   for slot in action.slots:
    bag=action.layers[0].strips[0].channelbag(slot)
    if bag:
     for curve in list(bag.fcurves):
      if curve.data_path.endswith('.location') and '"root"' not in curve.data_path:bag.fcurves.remove(curve)
  for bone in arm.pose.bones:bone.location=(0,0,0)
 for clip,needle in clips.items():
  choices=[(a,s) for a in bpy.data.actions for s in a.slots if needle.replace('_','') in s.identifier.replace('_','')]
  if not choices:raise RuntimeError(f'{name}: missing animation {needle}')
  action,slot=choices[0];arm.animation_data.action=action;arm.animation_data.action_slot=slot
  bag=action.layers[0].strips[0].channelbag(slot)
  frames=[key.co.x for curve in bag.fcurves for key in curve.keyframe_points]
  if not frames:raise RuntimeError(f'{name}: empty animation {needle}')
  smd(clip,range(int(min(frames)),int(max(frames))+1))
 materials={m.name.rsplit('/',1)[0] for obj in meshes for m in obj.data.materials}
 if name=='gmod':materials={'models/kleiner'}
 alias='sourcebox/portal2/chell.mdl' if name=='chell' else f'sourcebox/portal2/peers/{name}.mdl'
 qc=f'$modelname "{alias}"\n$body body "mesh.smd"\n$surfaceprop flesh\n'
 qc+=''.join(f'$cdmaterials "{m}/"\n' for m in sorted(materials))
 qc+=''.join(f'$sequence {clip} "{clip}.smd" fps 30 loop\n' for clip in clips)
 qc+='$bbox -16 -16 0 16 16 72\n$cbox -16 -16 0 16 16 72\n'
 (out/'model.qc').write_text(qc)
 print('EXPORTED_LOCAL_PEER',name,flush=True)

if request.get('chellCache'):
 import struct,shutil
 from SourceIO.library.utils.pylib.vtf import load_vtf_texture
 source=pathlib.Path(request['chellCache'])/'materials'
 destination=output.parent/'materials'
 for file in source.rglob('*'):
  if not file.is_file():continue
  target=destination/file.relative_to(source);target.parent.mkdir(parents=True,exist_ok=True)
  if file.suffix=='.vmt':shutil.copyfile(file,target);continue
  if file.suffix!='.vtf':continue
  original=file.read_bytes();pixels,width,height,is_float=load_vtf_texture(original)
  if is_float or len(pixels)!=width*height*4:raise ValueError('Unexpected Chell texture format')
  header=bytearray(80);header[:4]=b'VTF\0';struct.pack_into('<IIIHHIHH',header,4,7,2,80,width,height,struct.unpack_from('<I',original,20)[0]&0x3383,1,0)
  struct.pack_into('<3f',header,32,*struct.unpack_from('<3f',original,32));struct.pack_into('<fI',header,48,1.0,0)
  header[56]=1;struct.pack_into('<I',header,57,0xffffffff);struct.pack_into('<H',header,63,1)
  target.write_bytes(header+pixels)
 for eye in ['l','r']:
  (destination/'models/player/chell'/f'gambler_eyeball_{eye}.vmt').write_text('"VertexLitGeneric" { "$basetexture" "models/player/chell/gambler_eyes" "$halflambert" "1" }')
