"""Build cosmetic corpse sources from locally installed meshes, never shipped assets.

Run inside Blender with the same request paths as prepare-portable-p2-peers.py.
Hull geometry is deliberately simple and bounded to the major body joints.
"""
import sys,json,pathlib,importlib.util,re,bpy
req=json.loads(pathlib.Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'))
root=pathlib.Path(req['sourceioRoot'])
spec=importlib.util.spec_from_file_location('SourceIO',root/'__init__.py',submodule_search_locations=[str(root)])
module=importlib.util.module_from_spec(spec);sys.modules['SourceIO']=module;spec.loader.exec_module(module);module.register()
from SourceIO.library.shared.content_manager import ContentManager
from SourceIO.library.shared.content_manager.providers.loose_files import LooseFilesContentProvider
from SourceIO.library.utils.tiny_path import TinyPath
jobs=[('hl2',req['sharedCache'],'models/sourcebox/hl2/player.mdl'),('chell',req['chellCache'],'models/player/chell/player.mdl')]
for name,cache,model in jobs:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 ContentManager().add_child(LooseFilesContentProvider(TinyPath(cache)))
 ContentManager().priority_list=list(ContentManager().children)
 source=pathlib.Path(cache)/model
 bpy.ops.sourceio.mdl(filepath=str(source),directory=str(source.parent),files=[{'name':source.name}],scale=1,import_textures=False,import_physics=False,import_animations=False,import_include_animations=False,create_flex_drivers=False)
 arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');arm.animation_data_clear();arm.data.pose_position='REST'
 meshes=[o for o in bpy.data.objects if o.type=='MESH' and not o.hide_get()]
 bones=list(arm.data.bones);ids={b.name:i for i,b in enumerate(bones)}
 names={b.name:re.sub('[^a-z0-9_]','_',b.name.lower()) for b in bones}
 header=['version 1','nodes']+[f'{i} "{names[b.name]}" {ids[b.parent.name] if b.parent else -1}' for i,b in enumerate(bones)]+['end','skeleton','time 0']
 for i,b in enumerate(bones):
  matrix=b.parent.matrix_local.inverted()@b.matrix_local if b.parent else arm.matrix_world@b.matrix_local
  header.append(str(i)+' '+' '.join(f'{v:.8f}' for v in [*matrix.to_translation(),*matrix.to_euler('XYZ')]))
 header+=['end'];visual=header+['triangles'];groups={}
 def major(b):
  while b:
   key=b.name.lower()
   if re.search(r'(pelvis|spine2|head1?|upperarm|forearm|hand|thigh|calf|foot)$',key) or key in ['root','spine3','head','bicep_r','bicep_l','elbow_r','elbow_l','wrist_r','wrist_l','thigh_r','thigh_l','knee_r','knee_l','ankle_r','ankle_l']:
    return ids[b.name]
   b=b.parent
  return 0
 for obj in meshes:
  mesh=obj.data;mesh.calc_loop_triangles();uv=mesh.uv_layers.active
  for tri in mesh.loop_triangles:
   visual.append(mesh.materials[tri.material_index].name.split('/')[-1])
   for loop in tri.loops:
    vertex=mesh.vertices[mesh.loops[loop].vertex_index];pos=obj.matrix_world@vertex.co;normal=(obj.matrix_world.to_3x3()@mesh.loops[loop].normal).normalized();tex=uv.data[loop].uv if uv else (0,0)
    weights=[(ids[obj.vertex_groups[g.group].name],g.weight) for g in vertex.groups if obj.vertex_groups[g.group].name in ids and g.weight>0]
    weights=sorted(weights,key=lambda w:-w[1])[:3];total=sum(w for _,w in weights);weights=[(i,w/total) for i,w in weights] if total else [(0,1)]
    visual.append('0 '+' '.join(f'{v:.8f}' for v in [*pos,*normal,*tex])+' '+str(len(weights))+' '+' '.join(f'{i} {w:.8f}' for i,w in weights))
    groups.setdefault(major(bones[weights[0][0]]),[]).append(pos)
 visual+=['end'];collision=header+['triangles'];active=[]
 faces=[(0,2,3),(0,3,1),(4,5,7),(4,7,6),(0,1,5),(0,5,4),(2,6,7),(2,7,3),(0,4,6),(0,6,2),(1,3,7),(1,7,5)]
 for bone,points in groups.items():
  if len(points)<8:continue
  lo=[min(p[a] for p in points) for a in range(3)];hi=[max(p[a] for p in points) for a in range(3)]
  # Shrink hulls slightly so adjacent limbs do not start deeply interpenetrating.
  for a in range(3):
   mid=(lo[a]+hi[a])/2;half=max(.5,(hi[a]-lo[a])*.40);lo[a]=mid-half;hi[a]=mid+half
  verts=[(hi[0] if i&4 else lo[0],hi[1] if i&2 else lo[1],hi[2] if i&1 else lo[2]) for i in range(8)]
  for face in faces:
   collision.append('physics')
   for index in reversed(face):collision.append(f'{bone} '+' '.join(f'{v:.8f}' for v in verts[index])+f' 0 0 1 0 0 1 {bone} 1')
  active.append(bone)
 collision+=['end']
 if not 5<=len(active)<=24:raise RuntimeError(f'{name}: unexpected physics body count {len(active)}')
 out=pathlib.Path(req['outputRoot'])/name;out.mkdir(parents=True,exist_ok=True)
 (out/'mesh.smd').write_text('\n'.join(visual)+'\n');(out/'physics.smd').write_text('\n'.join(collision)+'\n')
 materials={m.name.rsplit('/',1)[0] for obj in meshes for m in obj.data.materials}
 if name=='hl2':materials={'models/player'}
 qc=f'$modelname "sourcebox/ragdolls/{name}.mdl"\n$body body "mesh.smd"\n$surfaceprop flesh\n'
 qc+=''.join(f'$cdmaterials "{m}/"\n' for m in sorted(materials))
 qc+='$sequence idle "mesh.smd" fps 1\n$collisionjoints "physics.smd" {\n$mass 75\n$inertia 5\n$damping 0.1\n$rotdamping 2\n'
 for bone in active:
  parent=bones[bone].parent
  while parent and ids[parent.name] not in active:parent=parent.parent
  if not parent:continue
  for axis in 'xyz':qc+=f'$jointconstrain "{names[bones[bone].name]}" {axis} limit -45 45 0\n'
 qc+='}\n';(out/'model.qc').write_text(qc)
 print('RAGDOLL_SOURCE',name,len(active),flush=True)
