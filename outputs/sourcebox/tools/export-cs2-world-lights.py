"""Retain installed maps' interior light positions; add bounded outdoor fill."""
import importlib.util,json,re,pathlib,math
root=pathlib.Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('bsp',pathlib.Path(__file__).with_name('export-worlds.py'))
bsp=importlib.util.module_from_spec(spec);spec.loader.exec_module(bsp)
registry=json.loads((root/'outputs/sourcebox/sheets/worlds-expanded.json').read_text())
lighting=json.loads((root/'outputs/sourcebox/sheets/cs2-world-lighting.json').read_text())
for key in ('maxLightsPerWorld','minimumLightSpacing','sourceBrightnessDivisor','minimumBrightness','maximumBrightness','interiorRange','outdoorBrightness','arrivalBrightness'):
 if not isinstance(lighting.get(key),(int,float)) or not math.isfinite(lighting[key]) or lighting[key]<=0:raise ValueError('Invalid lighting setting: '+key)
if not isinstance(lighting['maxLightsPerWorld'],int) or lighting['maxLightsPerWorld']>128 or lighting['minimumBrightness']>lighting['maximumBrightness']:raise ValueError('Invalid light budget/range')
lights=[]
for w in registry['worlds']:
 data=json.loads((root/'outputs/sourcebox/worlds'/f"{w['id']}.json").read_text());lo,hi=data['bounds'];local=[]
 for entity in re.findall(r'\{([^{}]*)\}',bsp.BSP(w['source']).lump(0).decode('utf8',errors='replace')):
  kv=dict(re.findall(r'"([^"\n]+)"\s*"([^"\n]*)"',entity))
  if kv.get('classname') not in ('light','light_spot'):continue
  try:
   source=[float(v) for v in kv['origin'].split()];p=[a+b for a,b in zip(source,w['offset'])]
   if len(p)!=3 or not all(lo[i]-32<=p[i]<=hi[i]+32 for i in range(3)):continue
   if any(math.dist(p,old['origin'])<lighting['minimumLightSpacing'] for old in local):continue
   color=[float(v) for v in kv.get('_light','255 240 220 200').split()]
   if len(color)<4:color=[255,240,220,200]
   local.append({'world':w['id'],'origin':p,'color':[int(max(0,min(255,v))) for v in color[:3]],'brightness':max(lighting['minimumBrightness'],min(lighting['maximumBrightness'],color[3]/lighting['sourceBrightnessDivisor'])),'range':lighting['interiorRange'],'source':'installed light'})
  except (ValueError,KeyError):continue
 # Keep the bake bounded; these are static lights, not per-frame shadow lights.
 lights.extend(local[:lighting['maxLightsPerWorld']])
 # Four broad sky-fill sources per outdoor map, below higher stacked worlds.
 if w['id'] in ('gm_construct','gm_flatgrass','ctf_2fort'):
  width=max(hi[0]-lo[0],hi[1]-lo[1])
  for x in (0.25,0.75):
   for y in (0.25,0.75):
    lights.append({'world':w['id'],'origin':[lo[0]+(hi[0]-lo[0])*x,lo[1]+(hi[1]-lo[1])*y,hi[2]+256],'color':[235,242,255],'brightness':lighting['outdoorBrightness'],'range':max(2048,width),'source':'outdoor fill'})
 # Arrival area always gets a local fill, including maps without light entities.
 lights.append({'world':w['id'],'origin':[w['spawn'][0],w['spawn'][1],w['spawn'][2]+96],'color':[255,244,225],'brightness':lighting['arrivalBrightness'],'range':lighting['interiorRange'],'source':'arrival fill'})
out=root/'work/worlds/cs2-content/lights.json';out.write_text(json.dumps(lights,indent=2))
print('Exported',len(lights),'static CS2 lights')
