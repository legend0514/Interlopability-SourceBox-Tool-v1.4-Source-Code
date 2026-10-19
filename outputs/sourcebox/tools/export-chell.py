# Local-only asset conversion; generated model files stay under work/, never in release.
import bpy,pathlib,json
from mathutils import Matrix
root=pathlib.Path(__file__).resolve().parents[3]
out=root/'work/phase6/chell-export';out.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'work/cs2/chell.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.data.objects if o.type=='MESH' and not o.hide_get()]
bones=list(arm.data.bones);indices={b.name:i for i,b in enumerate(bones)}
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
arm.animation_data_clear();arm.data.pose_position='REST';bpy.context.view_layer.update();smd('mesh')
bpy.ops.object.select_all(action='DESELECT')
for obj in [arm,*meshes]:obj.select_set(True)
for mat in bpy.data.materials:
 if mat.name.startswith('models/'):mat.name='materials/'+mat.name+'.vmat'
bpy.ops.export_scene.fbx(filepath=str(out/'mesh.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,apply_unit_scale=False,global_scale=0.01,object_types={'MESH','ARMATURE'})
arm.data.pose_position='POSE';a=bpy.data.actions['player_animations.mdl'];arm.animation_data_create()
# Include-model translations omit the character's bind-pose bone lengths.
# Retain those lengths rather than collapsing every joint onto its parent.
for slot in a.slots:
 bag=a.layers[0].strips[0].channelbag(slot)
 if bag:
  for fc in list(bag.fcurves):
   if fc.data_path.endswith('.location') and '"root"' not in fc.data_path:bag.fcurves.remove(fc)
for pb in arm.pose.bones:pb.location=(0,0,0)
for name,slotname in {'idle':'OBportalgun_standing_aimmatrix_aimMat_neutralPose','run':'OBportalgun_runCenter','air':'OB@defaultPose'}.items():
 slot=next(s for s in a.slots if s.identifier==slotname);arm.animation_data.action=a;arm.animation_data.action_slot=slot
 bag=a.layers[0].strips[0].channelbag(slot);frames=[k.co.x for fc in bag.fcurves for k in fc.keyframe_points];lo=int(min(frames));hi=int(max(frames));bpy.context.scene.frame_start=lo;bpy.context.scene.frame_end=hi;smd(name,range(lo,hi+1))
 bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,apply_unit_scale=False,global_scale=0.01,object_types={'ARMATURE'})
 print('EXPORTED_CHELL',name,lo,hi)
print('CHELL_HAND_BONES',[b.name for b in bones if 'hand' in b.name.lower()])
(out/'chell.qc').write_text('$modelname "sourcebox/portal2/chell.mdl"\n$body body "mesh.smd"\n$surfaceprop flesh\n$cdmaterials "models/player/chell/"\n$sequence idle "idle.smd" fps 30 loop\n$sequence run "run.smd" fps 30 loop\n$sequence air "air.smd" fps 30 loop\n$bbox -16 -16 0 16 16 72\n$cbox -16 -16 0 16 16 72\n')
