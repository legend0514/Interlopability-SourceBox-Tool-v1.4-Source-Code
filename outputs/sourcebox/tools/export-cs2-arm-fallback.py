import sys,importlib.util,pathlib,bpy,json,math
from mathutils import Matrix
root=pathlib.Path(__file__).resolve().parents[3];p=root/'work/cs2/sourceio/SourceIO-master'
spec=importlib.util.spec_from_file_location('SourceIO',p/'__init__.py',submodule_search_locations=[str(p)]);m=importlib.util.module_from_spec(spec);sys.modules['SourceIO']=m;spec.loader.exec_module(m);m.register()
from SourceIO.library.shared.content_manager import ContentManager
from SourceIO.library.shared.content_manager.providers.loose_files import LooseFilesContentProvider
from SourceIO.library.utils.tiny_path import TinyPath
src=root/'work/worlds/scenery/source';ContentManager().add_child(LooseFilesContentProvider(TinyPath(str(src))))
f=src/'models/anim_wp/room_transform/arm64x64_interior.mdl'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.sourceio.mdl(filepath=str(f),directory=str(f.parent),files=[{'name':f.name}],scale=1.0,import_textures=False,import_physics=False,import_animations=False,import_include_animations=False,create_flex_drivers=False)
objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];bpy.ops.object.select_all(action='DESELECT')
for o in objects:
 o.select_set(True);bpy.context.view_layer.objects.active=o
 for mod in list(o.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 o.parent=None;o.matrix_world=Matrix.Rotation(-math.pi/2,4,'Z')@o.matrix_world
for material in bpy.data.materials:material.name='materials/de_nuke/nuke_metalfloor_01'
folder=root/'work/worlds/scenery/fallback-arm';folder.mkdir(exist_ok=True)
bpy.ops.export_scene.fbx(filepath=str(folder/'mesh.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,apply_unit_scale=False,global_scale=0.01,object_types={'MESH'},axis_forward='-Y',axis_up='Z')
print('ARM_FALLBACK_MESHES',len(objects),sum(len(o.data.polygons) for o in objects))
