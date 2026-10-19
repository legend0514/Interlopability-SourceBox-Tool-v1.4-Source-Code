import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const filename=process.argv[2] || path.join(root,'work/runtime-assets/hl1mp/models/sourcebox/portal2/portal_ring.mdl');
const mdl=fs.readFileSync(filename);
if(mdl.length<152 || mdl.toString('ascii',0,4)!=='IDST') throw new Error('Invalid compiled ring model');
const mins=[0,4,8].map(n=>mdl.readFloatLE(104+n));
const maxs=[0,4,8].map(n=>mdl.readFloatLE(116+n));
// Adapters orient local +X toward the portal normal. StudioMDL's default
// rotation otherwise turns our YZ mesh into an edge-on XZ mesh.
for(const [actual,expected] of [...mins.map((v,i)=>[v,[0,-32,-64][i]]),...maxs.map((v,i)=>[v,[0,32,64][i]])]) {
  if(!Number.isFinite(actual)||Math.abs(actual-expected)>0.01) throw new Error(`Portal model axis/dimension mismatch: ${JSON.stringify({mins,maxs})}`);
}
console.log('Compiled portal ring matches local +X normal and 64 x 128 aperture. Visual acceptance remains separate.');
