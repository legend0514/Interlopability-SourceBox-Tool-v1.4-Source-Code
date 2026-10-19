import fs from 'node:fs';
const root='work/sourcebox-sdk/src/';
const file=root+'vpc_scripts/projects.vgc';let s=fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n');
for(const name of ['client','server'])if(!s.includes(name+'_hl2.vpc'))s=s.replace('$Project "'+name+'"\n{','$Project "'+name+'"\n{\n\t"game\\'+name+'\\'+name+'_hl2.vpc" [$HL2]');
if(!s.includes('launcher_main_mod_hl2.vpc'))s=s.replace('$Project "launcher_main"\n{','$Project "launcher_main"\n{\n\t"launcher_main\\launcher_main_mod_hl2.vpc" [$HL2]');fs.writeFileSync(file,s);
let launcher=fs.readFileSync(root+'launcher_main/launcher_main_mod_hl2mp.vpc','utf8').replace('$Macro OUTBINNAME "mod_hl2mp"','$Macro OUTBINNAME "mod_hl2"').replace('Launcher Main (Mod HL2MP)','Launcher Main (Sourcebox HL2)');fs.writeFileSync(root+'launcher_main/launcher_main_mod_hl2.vpc',launcher);

