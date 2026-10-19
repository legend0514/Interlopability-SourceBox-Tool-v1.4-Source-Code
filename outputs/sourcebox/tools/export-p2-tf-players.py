"""Local installed-asset conversion. Run with Blender; no assets enter release output."""
import sys, importlib.util, pathlib, bpy
from mathutils import Matrix
root = pathlib.Path(__file__).resolve().parents[3]
sourceio = root / 'work/cs2/sourceio/SourceIO-master'
spec = importlib.util.spec_from_file_location('SourceIO', sourceio/'__init__.py', submodule_search_locations=[str(sourceio)])
module = importlib.util.module_from_spec(spec)
sys.modules['SourceIO'] = module
spec.loader.exec_module(module)
module.register()
from SourceIO.library.shared.content_manager import ContentManager
from SourceIO.library.shared.content_manager.providers.loose_files import LooseFilesContentProvider
from SourceIO.library.utils.tiny_path import TinyPath
cache = root/'work/runtime-assets/tf2-soldier'
ContentManager().add_child(LooseFilesContentProvider(TinyPath(str(cache))))
source = (root/'outputs/sourcebox/tools/export-chell.py').read_text()
writer = source[source.index('def smd('):source.index('arm.animation_data_clear()')]
names = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['soldier']
for name in names:
 if name not in ['scout','soldier','pyro','demo','heavy','engineer','medic','sniper','spy']:
  raise ValueError('Unexpected TF2 class')
 bpy.ops.wm.read_factory_settings(use_empty=True)
 model = cache/'models/player'/f'{name}.mdl'
 bpy.ops.sourceio.mdl(filepath=str(model), directory=str(model.parent), files=[{'name':model.name}], scale=1.0, import_textures=False, import_physics=False, import_animations=True, import_include_animations=True, create_flex_drivers=False)
 out = root/'work/phase6'/f'p2-{name}'
 out.mkdir(exist_ok=True)
 arm = next(o for o in bpy.data.objects if o.type=='ARMATURE')
 meshes = [o for o in bpy.data.objects if o.type=='MESH' and not o.hide_get()]
 bones = list(arm.data.bones)
 indices = {b.name:i for i,b in enumerate(bones)}
 exec(writer)
 material_dirs = {m.name.rsplit('/',1)[0] for obj in meshes for m in obj.data.materials}
 arm.animation_data_clear(); arm.data.pose_position='REST'; bpy.context.view_layer.update(); smd('mesh')
 arm.data.pose_position='POSE'; arm.animation_data_create()
 for clip, needle in [('idle','stand_PRIMARY'),('run','a_runN_PRIMARY'),('air','jumpfloat_PRIMARY')]:
  choices = [(a,s) for a in bpy.data.actions for s in a.slots if needle.replace('_','') in s.identifier.replace('_','')]
  if not choices: raise RuntimeError(f'{name}: missing genuine animation {needle}')
  action,slot = choices[0]
  arm.animation_data.action=action; arm.animation_data.action_slot=slot
  bag=action.layers[0].strips[0].channelbag(slot)
  frames=[key.co.x for curve in bag.fcurves for key in curve.keyframe_points]
  smd(clip,range(int(min(frames)),int(max(frames))+1))
 qc=f'$modelname "sourcebox/portal2/peers/{name}.mdl"\n$body body "mesh.smd"\n$surfaceprop flesh\n'
 qc+=''.join(f'$cdmaterials "{m}/"\n' for m in sorted(material_dirs))
 qc+=''.join(f'$sequence {clip} "{clip}.smd" fps 30 loop\n' for clip in ['idle','run','air'])
 qc+='$bbox -16 -16 0 16 16 72\n$cbox -16 -16 0 16 16 72\n'
 (out/'model.qc').write_text(qc)
 print('EXPORTED_P2_TF_PLAYER',name,flush=True)
