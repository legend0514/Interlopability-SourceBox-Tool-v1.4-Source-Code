"""Decode installed VTF 7.5 textures and write standalone VTF 7.2 RGBA8888.
No version-header spoofing: resource tables, compression, and mip layout are
replaced with an actual legacy layout. Outputs are local-only installed assets.
Run with Blender's Python, which supplies SourceIO's numpy dependency.
"""
import sys,pathlib,importlib.util,struct
root=pathlib.Path(__file__).resolve().parents[3]
sourceio=root/'work/cs2/sourceio/SourceIO-master'
spec=importlib.util.spec_from_file_location('SourceIO',sourceio/'__init__.py',submodule_search_locations=[str(sourceio)])
module=importlib.util.module_from_spec(spec);sys.modules['SourceIO']=module;spec.loader.exec_module(module)
from SourceIO.library.utils.pylib.vtf import load_vtf_texture
import json,hashlib
entries=json.loads((root/'work/worlds/source-materials.json').read_text());out=root/'work/worlds/cs2-content';report={};errors=[]
for e in entries:
 if e['status']!='extracted':continue
 try:
  token=hashlib.sha256(e['name'].encode()).hexdigest()[:20];relative='materials/sourcebox/worlds/original/'+token
  pixels,w,h,floats=load_vtf_texture((root/'work/worlds/source-materials'/e['texture']).read_bytes())
  if floats or len(pixels)!=w*h*4:raise ValueError('Unsupported color texture format')
  # Uncompressed BGRA TGA, top-left origin. Preserve source UV layout.
  import numpy as np
  bgra=np.frombuffer(pixels,dtype=np.uint8).reshape(h,w,4)[:,:,[2,1,0,3]].tobytes()
  tga=struct.pack('<BBBHHBHHHHBB',0,0,2,0,0,0,0,0,w,h,32,40)+bgra
  target=out/(relative+'.tga');target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(tga)
  material='"Layer0"\n{\n "Shader" "csgo_complex.vfx"\n "TextureColor" "'+relative+'.tga"\n "g_flRoughnessScale" "0.85"\n "F_RENDER_BACKFACES" "1"\n'
  if e['alphaTest']:material+=' "F_ALPHA_TEST" "1"\n'
  if e['translucent']:material+=' "F_TRANSLUCENT" "1"\n'
  material+='}\n';target.with_suffix('.vmat').write_text(material);report[e['name']]=relative+'.vmat'
 except Exception as error:errors.append({'name':e['name'],'error':str(error)})
(root/'outputs/sourcebox/sheets/cs2-world-material-overrides.json').write_text(json.dumps(report,indent=2))
(root/'work/worlds/material-conversion-results.json').write_text(json.dumps({'converted':len(report),'errors':errors,'procedural':[e for e in entries if e['status']!='extracted']},indent=2))
print('MATERIALS',len(report),'FAILED',len(errors),flush=True)
if errors:raise RuntimeError(str(errors))

