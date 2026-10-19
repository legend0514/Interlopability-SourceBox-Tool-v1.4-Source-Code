"""Convert local Source 1 albedo and normal maps into Source 2 materials."""
import sys,json,pathlib,importlib.util,struct,re
request=json.loads(pathlib.Path(sys.argv[sys.argv.index('--')+1]).read_text(encoding='utf-8-sig'))
sourceio=pathlib.Path(request['sourceioRoot'])
spec=importlib.util.spec_from_file_location('SourceIO',sourceio/'__init__.py',submodule_search_locations=[str(sourceio)])
module=importlib.util.module_from_spec(spec);sys.modules['SourceIO']=module;spec.loader.exec_module(module)
from SourceIO.library.utils.pylib.vtf import load_vtf_texture
import numpy as np
source=pathlib.Path(request['input']);destination=pathlib.Path(request['content'])
def field(text,name):
 match=re.search(r'"?\$'+name+r'"?\s+"?([^"\s{}]+)',text,re.I)
 return match[1].replace('\\','/').lower() if match else None
def texture(name):
 name=name.removeprefix('materials/').removesuffix('.vtf')
 if '..' in name.split('/') or ':' in name:raise ValueError('Invalid texture reference')
 relative='materials/'+name+'.tga';target=destination/relative
 if not target.exists():
  pixels,w,h,floats=load_vtf_texture((source/('materials/'+name+'.vtf')).read_bytes())
  if floats or len(pixels)!=w*h*4:raise ValueError('Unsupported texture: '+name)
  bgra=np.frombuffer(pixels,dtype=np.uint8).reshape(h,w,4)[:,:,[2,1,0,3]].tobytes()
  target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(struct.pack('<BBBHHBHHHHBB',0,0,2,0,0,0,0,0,w,h,32,40)+bgra)
 return relative
for file in (source/'materials').rglob('*.vmt'):
 text=file.read_text(errors='replace');color=field(text,'basetexture');normal=field(text,'bumpmap')
 if not color:continue
 target=destination/file.relative_to(source).with_suffix('.vmat');target.parent.mkdir(parents=True,exist_ok=True)
 material='"Layer0" { "Shader" "csgo_complex.vfx" "TextureColor" "'+texture(color)+'" "g_flRoughnessScale" "0.8"'
 if normal and (source/('materials/'+normal.removesuffix('.vtf')+'.vtf')).exists():material+=' "TextureNormal" "'+texture(normal)+'"'
 if field(text,'alphatest')=='1':material+=' "F_ALPHA_TEST" "1"'
 if field(text,'translucent')=='1':material+=' "F_TRANSLUCENT" "1"'
 target.write_text(material+' }')
 print('MATERIAL',file.relative_to(source),flush=True)
