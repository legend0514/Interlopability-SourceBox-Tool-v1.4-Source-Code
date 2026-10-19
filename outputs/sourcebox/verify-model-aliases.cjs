// Verify the locally prepared NPC aliases before any game can mount them.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'../../work/runtime-assets/hl1mp/models');
const seen=new Set();let count=0;
function walk(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){
 const file=path.join(dir,entry.name);if(entry.isDirectory()){walk(file);continue;}
 if(!entry.name.endsWith('.mdl'))continue;
 const b=fs.readFileSync(file),expected=path.relative(root,file).replaceAll('\\','/');
 if(b.length<76||b.toString('ascii',0,4)!=='IDST')throw Error('Invalid studio model '+expected);
 const name=b.toString('ascii',12,b.indexOf(0,12));
 if(name!==expected||seen.has(name))throw Error('Colliding or incorrect model identity: '+expected+' -> '+name);
 seen.add(name);const checksum=b.readInt32LE(8),base=file.slice(0,-4);
 for(const [suffix,offset] of [['.vvd',8],['.dx90.vtx',16]]){
  if(!fs.existsSync(base+suffix))continue; // Animation-only models have no mesh.
  const companion=fs.readFileSync(base+suffix);
  if(companion.length<offset+4||companion.readInt32LE(offset)!==checksum)throw Error('Model mesh checksum mismatch: '+expected+suffix);
 }
 count++;
}}
walk(path.join(root,'sourcebox/npc'));
console.log(`Verified ${count} unique NPC aliases and available mesh checksums.`);
