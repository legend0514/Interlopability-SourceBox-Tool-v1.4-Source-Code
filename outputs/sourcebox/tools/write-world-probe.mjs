// Opt-in native-engine fixture. It exercises real input, traces and traversal;
// console positions/screenshots must be inspected separately from unit tests.
import fs from 'node:fs';
const [game,output]=process.argv.slice(2);
const weapon={hl2:'weapon_sourcebox_portalgun',tf2:'tf_weapon_grapplinghook',hl1mp:'weapon_egon',portal2:'weapon_portalgun'}[game];
if(!weapon||!output)throw Error('Expected supported game and output cfg');
const worlds=JSON.parse(fs.readFileSync(new URL('../sheets/worlds-expanded.json',import.meta.url))).worlds;
const lines=['sv_allow_wait_command 1','wait 600','gameui_hide'];
if(game==='tf2')lines.push('jointeam red','joinclass soldier','wait 300');
lines.push('use '+weapon,'wait 90');
for(const w of worlds){
 lines.push('setpos -128 0 2','setang 0 0 0','wait 60','+use','+reload','wait 15','-reload','-use','wait 30','+attack','wait 15','-attack','wait 90',
  'echo SOURCEBOX_WORLD_PROBE_ENTRY_'+w.id,'getpos','jpeg sourcebox_'+game+'_'+w.id+'_entry',
  'setpos 440 0 2','setang 0 0 0','wait 30','+forward','wait 100','-forward','wait 30',
  'echo SOURCEBOX_WORLD_PROBE_RESULT_'+w.id,'getpos','jpeg sourcebox_'+game+'_'+w.id+'_arrival','wait 45');
}
lines.push('-forward','-use','-reload','-attack','-attack2','setpos -128 0 2','setang 0 0 0','echo SOURCEBOX_WORLD_PROBE_FINISHED');
fs.writeFileSync(output,lines.join('\n')+'\n');
