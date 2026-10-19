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
from SourceIO.library.utils.pylib.vtf import VTFFile
import json
src=root/'work/worlds/legacy-input/materials'
dest=root/'work/runtime-assets/worlds-source1/materials'
results=[]
for file in src.rglob('*.vtf'):
 try:
  raw=file.read_bytes();original=VTFFile();original.load(str(file))
  # Rebuild the legacy sequential mip/frame/face payload; remove resource table.
  depth=struct.unpack_from('<H',raw,63)[0]
  if depth!=1:raise ValueError('Volume texture needs slice-preserving conversion')
  header=bytearray(raw[:80]);struct.pack_into('<III',header,4,7,2,80)
  struct.pack_into('<I',header,57,0xffffffff);header[61]=header[62]=0
  # Source's 7.2 loader expects seven stored faces at each mip, including the
  # obsolete sphere-map slot. 7.5 omits that slot. A header change alone leaves
  # the six native faces misaligned and causes out-of-bounds reads in engine.
  legacy_faces=7 if original.face_count==6 else original.face_count
  if original.face_count==6:struct.pack_into('<H',header,26,0)
  header[65:80]=bytes(15)
  count=struct.unpack_from('<I',raw,68)[0]
  resources=[struct.unpack_from('<II',raw,80+i*8) for i in range(count)]
  imageOffset=next(offset for tag,offset in resources if tag==0x30)
  bpp={0:4,1:4,2:3,3:3,4:2,5:1,6:2,8:1,9:3,10:3,11:4,12:4,16:4,17:2,18:2,19:2,21:2,22:2,23:4,24:8,25:8,26:4,27:4,28:12,29:16}
  def mip_size(mip):
   w=max(1,original.width>>mip);h=max(1,original.height>>mip)
   if original.format in (13,14,15,20,37,38):return ((w+3)//4)*((h+3)//4)*(8 if original.format in (13,20,38) else 16)
   return w*h*bpp[original.format]
  size=sum(mip_size(m) for m in range(original.mipmap_count))*original.frame_count*original.face_count
  if imageOffset+size>len(raw):raise ValueError('Image resource truncated')
  payload=raw[imageOffset:imageOffset+size]
  if original.face_count==6:
   parts=[];at=0
   for mip in reversed(range(original.mipmap_count)):
    face_size=mip_size(mip)
    for frame in range(original.frame_count):
     faces=payload[at:at+face_size*6];at+=face_size*6
     parts.extend([faces,faces[:face_size]])
   if at!=len(payload):raise ValueError('Cubemap payload layout mismatch')
   payload=b''.join(parts)
  data=bytes(header)+payload
  output=dest/file.relative_to(src);output.parent.mkdir(parents=True,exist_ok=True)
  check=VTFFile();check.load_bytes(data)
  if struct.unpack_from('<II',data,4)!=(7,2):raise ValueError('Legacy version mismatch')
  if check.face_count!=legacy_faces:raise ValueError('Legacy cubemap face count mismatch')
  for attr in ('width','height','frame_count','mipmap_count','format'):
   if getattr(original,attr)!=getattr(check,attr):raise ValueError('Texture layout mismatch: '+attr)
  for frame in range(original.frame_count):
   for face in range(original.face_count):
    for mip in range(original.mipmap_count):
     if original.get_data(frame,face,0,mip)!=check.get_data(frame,face,0,mip):raise ValueError('Texture payload mismatch')
  output.write_bytes(data);results.append({'file':str(file.relative_to(src)),'status':'converted'})
 except Exception as error:results.append({'file':str(file.relative_to(src)),'status':'failed','error':str(error)})
(root/'work/worlds/legacy-conversion-results.json').write_text(json.dumps(results,indent=2))
failures=[r for r in results if r['status']=='failed']
print('Converted',len(results)-len(failures),'failed',len(failures),flush=True)
if failures:raise RuntimeError(json.dumps(failures))


