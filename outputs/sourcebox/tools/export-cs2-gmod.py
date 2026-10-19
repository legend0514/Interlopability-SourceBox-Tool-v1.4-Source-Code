import bpy,pathlib,json
root=pathlib.Path(__file__).resolve().parents[3];out=root/'work/cs2/gmod-export';out.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'work/cs2/gmod.blend'))
arm=next(o for o in bpy.data.objects if o.type=='ARMATURE')
bpy.ops.object.select_all(action='DESELECT')
chosen=[]
for o in bpy.data.objects:
 if o.type=='ARMATURE' or (o.type=='MESH' and not o.hide_get()):o.select_set(True);chosen.append(o.name)
print('EXPORT_OBJECTS',chosen)
mats=sorted(set(m.name for o in bpy.context.selected_objects if o.type=='MESH' for m in o.data.materials if m));(out/'materials.json').write_text(json.dumps(mats))
matmap=json.loads((root/'work/cs2/gmod-material-map.json').read_text())
for m in bpy.data.materials:
 if m.name in matmap:m.name=matmap[m.name]
arm.animation_data_clear()
arm.data.pose_position="REST"
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(out/'mesh.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,apply_unit_scale=False,global_scale=0.01,object_types={'MESH','ARMATURE'})
arm.data.pose_position="POSE"
a=bpy.data.actions['_anm.mdl'];arm.animation_data_create()
for name,slotname in {'idle':'OB@idle_all_01','run':'OBa_RunN','air':'OB@jump_physgun'}.items():
 slot=next(s for s in a.slots if s.identifier==slotname);arm.animation_data.action=a;arm.animation_data.action_slot=slot
 bag=a.layers[0].strips[0].channelbag(slot);frames=[k.co.x for fc in bag.fcurves for k in fc.keyframe_points];bpy.context.scene.frame_start=int(min(frames));bpy.context.scene.frame_end=int(max(frames));bpy.context.scene.frame_set(int(min(frames)))
 bpy.ops.export_scene.fbx(filepath=str(out/(name+'.fbx')),use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,apply_unit_scale=False,global_scale=0.01,object_types={'ARMATURE'})
 print('EXPORTED_CLIP',name,min(frames),max(frames))

