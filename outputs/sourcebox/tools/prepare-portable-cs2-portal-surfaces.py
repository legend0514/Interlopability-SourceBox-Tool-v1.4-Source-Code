"""Generate SourceBox's original ring and oval camera surface geometry."""
import bpy,pathlib,math,sys,json
request=json.loads(pathlib.Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'))
content=pathlib.Path(request['content'])
for kind in ['ring','monitor']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 if kind=='ring':
  verts=[];faces=[]
  for i in range(64):
   angle=2*math.pi*i/64;verts.extend([(0,32*math.cos(angle),64*math.sin(angle)),(0,28*math.cos(angle),60*math.sin(angle))])
  for i in range(64):j=(i+1)%64;faces.append((2*i,2*j,2*j+1,2*i+1))
 else:
  verts=[(0,0,0)]+[(0,28*math.cos(i*2*math.pi/64),60*math.sin(i*2*math.pi/64)) for i in range(64)]
  faces=[(0,1+i,1+(i+1)%64) for i in range(64)]
 mesh=bpy.data.meshes.new(kind);mesh.from_pydata(verts,[],faces)
 obj=bpy.data.objects.new(kind,mesh);bpy.context.collection.objects.link(obj);obj.select_set(True)
 if kind=='monitor':
  uv=mesh.uv_layers.new(name='UVMap')
  for loop in mesh.loops:
   _,y,z=verts[loop.vertex_index];uv.data[loop.index].uv=(.5+y/56,.5-z/120)
 mesh.materials.append(bpy.data.materials.new('materials/sourcebox/portal_'+kind+'.vmat'))
 folder=content/'models/sourcebox/portals'/kind;folder.mkdir(parents=True,exist_ok=True)
 bpy.ops.export_scene.fbx(filepath=str(folder/'mesh.fbx'),use_selection=True,bake_anim=False,apply_unit_scale=False,global_scale=.01,object_types={'MESH'})
