"""Convert fixed detail brushes to native static models without changing their surfaces.
This keeps older BSP hosts below the 8192 brush limit; collision uses closed convex
brush components, not a single hull around the whole building.
"""
import pathlib,importlib.util,json,re,math,itertools,hashlib
ROOT=pathlib.Path(__file__).resolve().parents[3]
s=importlib.util.spec_from_file_location('vmf',pathlib.Path(__file__).with_name('combine-source-worlds.py'));v=importlib.util.module_from_spec(s);s.loader.exec_module(v)
def add(a,b):return tuple(x+y for x,y in zip(a,b))
def sub(a,b):return tuple(x-y for x,y in zip(a,b))
def mul(a,t):return tuple(x*t for x in a)
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def unit(a):return mul(a,1/math.sqrt(dot(a,a)))
def polygons(solid):
 sides=[side for k,side in solid if k=='side'];allpoints=v.solid_points(solid);center=mul(tuple(map(sum,zip(*allpoints))),1/len(allpoints));planes=[]
 for side in sides:
  a,b,c=[v.vector(p) for p in re.findall(r'\([^)]*\)',v.value(side,'plane'))];normal=unit(cross(sub(b,a),sub(c,a)));d=dot(normal,a)
  if dot(normal,center)>d:normal=mul(normal,-1);d=-d
  planes.append((normal,d,side))
 points=[]
 for (a,x,_),(b,y,_),(c,z,_) in itertools.combinations(planes,3):
  determinant=dot(a,cross(b,c))
  if abs(determinant)<1e-7:continue
  p=mul(add(add(mul(cross(b,c),x),mul(cross(c,a),y)),mul(cross(a,b),z)),1/determinant)
  if all(dot(n,p)<=d+0.02 for n,d,_ in planes) and not any(dot(sub(p,q),sub(p,q))<0.0001 for q in points):points.append(p)
 if len(points)<4:raise ValueError('Invalid convex brush')
 out=[]
 for normal,d,side in planes:
  face=[p for p in points if abs(dot(normal,p)-d)<0.03]
  if len(face)<3:continue
  center=mul(tuple(map(sum,zip(*face))),1/len(face));right=unit(sub(face[0],center));up=cross(normal,right)
  face.sort(key=lambda p:math.atan2(dot(sub(p,center),up),dot(sub(p,center),right)))
  out.append((face,normal,side))
 return out
source=ROOT/'work/worlds/decompiled/ctf_2fort.vmf';doc=v.parse(source.read_text());w=next(w for w in json.loads((ROOT/'outputs/sourcebox/sheets/worlds-expanded.json').read_text())['worlds'] if w['id']=='ctf_2fort')
brushes=[]
for k,e in doc:
 if k=='entity' and v.value(e,'classname')=='func_detail':
  for key,solid in e:
   if key=='solid' and isinstance(solid,list) and v.keep(solid,w):brushes.append(solid)
materialdefs={e['name']:e for e in json.loads((ROOT/'work/worlds/source-materials.json').read_text()) if e['status']=='extracted'}
# Cubemap-patched materials reference the same original base surfaces.
for name,e in list(materialdefs.items()):
 if name.startswith('maps/'):
  base='/'.join(name.split('/')[2:]);base=re.sub(r'_-?\d+_-?\d+_-?\d+$','',base);materialdefs.setdefault(base,e)
cache=ROOT/'work/runtime-assets/worlds-source1';stage=ROOT/'work/worlds/source1-detail';stage.mkdir(exist_ok=True);models=[];aliases={}
for batch in range(0,len(brushes),20):
 polys=[poly for s in brushes[batch:batch+20] for poly in polygons(s)]
 points=[p for face,_,_ in polys for p in face];center=tuple((min(p[a] for p in points)+max(p[a] for p in points))/2 for a in range(3))
 render=[];collision=[]
 for face,n,side in polys:
  material=v.value(side,'material','').lower();tool=material.startswith('tools/')
  alias='m_'+hashlib.sha256(material.encode()).hexdigest()[:16];definition=materialdefs.get(material)
  width=height=256
  if definition:
   tex=ROOT/'work/worlds/source-materials'/definition['texture'];import struct
   width,height=struct.unpack_from('<HH',tex.read_bytes(),16)
   dest=cache/'materials/sourcebox/world_detail'/f'{alias}.vmt';dest.parent.mkdir(parents=True,exist_ok=True)
   dest.write_text('"VertexLitGeneric"\n{\n "$basetexture" "'+definition['texture'][10:-4]+'"\n "$halflambert" "1"\n}\n');aliases[material]=alias
  elif not tool:raise ValueError('Missing original detail material '+material)
  def uv(p,key,size):
   m=re.fullmatch(r'\[([^]]+)\]\s+([^ ]+)',v.value(side,key));axis=v.vector(m[1]);return (dot(p,axis[:3])/float(m[2])+axis[3])/size
  for i in range(1,len(face)-1):
   tri=[face[0],face[i],face[i+1]]
   collision.append('phys');render.extend([] if tool else [alias])
   for p in tri:
    row='0 '+' '.join(f'{x:.7g}' for x in (*sub(p,center),*n,uv(p,'uaxis',width),1-uv(p,'vaxis',height)))
    collision.append('0 '+' '.join(f'{x:.7g}' for x in (*sub(p,center),0,0,1,0,0)))
    if not tool:render.append(row)
 if not render:continue
 name=f'ctf_2fort_detail_{batch//20:03}';folder=stage/name;folder.mkdir(exist_ok=True)
 header='version 1\nnodes\n0 "root" -1\nend\nskeleton\ntime 0\n0 0 0 0 0 0 0\nend\ntriangles\n'
 (folder/'render.smd').write_text(header+'\n'.join(render)+'\nend\n');(folder/'collision.smd').write_text(header+'\n'.join(collision)+'\nend\n')
 model='sourcebox/world_detail/'+name+'.mdl'
 (folder/'model.qc').write_text('$modelname "'+model+'"\n$staticprop\n$body body "render.smd"\n$cdmaterials "sourcebox/world_detail/"\n$sequence idle "render.smd"\n$surfaceprop concrete\n$collisionmodel "collision.smd"\n{\n $concave\n $maxconvexpieces 256\n $automass\n}\n')
 models.append({'model':'models/'+model,'origin':[center[a]+w['offset'][a] for a in range(3)],'qc':str(folder/'model.qc'),'brushes':len(brushes[batch:batch+20])})
(stage/'models.json').write_text(json.dumps(models,indent=2));print('DETAIL_MODELS',len(models),'BRUSHES',len(brushes),flush=True)

