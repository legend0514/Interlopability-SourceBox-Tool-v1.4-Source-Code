import fs from 'node:fs';
const sdk='work/sourcebox-sdk/src/game/';
// Class changes validate every inventory item, including custom carriers.
edit(sdk+'server/tf/tf_player.cpp',s=>{
 if(s.includes('// Sourcebox: reject weapons without schema data'))return s;
 const start=s.indexOf('void CTFPlayer::ValidateWeapons(');
 const needle='\t\tint iLoadoutSlot = pWeapon->GetAttributeContainer()->GetItem()->GetStaticData()->GetLoadoutSlot( GetPlayerClass()->GetClassIndex() );';
 const index=s.indexOf(needle,start);
 if(start<0||index<0)throw Error('Missing TF2 inventory validation hook');
 const guard=`\t\t// Sourcebox: reject weapons without schema data before loadout lookup.
\t\tif ( !pWeapon->GetAttributeContainer()->GetItem()->GetStaticData() )
\t\t{
\t\t\tWarning( "[Sourcebox] Removing weapon without item definition during loadout validation: %s\\n", pWeapon->GetClassname() );
\t\t\tif ( GetActiveTFWeapon() == pWeapon )
\t\t\t\tSwitchToNextBestWeapon( pWeapon );
\t\t\tWeapon_Detach( pWeapon );
\t\t\tUTIL_Remove( pWeapon );
\t\t\tcontinue;
\t\t}

`;
 return s.slice(0,index)+guard+s.slice(index);
});
for(const side of ['client','server'])fs.copyFileSync(`outputs/sourcebox/adapters/sdk/sourcebox_portal_${side}.inc`,`${sdk}${side}/sourcebox_portal_${side}.inc`);
fs.copyFileSync('outputs/sourcebox/adapters/sdk/sourcebox_portal_gun.inc',sdk+'server/sourcebox_portal_gun.inc');
function edit(file,fn){let s=fs.readFileSync(file,'utf8').replaceAll('\r\n','\n');fs.writeFileSync(file,fn(s));}
edit(sdk+'server/point_camera.cpp',s=>s.includes('#include "sourcebox_portal_server.inc"')?s:s+'\n#include "sourcebox_portal_server.inc"\n');
edit(sdk+'server/weapon_cubemap.cpp',s=>s.includes('#include "sourcebox_portal_gun.inc"')?s:s+'\n#include "sourcebox_portal_gun.inc"\n');
// The isolated TF2 mod uses its existing networked weapon carrier, with genuine
// first-person and world models on both sides rather than server-only SetModel.
edit(sdk+'shared/tf/tf_weapon_grapplinghook.h',s=>{
 if(s.includes('// Sourcebox native Portal Gun'))return s;
 return s.replace('\tCTFGrapplingHook();','\tCTFGrapplingHook();\n\t// Sourcebox native Portal Gun\n\tconst char *GetViewModel(int = 0) const OVERRIDE { return "models/sourcebox/portal2/v_portalgun.mdl"; }\n\tconst char *GetWorldModel() const OVERRIDE { return "models/sourcebox/portal2/w_portalgun.mdl"; }\n\tconst char *GetPrintName() const OVERRIDE { return "Portal Gun"; }\n\tbool HasAnyAmmo() OVERRIDE { return true; }\n\tint GetSlot() const OVERRIDE { return 5; }');
});
edit(sdk+'shared/tf/tf_weapon_grapplinghook.cpp',s=>{
 if(s.includes('// Sourcebox portal model precache'))return s;
 return s.replace('void CTFGrapplingHook::Precache()\n{','void CTFGrapplingHook::Precache()\n{\n\t// Sourcebox portal model precache\n\tPrecacheModel("models/sourcebox/portal2/v_portalgun.mdl");\n\tPrecacheModel("models/sourcebox/portal2/w_portalgun.mdl");');
});
// The portal carrier must not run the grappling hook's animation/projectile state machine.
edit(sdk+'shared/tf/tf_weapon_grapplinghook.cpp',s=>{
 function body(signature,replacement){
  const start=s.indexOf(signature);if(start<0)throw Error('Missing SDK function '+signature);
  const open=s.indexOf('{',start);let end=open+1,depth=1;
  for(;depth&&end<s.length;end++){if(s[end]==='{')depth++;else if(s[end]==='}')depth--;}
  if(depth)throw Error('Unbalanced SDK function');
  s=s.slice(0,open+1)+'\n'+replacement+'\n'+s.slice(end-1);
 }
 body('bool CTFGrapplingHook::SendWeaponAnim( int actBase )',`
  auto*p=GetTFPlayerOwner();if(!p)return false;
  auto*vm=p->GetViewModel(0);if(!vm)return false;
  vm->SetWeaponModel(GetViewModel(),this);
  const char*clip=actBase==ACT_VM_DRAW?"draw":actBase==ACT_VM_PRIMARYATTACK?"fire":"idle";
  int sequence=vm->LookupSequence(clip);if(sequence<0)sequence=vm->LookupSequence("idle");
  if(sequence<0)return false;
  vm->SendViewModelMatchingSequence(sequence);return true;`);
 body('void CTFGrapplingHook::ItemPostFrame( void )',`
  auto*p=GetTFPlayerOwner();if(!p)return;
  if((p->m_nButtons&(IN_ATTACK|IN_ATTACK2))&&gpGlobals->curtime>=m_flTimeWeaponIdle){
   SendWeaponAnim(ACT_VM_PRIMARYATTACK);m_flTimeWeaponIdle=gpGlobals->curtime+0.3f;
  }else if(gpGlobals->curtime>=m_flTimeWeaponIdle){
   SendWeaponAnim(ACT_VM_IDLE);m_flTimeWeaponIdle=gpGlobals->curtime+2.0f;
  }
  // Canonical placement is handled by Sourcebox; never launch a hook projectile.`);
 return s;
});
edit(sdk+'client/viewrender.cpp',s=>{
 if(s.includes('#include "sourcebox_portal_client.inc"'))return s;
 const signature='void CViewRender::RenderView( const CViewSetup &viewRender, int nClearFlags, int whatToDraw )';
 s=s.replace(signature,'#include "sourcebox_portal_client.inc"\n\n'+signature);
 s=s.replace(signature+'\n{',signature+'\n{\n\tSourceboxRenderViews(this, viewRender);');
 s=s.replace('VIEW_MAIN, whatToDraw & RENDERVIEW_DRAWVIEWMODEL );','VIEW_MAIN, whatToDraw & RENDERVIEW_DRAWVIEWMODEL, 0, sbPortalVisibility );');
 s=s.replace('// Now actually draw the viewmodel','SourceboxCompositePortals(viewRender, GetFrustum());\n\n\t\t// Now actually draw the viewmodel');
 return s;
});
