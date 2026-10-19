// Sourcebox network representation. The core owns placement; this entity only replicates it.
class CSourceboxPortal : public CBaseAnimating
{
public:
 DECLARE_CLASS(CSourceboxPortal, CBaseAnimating);
 DECLARE_SERVERCLASS();
 DECLARE_DATADESC();
 CNetworkVar(int, m_iPair);
 CNetworkVar(int, m_iEndpoint);
 void Spawn() OVERRIDE {
  PrecacheModel("models/sourcebox/portal2/portal_ring.mdl");
  SetModel("models/sourcebox/portal2/portal_ring.mdl");
  SetSolid(SOLID_NONE); SetMoveType(MOVETYPE_NONE);
  BaseClass::Spawn();
 }
 int UpdateTransmitState() OVERRIDE { return SetTransmitState(FL_EDICT_ALWAYS); }
};
LINK_ENTITY_TO_CLASS(sourcebox_portal, CSourceboxPortal);
IMPLEMENT_SERVERCLASS_ST(CSourceboxPortal, DT_SourceboxPortal)
 SendPropInt(SENDINFO(m_iPair), 3, SPROP_UNSIGNED),
 SendPropInt(SENDINFO(m_iEndpoint), 1, SPROP_UNSIGNED),
END_SEND_TABLE()
BEGIN_DATADESC(CSourceboxPortal)
 DEFINE_KEYFIELD(m_iPair, FIELD_INTEGER, "pairid"),
 DEFINE_KEYFIELD(m_iEndpoint, FIELD_INTEGER, "endpoint"),
END_DATADESC()

#ifdef HL2_DLL
// The MP runtime does not run HL2's single-player map loadout output for a local host.
// Restore the native arsenal only in Sourcebox's test room, and after each respawn.
class CSourceboxLoadout : public CAutoGameSystemPerFrame
{
 float next;
public:
 CSourceboxLoadout():CAutoGameSystemPerFrame("SourceboxLoadout"),next(0){}
 void LevelInitPostEntity() OVERRIDE {next=0;}
 void FrameUpdatePostEntityThink() OVERRIDE {
  if(gpGlobals->curtime<next)return;next=gpGlobals->curtime+1;
  if(engine->IsDedicatedServer()||Q_strcmp(STRING(gpGlobals->mapname),"sourcebox_room_001"))return;
  CBasePlayer*p=UTIL_GetListenServerHost();if(!p||!p->IsAlive())return;
  if(!p->Weapon_OwnsThisType("weapon_sourcebox_portalgun")){
   p->EquipSuit(false);
   const char*weapons[]={"weapon_crowbar","weapon_pistol","weapon_357","weapon_smg1","weapon_ar2","weapon_shotgun","weapon_crossbow","weapon_frag","weapon_rpg","weapon_physcannon","weapon_bugbait","weapon_sourcebox_portalgun"};
   for(int i=0;i<ARRAYSIZE(weapons);i++)if(!p->Weapon_OwnsThisType(weapons[i]))p->GiveNamedItem(weapons[i]);
   CBaseCombatWeapon*w=p->Weapon_OwnsThisType("weapon_sourcebox_portalgun");if(w)p->Weapon_Switch(w);
   Msg("[Sourcebox] Native HL2 arsenal and Portal Gun equipped\n");
  }
  const char*ammo[]={"Pistol","357","SMG1","AR2","Buckshot","XBowBolt","Grenade","RPG_Round","SMG1_Grenade","AR2AltFire"};
  for(int i=0;i<ARRAYSIZE(ammo);i++)p->GiveAmmo(999,ammo[i],true);
 }
};
static CSourceboxLoadout sbLoadout;
#endif

#ifdef TF_DLL
#include "tf/tf_weaponbase.h"
#include "tf/tf_player.h"
#include "baseviewmodel.h"
// Record the real native inventory gates; only active in the explicit test room.
class CSourceboxInventoryProbe : public CAutoGameSystemPerFrame {
 float next;
public:
 CSourceboxInventoryProbe():CAutoGameSystemPerFrame("SourceboxInventoryProbe"),next(0){}
 void LevelInitPostEntity() OVERRIDE {next=0;}
 void FrameUpdatePostEntityThink() OVERRIDE {
  if(gpGlobals->curtime<next)return;next=gpGlobals->curtime+0.05f;
  if(engine->IsDedicatedServer()||Q_strcmp(STRING(gpGlobals->mapname),"sourcebox_room_001"))return;
  CBasePlayer*p=UTIL_GetListenServerHost();if(!p||!p->IsAlive())return;
  auto*tf=ToTFPlayer(p);
  if(!tf||tf->GetTeamNumber()<TF_TEAM_RED||tf->GetTeamNumber()>TF_TEAM_BLUE||tf->StateGet()!=TF_STATE_ACTIVE)return;
  const int playerClass=tf->GetPlayerClass()->GetClassIndex();
  if(playerClass<TF_CLASS_SCOUT||playerClass>TF_CLASS_ENGINEER)return;
  CBaseCombatWeapon*w=p->Weapon_OwnsThisType("tf_weapon_grapplinghook");
  if(!w){
   for(int i=0;i<p->WeaponCount();i++){auto*candidate=p->GetWeapon(i);if(candidate&&FClassnameIs(candidate,"tf_weapon_grapplinghook")){w=candidate;break;}}
   if(!w){
    auto*gun=dynamic_cast<CTFWeaponBase*>(CreateEntityByName("tf_weapon_grapplinghook"));if(!gun)return;
    // Class changes inspect static item data even for non-econ viewmodels.
    // Keep the native carrier definition while suppressing its hand attachment.
    auto*item=gun->GetAttributeContainer()->GetItem();
    item->SetItemDefIndex(1152);
    if(!item->GetStaticData()){Warning("[Sourcebox] Portal Gun carrier definition 1152 unavailable\n");UTIL_Remove(gun);return;}
    item->SetInitialized(false);
    gun->SetName(AllocPooledString("sourcebox_portalgun"));
    gun->ChangeTeam(p->GetTeamNumber());DispatchSpawn(gun);p->Weapon_Equip(gun);w=gun;
    Msg("[Sourcebox inventory] native Portal Gun added\n");
   }
   w->SetSubType(0);
   Msg("[Sourcebox inventory] ammo=%d deploy=%d select=%d switch=%d slot=%d\n",w->HasAnyAmmo(),w->CanDeploy(),w->CanBeSelected(),p->Weapon_CanSwitchTo(w),w->GetSlot());
  }
  w->m_iPrimaryAmmoType=-1;w->m_iSecondaryAmmoType=-1;
  w->m_flNextPrimaryAttack=gpGlobals->curtime+1;w->m_flNextSecondaryAttack=gpGlobals->curtime+1;
  const char*world="models/sourcebox/portal2/w_portalgun.mdl";
  if(Q_strcmp(STRING(w->GetModelName()),world))w->SetModel(world);
  if(p->GetActiveWeapon()==w){auto*vm=p->GetViewModel();if(vm){
   const char*view="models/sourcebox/portal2/v_portalgun.mdl";
   if(Q_strcmp(STRING(vm->GetModelName()),view)){
    vm->SetWeaponModel(view,w);
    const int idle=vm->LookupSequence("idle");
    if(idle>=0){vm->ResetSequence(idle);vm->SetCycle(0);}
   }
  }}
 }
};
static CSourceboxInventoryProbe sbInventoryProbe;
#endif
#include "sourcebox_prop_portals.inc"
// Crossplay damage has already been validated by the local relay. SetHealth's
// native input calls TakeDamage(self), which multiplayer rules may reject.
class CSourceboxPlayerControl : public CPointEntity {
public:
 DECLARE_CLASS(CSourceboxPlayerControl,CPointEntity);
 DECLARE_DATADESC();
 void ApplyHealth(inputdata_t& data){
  if(Q_strcmp(STRING(gpGlobals->mapname),"sourcebox_room_001"))return;
  CBasePlayer* p=UTIL_GetListenServerHost();if(!p||!p->IsAlive())return;
  int before=p->GetHealth();p->SetHealth(clamp(data.value.Int(),1,p->GetMaxHealth()));
  Msg("[Sourcebox] crossplay health %d -> %d\n",before,p->GetHealth());
 }
};
LINK_ENTITY_TO_CLASS(sourcebox_player_control,CSourceboxPlayerControl);
BEGIN_DATADESC(CSourceboxPlayerControl)
 DEFINE_INPUTFUNC(FIELD_INTEGER,"ApplyHealth",ApplyHealth),
END_DATADESC()
