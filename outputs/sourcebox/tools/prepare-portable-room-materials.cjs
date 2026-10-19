// Original procedural grid textures; no imported game imagery.
const fs=require('fs'),path=require('path');
const root=path.join(process.argv[2],'materials/sourcebox/room');fs.mkdirSync(root,{recursive:true});
for(const [name,color] of [['dev_measurewall01a',[245,151,18]],['dev_measuregeneric01b',[115,115,115]]]){
 const size=128,bytes=Buffer.alloc(18+size*size*3);bytes[2]=2;bytes.writeUInt16LE(size,12);bytes.writeUInt16LE(size,14);bytes[16]=24;bytes[17]=32;
 for(let y=0;y<size;y++)for(let x=0;x<size;x++){const rgb=x%64===0||y%64===0?[225,225,225]:color;const offset=18+(y*size+x)*3;bytes[offset]=rgb[2];bytes[offset+1]=rgb[1];bytes[offset+2]=rgb[0];}
 fs.writeFileSync(path.join(root,name+'.tga'),bytes);
 fs.writeFileSync(path.join(root,name+'.vmat'),`"Layer0" { "Shader" "csgo_lightmappedgeneric.vfx" "TextureLayer1Color" "materials/sourcebox/room/${name}.tga" "F_LAYERS" "0" "g_vLayer1Tint" "[1 1 1]" "SystemAttributes" { "PhysicsSurfaceProperties" "concrete" } }`);
}
