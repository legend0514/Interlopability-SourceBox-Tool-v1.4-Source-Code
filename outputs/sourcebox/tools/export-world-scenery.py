"""Inventory installed BSP scenery; preserve native transforms and model identity."""
import importlib.util,json,re,struct,lzma,pathlib
root=pathlib.Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('bsp',pathlib.Path(__file__).with_name('export-worlds.py'));bsp=importlib.util.module_from_spec(spec);spec.loader.exec_module(bsp)
registry=json.loads((root/'outputs/sourcebox/sheets/worlds-expanded.json').read_text());result=[]
for world in registry['worlds']:
 b=bsp.BSP(world['source']);game=b.lump(35);instances=[];versions=[]
 base=struct.unpack_from('<i',b.b,8+16*35)[0]
 count=struct.unpack_from('<i',game)[0] if game else 0
 headers=[struct.unpack_from('<IHHii',game,4+i*16) for i in range(count)]
 for ident,flags,version,offset,size in headers:
  if ident!=int.from_bytes(b'sprp','big'):continue
  relative=offset-base;data=game[relative:relative+size]
  if flags&1:
   if data[:4]!=b'LZMA':raise ValueError('Invalid compressed prop lump')
   unpacked,packed=struct.unpack_from('<II',data,4);p=data[12];dictionary=struct.unpack_from('<I',data,13)[0]
   if unpacked>64*1024*1024 or dictionary>64*1024*1024:raise ValueError('Prop lump exceeds budget')
   data=lzma.LZMADecompressor(format=lzma.FORMAT_RAW,filters=[{'id':lzma.FILTER_LZMA1,'dict_size':max(4096,dictionary),'lc':p%9,'lp':p//9%5,'pb':p//45}]).decompress(game[relative+17:relative+17+packed],max_length=unpacked)
   if len(data)!=unpacked:raise ValueError('Prop lump decompression mismatch')
  n=struct.unpack_from('<i',data)[0];at=4
  names=[data[at+i*128:at+(i+1)*128].split(b'\0')[0].decode().replace('\\','/').lower() for i in range(n)];at+=n*128
  leaves=struct.unpack_from('<i',data,at)[0];at+=4+leaves*(4 if version>=13 else 2)
  n=struct.unpack_from('<i',data,at)[0];at+=4
  if not n:continue
  stride=(len(data)-at)//n
  if stride<56 or (len(data)-at)%n:raise ValueError('Unsupported prop record layout')
  versions.append({'version':version,'stride':stride,'count':n})
  for i in range(n):
   off=at+i*stride;pos=list(struct.unpack_from('<3f',data,off));rot=list(struct.unpack_from('<3f',data,off+12));model=struct.unpack_from('<H',data,off+24)[0];solid,propflags=struct.unpack_from('<BB',data,off+30);skin=struct.unpack_from('<i',data,off+32)[0]
   if model>=len(names):raise ValueError('Invalid static prop model index')
   # Flag 4 is runtime/DX-dependent NO_DRAW. Keep dependencies for the
   # receiving engine rather than permanently dropping the model here.
   if not world['sourceZ'][0]<=pos[2]<=world['sourceZ'][1]:continue
   instances.append({'id':f"{world['id']}:static:{i}",'model':names[model],'position':[a+c for a,c in zip(pos,world['offset'])],'rotation':rot,'skin':skin,'solid':solid!=0,'scale':1,'kind':'static'})
 entities=[dict(re.findall(r'"([^"\n]+)"\s*"([^"\n]*)"',e)) for e in re.findall(r'\{([^{}]*)\}',b.lump(0).decode(errors='replace'))]
 for i,e in enumerate(entities):
  if e.get('classname') not in ('prop_dynamic','prop_dynamic_override','prop_physics','prop_physics_override','prop_door_rotating'):continue
  model=e.get('model','').replace('\\','/').lower()
  if not model.startswith('models/') or not model.endswith('.mdl'):continue
  pos=[float(v) for v in e.get('origin','0 0 0').split()]
  if not world['sourceZ'][0]<=pos[2]<=world['sourceZ'][1]:continue
  instances.append({'id':f"{world['id']}:entity:{i}",'model':model,'position':[a+c for a,c in zip(pos,world['offset'])],'rotation':[float(v) for v in e.get('angles','0 0 0').split()],'skin':int(e.get('skin','0')),'solid':e.get('solid','6')!='0','scale':float(e.get('modelscale','1')),'kind':e['classname'],'targetname':e.get('targetname',''),'animation':e.get('DefaultAnim','')})
 result.append({'id':world['id'],'source':world['source'],'formats':versions,'instances':instances})
 print(world['id'],len(instances),'scenery instances',len(set(p['model'] for p in instances)),'models',versions)
out=root/'work/worlds/scenery.json';out.write_text(json.dumps(result,indent=2))
