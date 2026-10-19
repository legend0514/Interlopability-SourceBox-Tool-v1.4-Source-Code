import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const out=path.join(root,'work/phase6/portal-ring');fs.mkdirSync(out,{recursive:true});
let smd='version 1\nnodes\n0 "root" -1\nend\nskeleton\ntime 0\n0 0 0 0 0 0 0\nend\ntriangles\n';
const vertex=(r,z,t)=>`0 0 ${(r*Math.cos(t)).toFixed(6)} ${(z*Math.sin(t)).toFixed(6)} 1 0 0 0 0\n`;
for(let i=0;i<64;i++){const a=i*2*Math.PI/64,b=(i+1)*2*Math.PI/64;for(const triangle of [[vertex(32,64,a),vertex(32,64,b),vertex(28,60,b)],[vertex(32,64,a),vertex(28,60,b),vertex(28,60,a)]])smd+='portal_ring\n'+triangle.join('');}
smd+='end\n';fs.writeFileSync(path.join(out,'ring.smd'),smd);
fs.writeFileSync(path.join(out,'ring.qc'),'$modelname "sourcebox/portal2/portal_ring.mdl"\n$origin 0 0 0 -90\n$body body "ring.smd"\n$cdmaterials "sourcebox/portal2/"\n$sequence idle "ring.smd" fps 1 loop\n');
const material=path.join(root,'work/runtime-assets/hl1mp/materials/sourcebox/portal2');fs.mkdirSync(material,{recursive:true});
fs.writeFileSync(path.join(material,'portal_ring.vmt'),'"VertexLitGeneric" { "$basetexture" "vgui/white" "$model" "1" "$selfillum" "1" "$nocull" "1" }\n');

