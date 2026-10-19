import fs from 'node:fs';
const root=new URL('./',import.meta.url);
const sheet=JSON.parse(fs.readFileSync(new URL('sheets/hl1mp.json',root)));
const room=JSON.parse(fs.readFileSync(new URL('sheets/room.json',root)));
for(const [game,spawn,classname] of [['hl1mp',sheet.spawn,'info_player_deathmatch'],['gmod',room.gmodSpawn,'info_player_start']]){
 const b=fs.readFileSync(new URL('maps/hl2/sourcebox_room_001.bsp',root));
 const offset=b.readInt32LE(8),length=b.readInt32LE(12);
 let entities=b.toString('utf8',offset,offset+length).replace(/\0+$/,'');
 entities=entities.replace(/\{[^{}]*"classname"\s*"info_player_start"[^{}]*\}/g,block=>block.replace('"info_player_start"',`"${classname}"`).replace(/"origin"\s*"[^"]*"/,`"origin" "${spawn.join(' ')}"`));
 entities=entities.replace(/\{[^{}]*"classname"\s*"prop_physics"[^{}]*\}/g,'');
 const data=Buffer.from(entities+'\0');b.writeInt32LE(b.length,8);b.writeInt32LE(data.length,12);
 fs.mkdirSync(new URL(`maps/${game}/`,root),{recursive:true});
 fs.writeFileSync(new URL(`maps/${game}/sourcebox_room_001.bsp`,root),Buffer.concat([b,data]));
 console.log(`Prepared ${game} room: spawn ${spawn.join(' ')}, shared collision geometry`);
}
