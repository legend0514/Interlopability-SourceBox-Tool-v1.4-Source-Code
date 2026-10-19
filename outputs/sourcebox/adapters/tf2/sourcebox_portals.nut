// Local Sourcebox room only. Uses TF2's supported VScript entity and weapon APIs.
if (GetMapName() != "sourcebox_room_001") return;
local old = Entities.FindByName(null, "sourcebox_portal_inventory");
if (old != null) old.Kill();
local controller = SpawnEntityFromTable("logic_script", {targetname="sourcebox_portal_inventory"});
controller.ValidateScriptScope();
local scope=controller.GetScriptScope();
scope.nextInventoryLog <- 0.0;
PrecacheModel("models/sourcebox/portal2/v_portalgun.mdl");
PrecacheModel("models/sourcebox/portal2/w_portalgun.mdl");
scope.PortalInventoryThink <- function() {
 local p=null;
 while ((p=Entities.FindByClassname(p,"player")) != null) {
  if (p.GetTeam()<2 || NetProps.GetPropInt(p,"m_lifeState")!=0) continue;
  local gun=null, existing=null;
  for(local i=0;i<8;i++) {
   local w=NetProps.GetPropEntityArray(p,"m_hMyWeapons",i);
   if(w!=null && Time()>=nextInventoryLog)printl("[Sourcebox inventory script] index="+i+" class="+w.GetClassname()+" subtype="+w.GetSubType());
   if(w!=null && w.GetClassname()=="tf_weapon_grapplinghook") { existing=w;if(w.GetName()=="sourcebox_portalgun")gun=w; }
  }
  if(gun==null && existing==null) {
   gun=Entities.CreateByClassname("tf_weapon_grapplinghook");
   NetProps.SetPropInt(gun,"m_AttributeManager.m_Item.m_iItemDefinitionIndex",1152);
   NetProps.SetPropBool(gun,"m_AttributeManager.m_Item.m_bInitialized",true);
   gun.SetTeam(p.GetTeam());gun.KeyValueFromString("targetname","sourcebox_portalgun");
   Entities.DispatchSpawn(gun);
   // Econ assigns a nonzero subtype; the standard `use <class>` command asks
   // for subtype zero. This independent Portal Gun uses the ordinary slot path.
   gun.SetSubType(0);p.Weapon_Equip(gun);
   printl("[Sourcebox] Added Portal Gun native grappling-hook inventory carrier");
  }
  if(gun!=null) {
   // Portal placement has no ammunition. The carrier otherwise inherits a TF
   // grenade ammo type, causing normal inventory selection to reject it empty.
   NetProps.SetPropInt(gun,"m_iPrimaryAmmoType",-1);
   NetProps.SetPropInt(gun,"m_iSecondaryAmmoType",-1);
   NetProps.SetPropFloat(gun,"m_flNextPrimaryAttack",Time()+1);
   NetProps.SetPropFloat(gun,"m_flNextSecondaryAttack",Time()+1);
   gun.SetModel("models/sourcebox/portal2/w_portalgun.mdl");
   if(p.GetActiveWeapon()==gun) {
    local vm=NetProps.GetPropEntityArray(p,"m_hViewModel",0);
    if(vm!=null){vm.SetModel("models/sourcebox/portal2/v_portalgun.mdl");NetProps.SetPropInt(vm,"m_nSequence",8);}
   }
  }
 }
 if(Time()>=nextInventoryLog)nextInventoryLog=Time()+5;
 return 0.05;
};
AddThinkToEnt(controller,"PortalInventoryThink");
