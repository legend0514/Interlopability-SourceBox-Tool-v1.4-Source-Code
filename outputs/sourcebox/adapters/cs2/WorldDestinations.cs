using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
public partial class SourceboxCS2 {
 private bool worldReadySent;private long nextWorldReadyProbe;private int selectedWorld=-1;
 private void TickWorldReadiness(){
  if(worldReadySent||Environment.TickCount64<nextWorldReadyProbe)return;
  nextWorldReadyProbe=Environment.TickCount64+1000;
  if(!Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_target").Any(e=>e.IsValid&&e.Entity?.Name=="sourcebox_worlds_ready"))return;
  Command(new{type="portal_world_ready",session=portalSession,worlds=WorldDestinations.Select(w=>w.Id).ToArray()});worldReadySent=true;
  Console.WriteLine("[Sourcebox] Native destination geometry loaded; announcing five worlds");
 }
 private bool SelectDestination(CCSPlayerController c,PlayerButtons pressed){
  if((pressed&PlayerButtons.Reload)==0||(c.Buttons&PlayerButtons.Use)==0)return false;
  if(!worldReadySent){c.PrintToChat("Destination maps are not loaded in this room.");return true;}
  // Normal, five destinations, random, then normal again.
  selectedWorld++;if(selectedWorld>WorldDestinations.Length)selectedWorld=-1;
  var label=selectedWorld<0?"Normal portals":selectedWorld==WorldDestinations.Length?"Random world":WorldDestinations[selectedWorld].Label;
  c.PrintToChat("Portal Gun destination: "+label+". E+R changes destination; fire at a room wall to open the pair.");return true;
 }
 private string? ShotDestination(){
  if(!worldReadySent||selectedWorld<0)return null;
  return WorldDestinations[selectedWorld==WorldDestinations.Length?Random.Shared.Next(WorldDestinations.Length):selectedWorld].Id;
 }
}
