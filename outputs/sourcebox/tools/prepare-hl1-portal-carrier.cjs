const fs=require('fs'),path=require('path');const {vpk}=require('./installed-assets.cjs');
const root=path.resolve(__dirname,'../../..');
const inventory=JSON.parse(fs.readFileSync(path.join(__dirname,'../sheets/hl1mp.json'))).portalInventory;
if(inventory?.carrier!=='weapon_egon'||inventory.slot!==3)throw Error('Unsupported HL1 Portal Gun inventory configuration');
const files=vpk('C:/Program Files (x86)/Steam/steamapps/common/Half-Life 1 Source Deathmatch/hl1/hl1_pak_dir.vpk');
const source=files.get('scripts/weapon_egon.txt');if(!source)throw Error('Installed Egon weapon script missing');
let text=source().toString();
for(const [key,value]of Object.entries({printname:inventory.displayName,viewmodel:'models/sourcebox/portal2/v_portalgun.mdl',playermodel:'models/sourcebox/portal2/w_portalgun.mdl',bucket:String(inventory.slot-1),bucket_position:'0'})){
 const re=new RegExp('("'+key+'"\\s*)"[^"]*"');if(!re.test(text))throw Error('Missing weapon property '+key);text=text.replace(re,(_,prefix)=>prefix+'"'+value+'"');}
const dest=path.join(root,'work/runtime-assets/hl1mp/scripts/weapon_egon.txt');fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,text);
console.log('HL1 local Portal Gun carrier: weapon_egon, native slot 3; crowbar and pistol unchanged.');
