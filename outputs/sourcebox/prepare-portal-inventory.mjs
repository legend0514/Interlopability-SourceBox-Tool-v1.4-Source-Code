import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {preflight} from './preflight.mjs';
const root=path.dirname(fileURLToPath(import.meta.url)),config=preflight().portals;
// HL2 exposes the SDK's stock developer-tool weapon to both server and client.
// Its native weapon inventory entry is unused by the ordinary HL2 arsenal.
// Only the isolated Sourcebox lab overrides its script, never retail assets.
if(config.inventoryMode!=='native-slot')throw Error('Native inventory required');
const folder=path.join(root,'labs/hl2/scripts');fs.mkdirSync(folder,{recursive:true});
const script=`WeaponData
{
 "printname" "Sourcebox Portal Gun"
 "viewmodel" "models/sourcebox/portal2/v_portalgun.mdl"
 "playermodel" "models/sourcebox/portal2/w_portalgun.mdl"
 "anim_prefix" "physgun"
 "bucket" "5"
 "bucket_position" "2"
 "clip_size" "-1"
 "clip2_size" "-1"
 "default_clip" "-1"
 "default_clip2" "-1"
 "primary_ammo" "None"
 "secondary_ammo" "None"
 "weight" "0"
 "item_flags" "0"
 TextureData
 {
  "weapon" { "file" "sprites/w_icons1" "x" "128" "y" "128" "width" "128" "height" "64" }
  "weapon_s" { "file" "sprites/w_icons1b" "x" "128" "y" "128" "width" "128" "height" "64" }
  "crosshair" { "file" "sprites/crosshairs" "x" "0" "y" "48" "width" "24" "height" "24" }
 }
}
`;
fs.writeFileSync(path.join(folder,'weapon_cubemap.txt'),script);
const hl1=path.resolve(root,'../../work/runtime-assets/hl1mp/scripts');fs.mkdirSync(hl1,{recursive:true});fs.writeFileSync(path.join(hl1,'weapon_sourcebox_portalgun.txt'),script);
console.log('Prepared HL2 native inventory candidate. Portal service enabled; gameplay acceptance tracked separately.');
