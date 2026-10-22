"""Build local-only world hosts. No machine paths, game installs or settings are mutated.

Input paths and selected games come from the launcher; all artifacts and compiler
logs stay in a new cache. Activation is a separate, validated launcher step.
"""
import argparse, hashlib, importlib.util, json, os, pathlib, re, shutil, subprocess, sys, zipfile, io

HERE=pathlib.Path(__file__).resolve().parent
def module(name, filename):
 spec=importlib.util.spec_from_file_location(name,HERE/filename); m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def run(argv,log,timeout=1800):
 log.parent.mkdir(parents=True,exist_ok=True)
 with log.open('w',encoding='utf-8') as out:
  p=subprocess.run([str(a) for a in argv],stdout=out,stderr=subprocess.STDOUT,timeout=timeout,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
 text=log.read_text(errors='replace')
 if p.returncode or re.search(r'\*\*\*\* leaked|MAX_MAP_|RESOURCE COMPILE ERROR|Error:',text):
  raise RuntimeError(f'{pathlib.Path(argv[0]).name} failed: {log}\n'+text[-1800:])
 return text
def safe_output(root,relative):
 p=(root/relative).resolve()
 if not p.is_relative_to(root.resolve()):raise ValueError('Unsafe embedded asset path')
 return p
def prepare(r):
 root=pathlib.Path(r['outputRoot']).resolve(); project=pathlib.Path(r['projectRoot']).resolve()
 if root.exists() and any(root.iterdir()):raise ValueError('World build requires a fresh output directory')
 root.mkdir(parents=True,exist_ok=True)
 cfg=r['registry']; ids=[w['id'] for w in cfg['worlds']]
 if not ids or len(ids)!=len(set(ids)) or any(not re.fullmatch('[a-z0-9_]+',i) for i in ids):raise ValueError('Invalid or empty destination selection')
 targets=r['targets']
 if not targets or any(g not in ('hl2','tf2','gmod','hl1mp','portal2','cs2') for g in targets):raise ValueError('Invalid target games')
 export=module('portable_bsp','export-worlds.py'); compose=module('portable_compose','combine-source-worlds.py'); light=module('portable_lighting','verify-world-lighting.py')
 compose.ROOT=root
 sheets=root/'outputs/sourcebox/sheets';sheets.mkdir(parents=True)
 (sheets/'worlds-expanded.json').write_text(json.dumps(cfg))
 shutil.copyfile(project/'sheets/hl1mp.json',sheets/'hl1mp.json')
 for game in ('hl2','tf2','portal2'):
  target=root/f'outputs/sourcebox/maps/{game}/sourcebox_room_001.vmf';target.parent.mkdir(parents=True)
  shutil.copyfile(project/f'maps/{game}/sourcebox_room_001.vmf',target)
 data=root/'outputs/sourcebox/worlds';data.mkdir(parents=True)
 decompiled=root/'work/worlds/decompiled';decompiled.mkdir(parents=True)
 assets=root/'assets';assets.mkdir()
 sources=[]
 for world in cfg['worlds']:
  source=pathlib.Path(world['source']);print('[SourceBox] Reading '+world['label']+' from the installed game...',flush=True)
  if not source.is_absolute() or not source.is_file():raise ValueError('Missing installed map: '+str(source))
  parsed=export.parse(source,world)
  (data/(world['id']+'.json')).write_text(json.dumps(parsed,separators=(',',':')))
  metadata={k:parsed[k] for k in ('id','bounds','spawn','offset','yaw')};metadata.update(chunks=[],collision=[])
  (data/(world['id']+'.metadata.json')).write_text(json.dumps(metadata))
  pak=export.BSP(source).lump(40)
  if pak:
   with zipfile.ZipFile(io.BytesIO(pak)) as archive:
    total=0
    for entry in archive.infolist():
     rel=entry.filename.replace('\\','/')
     if entry.is_dir() or not rel.startswith(('materials/','models/')):continue
     total+=entry.file_size
     if total>512*1024*1024:raise ValueError('Embedded asset archive exceeds limit')
     target=safe_output(assets,rel);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(archive.read(entry))
  run([r['java'],'-m','info.ata4.bspsrc.app/info.ata4.bspsrc.app.src.BspSourceLauncher','--no_areaportals','--no_occluders','--no_visclusters','-o',decompiled/(world['id']+'.vmf'),source],root/'logs'/(world['id']+'-decompile.log'))
  sources.append({'id':world['id'],'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
 hosts={}
 for game in targets:
  if game=='cs2':continue
  print('[SourceBox] Composing and lighting destinations for '+game+'...',flush=True)
  compose.build(game,portable=True)
  folder=root/'work/worlds/source1-host'/game
  mount=folder/'gameinfo.txt'
  paths=[str(assets),*r['mounts']]
  if any('"' in p or '\n' in p for p in paths):raise ValueError('Invalid mount')
  mount.write_text('"GameInfo" { game "SourceBox map preparation" FileSystem { SteamAppId 243750 SearchPaths {\n'+'\n'.join('Game "'+p.replace('\\','/')+'"' for p in paths)+'\n} } }')
  compiler=pathlib.Path(r['compilers'][game]);base=folder/'sourcebox_room_001'
  for tool,flags in [('vbsp',['-threads','2']),('vvis',['-fast','-threads','2']),('vrad',['-fast','-bounce','2','-threads','2','-both'])]:
   print('[SourceBox] '+game+': '+tool+'...',flush=True)
   text=run([compiler/(tool+'.exe'),*flags,'-game',folder,base],root/'logs'/(game+'-'+tool+'.log'))
   if tool=='vbsp':
    missing=sorted(set(re.findall(r'Material not found!:\s*([^\r\n]+)',text)))
    if missing:raise RuntimeError('Destination materials are unresolved for '+game+': '+', '.join(missing[:8])+'. See '+str(root/'logs'/(game+'-'+tool+'.log')))
  bsp=base.with_suffix('.bsp')
  if not light.inspect(bsp)['complete']:raise ValueError('Incomplete lighting: '+game)
  hosts[game]={'enabled':True,'map':str(bsp),'sha256':hashlib.sha256(bsp.read_bytes()).hexdigest(),'acceptance':'Compiled locally; native gameplay not yet tested'}
 if 'cs2' in targets:
  print('[SourceBox] Converting destination topology for CS2...',flush=True)
  compose.build('hl2',portable=True,output_game='cs2',base_path=project/'maps/cs2/sourcebox_room_001.vmf')
  source=root/'work/worlds/source1-host/cs2/sourcebox_room_001.vmf'
  doc=compose.parse(source.read_text())
  # Source 1 model binaries and shaders cannot be imported as Source 2 assets.
  # Preserve brush interiors and displacement topology with a neutral material;
  # report omitted scenery explicitly rather than substituting ERROR models.
  omitted=[]
  def topology(node):
   result=[]
   for key,val in node:
    if isinstance(val,list):
     if key=='entity' and compose.value(val,'classname','').startswith('prop_'):
      omitted.append(compose.value(val,'model',''));continue
     val=topology(val)
    elif key=='material' and not val.lower().startswith('tools/'):val='dev/dev_measuregeneric01b'
    elif key=='targetname' and val=='sourcebox_worlds_baked':val='sourcebox_worlds_ready'
    result.append((key,val))
   return result
  vmf=root/'cs2-topology.vmf';vmf.write_text(compose.serialize(topology(doc)))
  metadata=root/'cs2-world-metadata.json';metadata.write_text(json.dumps([json.loads((data/(i+'.metadata.json')).read_text()) for i in ids]))
  csrequest=root/'cs2-request.json';csoutput=root/'cs2'
  csrequest.write_text(json.dumps({'csRoot':r['csRoot'],'hl2Root':r['hl2Root'],'outputRoot':str(csoutput),'nodePath':r['nodePath'],'vmfPath':str(vmf),'worldMetadata':str(metadata)}))
  run([r['powershell'],'-NoProfile','-ExecutionPolicy','Bypass','-File',HERE/'prepare-portable-cs2-room.ps1','-Request',csrequest],root/'logs/cs2-compile.log',3600)
  vpk=csoutput/'maps/sourcebox_room_001.vpk'
  if not vpk.is_file():raise ValueError('Missing compiled Source 2 map')
  hosts['cs2']={'enabled':True,'map':str(vpk),'sha256':hashlib.sha256(vpk.read_bytes()).hexdigest(),'content':str(csoutput),'mode':'topology-only','omittedScenery':sorted(set(omitted)),'acceptance':'Compiled locally; native gameplay not yet tested'}
 (root/'prepared-worlds.json').write_text(json.dumps({'version':1,'registry':cfg,'hosts':hosts,'assets':str(assets),'metadata':str(data),'mounts':r['mounts'],'sources':sources,'nativeGameplayVerified':False},indent=2))
 print('[SourceBox] Destination maps compiled; ready for installation and native testing.',flush=True)
if __name__=='__main__':
 args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
 r=json.loads(pathlib.Path(args[0]).read_text(encoding='utf-8-sig'));prepare(r)
