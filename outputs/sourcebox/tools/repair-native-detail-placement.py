"""Repair generated detail model placement without rebuilding unrelated BSP lumps.
StudioMDL rotates SMD XY by +90 degrees; compensate on the placed instance.
Only the named SourceBox detail family is changed. Repeated runs are identical.
"""
import pathlib,struct,argparse

def repair(data):
 b=bytearray(data)
 if b[:4]!=b'VBSP':raise ValueError('Expected Source BSP')
 off,size=struct.unpack_from('<ii',b,8+35*16)
 changed=found=0
 for i in range(struct.unpack_from('<i',b,off)[0]):
  ident,flags,ver,p,length=struct.unpack_from('<IHHii',b,off+4+i*16)
  if ident!=int.from_bytes(b'sprp','big'):continue
  if flags&1 or ver!=10:raise ValueError('Expected uncompressed host static props v10')
  count=struct.unpack_from('<i',b,p)[0];names=[b[p+4+j*128:p+4+(j+1)*128].split(b'\0')[0].decode() for j in range(count)]
  at=p+4+count*128;leaves=struct.unpack_from('<i',b,at)[0];at+=4+leaves*2
  count=struct.unpack_from('<i',b,at)[0];at+=4
  if at+count*72!=p+length:raise ValueError('Unexpected prop record size')
  for j in range(count):
   pos=at+j*72;model=struct.unpack_from('<H',b,pos+24)[0]
   if not names[model].startswith('models/sourcebox/world_detail/ctf_2fort_detail_'):continue
   found+=1;angles=struct.unpack_from('<3f',b,pos+12)
   if angles==(0.,-90.,0.):continue
   if angles!=(0.,0.,0.):raise ValueError('Unexpected detail orientation')
   struct.pack_into('<3f',b,pos+12,0.,-90.,0.);changed+=1
 if found!=93:raise ValueError(f'Expected 93 architecture sections, found {found}')
 return bytes(b),changed

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('bsp');a=p.parse_args();file=pathlib.Path(a.bsp)
 old=file.read_bytes();new,count=repair(old)
 if count:
  backup=file.with_suffix('.before-detail-repair.bsp')
  if not backup.exists():backup.write_bytes(old)
  file.write_bytes(new)
 print(f'Corrected {count} sections; 93 checked')
