// Repair the installed retail HUD's stray token in a local-only asset cache.
const fs=require('fs'),path=require('path'),{vpk}=require('./tools/installed-assets.cjs');
const common=process.argv[2];
const files=vpk(path.join(common,'Half-Life 2/ep2/ep2_pak_dir.vpk'));
const original=files.get('scripts/hudlayout.res');
if(!original)throw Error('Installed HL2 HUD layout missing');
const text=original().toString().replace(/("426")HudPoisonDamageIndicator/g,'$1');
const cache=path.resolve(__dirname,'../../work/runtime-assets/hl2-hud');
fs.mkdirSync(path.join(cache,'scripts'),{recursive:true});
fs.writeFileSync(path.join(cache,'scripts/hudlayout.res'),text);
console.log('Prepared local installed HL2 HUD layout; excluded from release.');
