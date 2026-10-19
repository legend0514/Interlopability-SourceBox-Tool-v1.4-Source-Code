import struct, json, math, re, sys, zipfile, io, lzma
from pathlib import Path
# Source SDK public bspfile.h structures; geometry only, no executable code.
class BSP:
 def __init__(self,path):
  self.b=Path(path).read_bytes()
  if self.b[:4]!=b'VBSP' or struct.unpack_from('<i',self.b,4)[0] not in (20,21):raise ValueError('Unsupported BSP')
 def lump(self,i):
  off,n,ver,code=struct.unpack_from('<4i',self.b,8+16*i)
  if off<0 or n<0 or off+n>len(self.b):raise ValueError('Invalid lump bounds')
  data=self.b[off:off+n]
  if code:
   if len(data)<17 or data[:4]!=b'LZMA':raise ValueError('Unsupported compressed lump header')
   size,packed=struct.unpack_from('<2I',data,4);p=data[12];dictionary=struct.unpack_from('<I',data,13)[0]
   if size>128*1024*1024 or dictionary>64*1024*1024 or packed!=len(data)-17 or p>=225:raise ValueError('Invalid compressed lump bounds')
   filters=[{'id':lzma.FILTER_LZMA1,'dict_size':max(dictionary,4096),'lc':p%9,'lp':(p//9)%5,'pb':p//45}]
   data=lzma.LZMADecompressor(format=lzma.FORMAT_RAW,filters=filters).decompress(data[17:],max_length=size)
   if len(data)!=size:raise ValueError('Compressed lump size mismatch')
  return data
def vadd(a,b):return tuple(x+y for x,y in zip(a,b))
def mul(a,t):return tuple(x*t for x in a)
def sub(a,b):return tuple(x-y for x,y in zip(a,b))
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def norm(v):
 d=math.sqrt(dot(v,v));return mul(v,1/d) if d>1e-9 else (0,0,1)
def rotate(v,angles):
 # Source QAngle: yaw about Z, pitch about Y, roll about X.
 p,y,r=[math.radians(a) for a in angles];x,b,z=v
 b,z=b*math.cos(r)-z*math.sin(r),b*math.sin(r)+z*math.cos(r)
 x,z=x*math.cos(p)+z*math.sin(p),-x*math.sin(p)+z*math.cos(p)
 return (x*math.cos(y)-b*math.sin(y),x*math.sin(y)+b*math.cos(y),z)
def parse(path,world):
 bsp=BSP(path); verts=list(struct.iter_unpack('<3f',bsp.lump(3)));edges=list(struct.iter_unpack('<2H',bsp.lump(12)));surf=[x[0] for x in struct.iter_unpack('<i',bsp.lump(13))];planes=list(struct.iter_unpack('<4fi',bsp.lump(1)))
 texinfos=list(struct.iter_unpack('<16f2i',bsp.lump(6)));texdata=list(struct.iter_unpack('<3f5i',bsp.lump(2)));strings=bsp.lump(43);offsets=[x[0] for x in struct.iter_unpack('<i',bsp.lump(44))]
 names=[strings[o:strings.index(0,o)].decode('utf8') for o in offsets];faces=bsp.lump(7);disp=bsp.lump(26);dv=list(struct.iter_unpack('<5f',bsp.lump(33)))
 models=list(struct.iter_unpack('<9f3i',bsp.lump(14)));first,count=models[0][-2:]
 entities=[dict(re.findall(r'"([^"\n]+)"\s*"([^"\n]*)"',e)) for e in re.findall(r'\{([^{}]*)\}',bsp.lump(0).decode('utf8',errors='replace'))]
 work=[(fi,(0,0,0),(0,0,0),True) for fi in range(first,first+count)]
 brush_count=0
 # Triggers and clipping volumes are not visible architecture. Moving doors,
 # breakables and track trains need separate synchronized entities, not a
 # permanently closed static wall baked into the floor collision mesh.
 for e in entities:
  if e.get('classname') not in ('func_brush','func_wall','func_illusionary','func_reflective_glass'):continue
  if not re.fullmatch(r'\*\d+',e.get('model','')):continue
  if e.get('StartDisabled','0')=='1':continue
  mi=int(e['model'][1:])
  if not 0<mi<len(models):raise ValueError('Invalid brush model reference')
  origin=tuple(float(v) for v in e.get('origin','0 0 0').split());angles=tuple(float(v) for v in e.get('angles','0 0 0').split())
  if len(origin)!=3 or len(angles)!=3 or not all(math.isfinite(v) for v in (*origin,*angles)):raise ValueError('Invalid brush transform')
  first,count=models[mi][-2:];solid=e.get('classname')!='func_illusionary' and e.get('Solidity','0')!='1'
  work.extend((fi,origin,angles,solid) for fi in range(first,first+count));brush_count+=1
 chunks={};collision={};stats={'faces':0,'triangles':0,'displacements':0,'skipped':0};bounds=[[float('inf')]*3,[-float('inf')]*3]
 def emit(tri,ti,material,normal,origin,angles,solid):
  local=tri;tri=tuple(vadd(rotate(p,angles),origin) for p in tri)
  if any(p[2]<world['sourceZ'][0] or p[2]>world['sourceZ'][1] for p in tri):return
  area=cross(sub(tri[1],tri[0]),sub(tri[2],tri[0]))
  longest=max(dot(sub(tri[i],tri[j]),sub(tri[i],tri[j])) for i,j in ((0,1),(1,2),(2,0)))
  # VPhysics uses floats. Near-collinear BSP edge fragments become degenerate
  # after translation even if their double-precision area is nonzero.
  if longest<1e-8 or dot(area,area)/longest<0.0001:return
  normal=norm(area)
  center=[sum(v[a] for v in tri)/3 for a in range(3)];cell=tuple(math.floor(x/2048) for x in center)
  # Static triangle meshes have their own native acceleration structure.
  # Splitting a flat floor into hundreds of Lua entities adds needless work.
  if solid:
   cells=collision.setdefault(world['id'],[[]]);physics=cells[-1]
   if len(physics)>=6000:physics=[];cells.append(physics)
   physics.extend([[round(x,5) for x in vadd(p,world['offset'])] for p in tri])
  key=(material,);groups=chunks.setdefault(key,[[]]);group=groups[-1]
  if len(group)>=6000:group=[];groups.append(group)
  td=texdata[ti[-1]];w,h=max(1,td[4]),max(1,td[5])
  for p,texture_p in zip(tri,local):
   q=vadd(p,world['offset'])
   for a in range(3):bounds[0][a]=min(bounds[0][a],q[a]);bounds[1][a]=max(bounds[1][a],q[a])
   uv=[(dot(texture_p,ti[0:3])+ti[3])/w,(dot(texture_p,ti[4:7])+ti[7])/h]
   group.append([round(x,5) for x in (*q,*normal,*uv)])
  stats['triangles']+=1
 for fi,origin,angles,solid in work:
  plan,side,node,fe,ne,tx,di,fog=struct.unpack_from('<HBBihhhh',faces,fi*56)
  if tx<0 or ne<3:continue
  ti=texinfos[tx];td=texdata[ti[-1]];mat=names[td[3]].lower()
  # Sky brushes are the enclosing BSP, not playable world geometry.
  if mat.startswith('tools/') or ti[-2]&0x80 or ti[-2]&0x4:stats['skipped']+=1;continue
  poly=[verts[edges[abs(surf[fe+i])][0 if surf[fe+i]>=0 else 1]] for i in range(ne)]
  normal=mul(planes[plan][:3],-1 if side else 1);stats['faces']+=1
  triangles=[]
  if di>=0:
   if len(poly)!=4:raise ValueError('Nonquad displacement')
   start=struct.unpack_from('<3f',disp,di*176);vi,_,power=struct.unpack_from('<3i',disp,di*176+12)
   if power not in (2,3,4):raise ValueError('Unsupported displacement power')
   at=min(range(4),key=lambda i:dot(sub(poly[i],start),sub(poly[i],start)));poly=poly[at:]+poly[:at];steps=2**power;grid=[]
   for y in range(steps+1):
    for x in range(steps+1):
     u=x/steps;v=y/steps
     base=vadd(mul(vadd(mul(poly[0],1-u),mul(poly[3],u)),1-v),mul(vadd(mul(poly[1],1-u),mul(poly[2],u)),v))
     vert=dv[vi+y*(steps+1)+x];grid.append(vadd(base,mul(vert[:3],vert[3])))
   for y in range(steps):
    for x in range(steps):
     a=y*(steps+1)+x;b=a+1;c=a+steps+1;d=c+1
     triangles.extend([(grid[a],grid[c],grid[b]),(grid[b],grid[c],grid[d])])
   stats['displacements']+=1
  else:triangles=[(poly[0],poly[i],poly[i+1]) for i in range(1,len(poly)-1)]
  for tri in triangles:
   if dot(cross(sub(tri[1],tri[0]),sub(tri[2],tri[0])),normal)<0:tri=(tri[0],tri[2],tri[1])
   emit(tri,ti,mat,normal,origin,angles,solid)
 stats['brushModels']=brush_count
 result={'id':world['id'],'offset':world['offset'],'spawn':world['spawn'],'yaw':world['yaw'],'bounds':bounds,'stats':stats,'collision':[{'vertices':g} for groups in collision.values() for g in groups],'chunks':[{'material':k[0],'vertices':g} for k,groups in chunks.items() for g in groups]}
 if any(abs(x)>16000 for row in bounds for x in row):raise ValueError('World exceeds native coordinates: '+str(bounds))
 return result
if __name__=='__main__':
 cfg=json.loads(Path(sys.argv[1] if len(sys.argv)>1 else 'outputs/sourcebox/sheets/worlds.json').read_text())
 for world in cfg['worlds']:
  pak=BSP(world['source']).lump(40)
  if pak:
   assets=Path('work/worlds/assets').resolve();assets.mkdir(parents=True,exist_ok=True)
   for entry in zipfile.ZipFile(io.BytesIO(pak)).infolist():
    rel=entry.filename.replace('\\','/')
    if entry.is_dir() or not rel.startswith(('materials/','models/')) or '..' in rel.split('/'):continue
    target=(assets/rel).resolve()
    if not target.is_relative_to(assets):raise ValueError('Unsafe embedded path')
    target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(zipfile.ZipFile(io.BytesIO(pak)).read(entry))
  result=parse(world['source'],world);out=Path('outputs/sourcebox/worlds')/(world['id']+'.json');out.write_text(json.dumps(result,separators=(',',':')));print(world['id'],result['stats'],'bounds',result['bounds'],'bytes',out.stat().st_size)

