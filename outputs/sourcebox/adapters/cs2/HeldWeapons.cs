using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Drawing;
public partial class SourceboxCS2 {
 private void UpdateHeldWeapon(string id,Peer p){
  string model="";
  var portal=p.Weapon=="weapon_sourcebox_portalgun"||p.Weapon=="weapon_portalgun"||(id=="tf2"&&p.Weapon=="tf_weapon_grapplinghook");
  if(portal)model="models/sourcebox/portals/world/world.vmdl";
  else if(HeldWeapons.TryGetValue(p.Weapon,out var source))PropModels.TryGetValue(source,out model!);
  if(!string.IsNullOrEmpty(model)&&!HasLocalModel(model))model="";
  if(p.WeaponEntity?.IsValid==true&&p.WeaponModel!=model){p.WeaponEntity.Remove();p.WeaponEntity=null;}p.WeaponModel=model??"";
  if(string.IsNullOrEmpty(model)||p.WeaponEntity?.IsValid==true)return;
  var e=Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");if(e==null)return;e.DispatchSpawn();e.SetModel(model);e.UseAnimGraph=false;e.Render=Color.White;
  e.AcceptInput("SetParent",p.Entity,p.Entity,"!activator");e.AcceptInput("SetParentAttachment",value:PlayerModels[p.ModelKey].WeaponAttachment);p.WeaponEntity=e;
 }
}
