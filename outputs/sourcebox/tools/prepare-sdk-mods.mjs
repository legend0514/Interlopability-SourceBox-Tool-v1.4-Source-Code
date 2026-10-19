import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const project=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const workspace=path.resolve(project,'../..');
const checkout=path.join(workspace,'work/sourcebox-sdk');
const common='C:/Program Files (x86)/Steam/steamapps/common';
const q=s=>'"'+s.replaceAll('\\','/')+'"';
const manifest=JSON.parse(fs.readFileSync(path.join(project,'sheets/sdk-builds.json')));
const worldHosts=JSON.parse(fs.readFileSync(path.join(project,'sheets/native-world-hosts.json')));
for(const game of process.argv.slice(2).length?process.argv.slice(2):['hl2','tf2']){
 if(!manifest.approvedGames.includes(game))throw Error('Unapproved SDK target: '+game);
 const mod=path.join(checkout,'game',manifest.targets[game].mod);
 for(const file of ['client.dll','server.dll'])if(!fs.existsSync(path.join(mod,'bin/x64',file)))throw Error('Missing SDK game binary: '+game+'/'+file);
 const adapter=path.join(project,'bin/sdk/sourcebox_'+game+'.dll');
 if(!fs.existsSync(adapter))throw Error('Build separate SDK adapter first: '+game);
 for(const dir of ['addons','maps','cfg','scripts/vscripts'])fs.mkdirSync(path.join(mod,dir),{recursive:true});
 fs.copyFileSync(adapter,path.join(mod,'addons/sourcebox_'+game+'.dll'));
 fs.copyFileSync(worldHosts[game]?.enabled?worldHosts[game].map:path.join(project,'maps',game,'sourcebox_room_001.bsp'),path.join(mod,'maps/sourcebox_room_001.bsp'));
 const entries=[['mod+mod_write','|gameinfo_path|.'],['game+game_write','|gameinfo_path|.'],['default_write_path','|gameinfo_path|.'],['gamebin','|gameinfo_path|bin']];
 for(const cache of ['hl1mp','tf2-soldier',...(game==='hl2'?['hl2-hud']:[])]){
  const p=path.join(workspace,'work/runtime-assets',cache);if(fs.existsSync(p))entries.push(['game',p]);
 }
 if(worldHosts[game]?.enabled){
  entries.push(['game',path.join(workspace,'work/runtime-assets/worlds-source1')],['game',path.join(workspace,'work/worlds/assets')]);
  // Foreign full-game VPKs also override HUD/UI scripts. Mount only the
  // extracted model/material dependencies used by these local map imports.
  entries.push(['game',path.join(workspace,'work/worlds/scenery/source')]);
 }
 if(game==='tf2'){
  for(const file of ['tf2_textures','tf2_sound_vo_english','tf2_sound_misc','tf2_misc'])entries.push(['game+mod',common+'/Team Fortress 2/tf/'+file+'.vpk']);
  entries.push(['game',common+'/Team Fortress 2/tf']);
  fs.copyFileSync(path.join(project,'adapters/tf2/sourcebox_portals.nut'),path.join(mod,'scripts/vscripts/sourcebox_portals.nut'));
 }
 for(const file of ['hl2_textures','hl2_sound_vo_english','hl2_sound_misc','hl2_misc'])entries.push(['game',common+'/Source SDK Base 2013 Multiplayer/hl2/'+file+'.vpk']);
 entries.push(['game',common+'/Source SDK Base 2013 Multiplayer/hl2'],['game',common+'/Half-Life 2/hl2'],['platform',common+'/Source SDK Base 2013 Multiplayer/platform/platform_misc.vpk'],['platform',common+'/Source SDK Base 2013 Multiplayer/platform']);
 const text=`"GameInfo"\n{\n game "Sourcebox ${game.toUpperCase()} SDK"\n type ${game==='hl2'?'singleplayer_only':'multiplayer_only'}\n DependsOnAppID ${game==='hl2'?220:440}\n ${game==='tf2'?'AdditionalLocalization tf\n ReplayRequired 1\n':''} FileSystem\n {\n  SteamAppId 243750\n  SearchPaths\n  {\n${entries.map(([key,value])=>'   '+key+' '+q(value)).join('\n')}\n  }\n }\n}\n`;
 fs.writeFileSync(path.join(mod,'gameinfo.txt'),text);
 fs.writeFileSync(path.join(mod,'cfg/valve.rc'),'exec config.cfg\nexec sourcebox_controls.cfg\nstuffcmds\n');
 fs.writeFileSync(path.join(mod,'cfg/sourcebox_controls.cfg'),'alias +sourcebox_use "+use; cmd sourcebox_take"\nalias -sourcebox_use "-use"\nbind e +sourcebox_use\nbind F6 "cmd sourcebox_resetprops"\n');
 if(game==='hl2'){
  fs.copyFileSync(path.join(workspace,'work/runtime-assets/hl1mp/scripts/weapon_sourcebox_portalgun.txt'),path.join(mod,'scripts/weapon_sourcebox_portalgun.txt'));
  const loadout=JSON.parse(fs.readFileSync(path.join(project,'sheets/hl2-gameplay.json')));
  fs.writeFileSync(path.join(mod,'cfg/sourcebox_loadout.cfg'),[...loadout.commands,...Object.entries(loadout.bindings).map(([k,v])=>'bind '+q(k)+' '+q(v))].join('\n')+'\n');
 }
 if(game==='tf2')fs.appendFileSync(path.join(mod,'cfg/sourcebox_controls.cfg'),'cl_hud_playerclass_playermodel_showed_confirm_dialog 1\ncl_hud_playerclass_use_playermodel 0\n');
 console.log(game+': isolated SDK mod prepared; runtime testing pending');
}

