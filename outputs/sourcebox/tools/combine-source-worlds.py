"""Compose native Source 1 host maps from locally reconstructed VMFs.
Preserve original surfaces/UVs/displacements. Runtime gameplay entities are not
silently baked into static substitutes: their inventory is emitted separately.
"""
import json,re,pathlib,copy,argparse,math
ROOT=pathlib.Path(__file__).resolve().parents[3]
TOKEN=re.compile(r'//[^\n]*|"(?:\\.|[^"\\])*"|[{}]|[^\s{}"]+')
def parse(text):
 tokens=[t for t in TOKEN.findall(text) if not t.startswith('//')];at=0
 def atom(t):return t[1:-1].replace('\\"','"').replace('\\\\','\\') if t.startswith('"') else t
 def block(nested=False):
  nonlocal at
  out=[]
  while at<len(tokens):
   if tokens[at]=='}':
    if not nested:raise ValueError('Unexpected closing brace')
    at+=1;return out
   key=atom(tokens[at]);at+=1
   if at>=len(tokens):raise ValueError('Missing value')
   val=tokens[at];at+=1
   out.append((key,block(True) if val=='{' else atom(val)))
  if nested:raise ValueError('Unclosed block')
  return out
 return block()
def value(node,key,default=None):return next((v for k,v in node if k==key),default)
def setval(node,key,new):
 for i,(k,v) in enumerate(node):
  if k==key:node[i]=(k,new);return
 node.append((key,new))
def serialize(node,depth=0):
 q=lambda s:'"'+str(s).replace('\\','\\\\').replace('"','\\"')+'"';out=[];indent='\t'*depth
 for k,v in node:
  if isinstance(v,list):out.extend([indent+k,indent+'{',serialize(v,depth+1),indent+'}'])
  else:out.append(indent+q(k)+' '+q(v))
 return '\n'.join(out)
def vector(s):return [float(v) for v in s.strip('[]() ').split()]
def fmt(v):return ' '.join(f'{x:.8g}' for x in v)
def shift(node,offset):
 out=[]
 for k,v in node:
  if isinstance(v,list):v=shift(v,offset)
  elif k=='plane':v=' '.join('('+fmt([a+b for a,b in zip(vector(p),offset)])+')' for p in re.findall(r'\([^)]*\)',v))
  elif k in ('origin','startposition'):
   nums=vector(v)
   if len(nums)==3:v=('[' if k=='startposition' else '')+fmt([a+b for a,b in zip(nums,offset)])+(']' if k=='startposition' else '')
  elif k in ('uaxis','vaxis'):
   match=re.fullmatch(r'\[([^]]+)\]\s+([^ ]+)',v)
   if match:
    axis=vector(match[1]);scale=float(match[2]);axis[3]-=sum(axis[i]*offset[i] for i in range(3))/scale;v='['+fmt(axis)+'] '+match[2]
  if k not in ('editor','visgroupid','connections'):out.append((k,v))
 return out
def solid_points(s):return [vector(p) for k,side in s if k=='side' for p in re.findall(r'\([^)]*\)',value(side,'plane',''))]
def keep(s,w):
 mats=[value(side,'material','').lower() for k,side in s if k=='side']
 if any('toolsskybox' in m or 'toolsareaportal' in m for m in mats):return False
 pts=solid_points(s)
 return bool(pts) and all(w['sourceZ'][0]-32<=p[2]<=w['sourceZ'][1]+32 and all(abs(p[i]+w['offset'][i])<15872 for i in range(3)) for p in pts)
def box(lo,hi):
 x,y,z=lo;X,Y,Z=hi
 faces=[[(x,y,z),(x,y,Z),(x,Y,Z)],[(X,Y,z),(X,Y,Z),(X,y,Z)],[(X,y,z),(X,y,Z),(x,y,Z)],[(x,Y,z),(x,Y,Z),(X,Y,Z)],[(x,Y,z),(X,Y,z),(X,y,z)],[(x,y,Z),(X,y,Z),(X,Y,Z)]]
 return [('side',[('plane',' '.join('('+fmt(p)+')' for p in reversed(f))),('material','tools/toolsskybox'),('uaxis','[1 0 0 0] 0.25'),('vaxis','[0 -1 0 0] 0.25'),('lightmapscale','64')]) for f in faces]
def fallback_geometry(node):
 """Keep planes, UVs, displacement and collision settings; replace visual surfaces."""
 out=[]
 for key,val in node:
  if isinstance(val,list):val=fallback_geometry(val)
  elif key=='material' and not val.lower().startswith('tools/'):
   val='sourcebox/fallback/topology'
  out.append((key,val))
 return out

def missing_worlds(profile):
 if profile is None:return set()
 data=json.loads(pathlib.Path(profile).read_text(encoding='utf-8-sig'))
 if data.get('version')!=1:raise ValueError('Unsupported content profile')
 result=set()
 for w in data['worlds']:
  if not re.fullmatch('[a-z0-9_]+',w['id']):raise ValueError('Invalid world ID')
  if w['mode'] not in ('installed-assets','topology-placeholder'):raise ValueError('Invalid content mode')
  if w['mode']=='topology-placeholder':
   if not w['topologyCached']:raise ValueError('No cached topology for '+w['id'])
   result.add(w['id'])
 return result

def build(game,profile=None):
 missing=missing_worlds(profile)
 registry=json.loads((ROOT/'outputs/sourcebox/sheets/worlds-expanded.json').read_text())
 detail_models=json.loads((ROOT/'work/worlds/source1-detail/models.json').read_text())
 basegame='hl2' if game in ('gmod','hl1mp') else game
 host=parse((ROOT/f'outputs/sourcebox/maps/{basegame}/sourcebox_room_001.vmf').read_text());world=value(host,'world');pending=[];report=[]
 # Portal 2 does not ship HL2's sky_day01_01. Use its own complete HDR sky.
 if game=='portal2':setval(world,'skyname','sky_l4d_c4m1_hdr')
 for w in registry['worlds']:
  src=parse((ROOT/f"work/worlds/decompiled/{w['id']}.vmf").read_text());added=0;scenery=0
  placeholder=w['id'] in missing
  if placeholder:src=fallback_geometry(src)
  for k,s in value(src,'world'):
   if k=='solid' and keep(s,w):world.append(('solid',shift(s,w['offset'])));added+=1
  for k,e in src:
   if k!='entity':continue
   cls=value(e,'classname','');solids=[s for key,s in e if key=='solid']
   if w['id']=='ctf_2fort' and cls=='func_detail' and not placeholder:continue
   if cls in ('func_detail','func_brush','func_wall','func_illusionary','func_reflective_glass') and solids:
    chosen=[s for s in solids if keep(s,w)]
    if not chosen:continue
    entity=shift([(key,v) for key,v in e if key not in ('solid','connections','parentname')],w['offset']);setval(entity,'classname','func_detail' if cls=='func_detail' else 'func_brush');setval(entity,'targetname','sourcebox_arch_'+w['id']+'_'+str(scenery))
    if cls=='func_illusionary':setval(entity,'Solidity','1')
    entity.extend(('solid',shift(s,w['offset'])) for s in chosen);host.append(('entity',entity));added+=len(chosen);scenery+=1
   elif cls in ('prop_static','prop_dynamic','prop_dynamic_override','light','light_spot'):
    if placeholder and cls.startswith('prop_'):continue
    origin=vector(value(e,'origin','0 0 0'))
    if len(origin)!=3 or not w['sourceZ'][0]<=origin[2]<=w['sourceZ'][1] or any(abs(origin[i]+w['offset'][i])>=15872 for i in range(3)):continue
    entity=shift(e,w['offset']);setval(entity,'targetname','sourcebox_arch_'+w['id']+'_'+str(scenery));entity=[(key,v) for key,v in entity if key not in ('parentname','hammerid','lightingorigin') and not (cls in ('light','light_spot') and key in ('targetname','style','pattern'))];host.append(('entity',entity));scenery+=1
   elif cls not in ('func_areaportal','func_areaportalwindow','func_occluder'):
    pending.append({'world':w['id'],'class':cls,'targetname':value(e,'targetname',''),'origin':value(e,'origin'),'model':value(e,'model'),'reason':'Requires shared gameplay adapter; not baked into a permanent obstacle'})
  report.append({'world':w['id'],'brushes':added,'sceneryAndLights':scenery,'contentMode':'topology-placeholder' if placeholder else 'installed-assets'})
 # One shared outdoor sun lights all native sky-enclosed destinations. Preserve
 # Flatgrass's installed daylight values instead of leaving outdoor maps unlit.
 daylight=parse((ROOT/'work/worlds/decompiled/gm_flatgrass.vmf').read_text())
 sun=next(copy.deepcopy(e) for k,e in daylight if k=='entity' and value(e,'classname')=='light_environment')
 setval(sun,'origin','-512 576 8780')
 host.append(('entity',sun))
 for i,model in enumerate(detail_models):
  if any('/'+world+'_detail_' in model['model'] for world in missing):continue
  host.append(('entity',[('classname','prop_static'),('model',model['model']),('origin',fmt(model['origin'])),('angles','0 0 0'),('solid','6'),('targetname','sourcebox_detail_'+str(i))]))
 # Separate native sky enclosures stop other worlds appearing in the distance
 # and keep the engine's visibility sets local to each destination.
 for w in registry['worlds']:
  bounds=json.loads((ROOT/f"outputs/sourcebox/worlds/{w['id']}.json").read_text())['bounds']
  # Render-face bounds omit model origins and hidden structural surfaces.
  # Enclose the full imported vertical band, including those native entities.
  x,y,z=[n-256 for n in bounds[0]];X,Y,Z=[n+256 for n in bounds[1]];t=16
  z=min(z,w['sourceZ'][0]+w['offset'][2]-256)
  for lo,hi in [([x,y,z-t],[X,Y,z]),([x,y,Z],[X,Y,Z+t]),([x-t,y,z],[x,Y,Z]),([X,y,z],[X+t,Y,Z]),([x,y-t,z],[X,y,Z]),([x,Y,z],[X,Y+t,Z])]:world.append(('solid',box(lo,hi)))
 host.append(('entity',[('classname','info_target'),('targetname','sourcebox_worlds_baked'),('origin','0 0 32')]))
 if game=='hl1mp':
  spawn=json.loads((ROOT/'outputs/sourcebox/sheets/hl1mp.json').read_text())['spawn']
  host=[(k,e) for k,e in host if not (k=='entity' and value(e,'classname')=='prop_physics')]
  for k,e in host:
   if k=='entity' and value(e,'classname')=='info_player_start':setval(e,'classname','info_player_deathmatch');setval(e,'origin',fmt(spawn))
 counter=0
 def ids(node):
  nonlocal counter
  for i,(k,v) in enumerate(node):
   if k=='id':counter+=1;node[i]=(k,str(counter))
   elif isinstance(v,list):
    if k in ('solid','side','entity') and value(v,'id') is None:counter+=1;v.insert(0,('id',str(counter)))
    ids(v)
 ids(host)
 out=ROOT/f'work/worlds/{"source1-fallback" if missing else "source1-host"}/{game}';out.mkdir(parents=True,exist_ok=True);(out/'sourcebox_room_001.vmf').write_text(serialize(host));(out/'report.json').write_text(json.dumps({'maps':report,'pendingGameplay':pending,'nativeTested':False},indent=2));print(game,report,'pendingGameplay',len(pending))
 if missing:
  material=ROOT/'work/worlds/source1-host/materials/sourcebox/fallback/topology.vmt';material.parent.mkdir(parents=True,exist_ok=True)
  material.write_text('"LightmappedGeneric"\n{\n "$basetexture" "vgui/white"\n "$color" "[0.32 0.38 0.43]"\n}\n')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('game',choices=['hl2','tf2','gmod','hl1mp','portal2']);p.add_argument('--content-profile');args=p.parse_args();build(args.game,args.content_profile)

