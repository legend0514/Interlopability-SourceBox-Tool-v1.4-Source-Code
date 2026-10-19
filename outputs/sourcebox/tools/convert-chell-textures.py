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
src=root/'work/phase6/portal2-assets/materials'
dest=root/'work/runtime-assets/hl1mp/materials'
dest.mkdir(parents=True,exist_ok=True)
for file in src.rglob('*.vtf'):
 original=file.read_bytes();pixels,width,height,is_float=load_vtf_texture(original)
 if is_float:raise ValueError('Unexpected floating-point Chell texture: '+file.name)
 if len(pixels)!=width*height*4:raise ValueError('Invalid decoded RGBA length')
 header=bytearray(80);header[0:4]=b'VTF\0';struct.pack_into('<IIIHHIHH',header,4,7,2,80,width,height,struct.unpack_from('<I',original,20)[0]&0x3383,1,0)
 struct.pack_into('<3f',header,32,*struct.unpack_from('<3f',original,32));struct.pack_into('<fI',header,48,1.0,0)
 header[56]=1;struct.pack_into('<I',header,57,0xffffffff);struct.pack_into('<H',header,63,1)
 output=dest/file.relative_to(src);output.parent.mkdir(parents=True,exist_ok=True);output.write_bytes(header+pixels)
 print('LEGACY_TEXTURE',file.name,width,height)
# EyeRefract needs native eyeball metadata omitted by the portable rig compiler.
# Use the original iris texture with a supported ordinary material instead.
dest=dest/'models/player/chell'
for eye in ['l','r']:
 (dest/f'gambler_eyeball_{eye}.vmt').write_text('"VertexLitGeneric"\n{\n "$basetexture" "models/player/chell/gambler_eyes"\n "$halflambert" "1"\n}\n')
