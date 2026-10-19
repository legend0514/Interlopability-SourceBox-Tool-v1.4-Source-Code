#include "transport.h"
#include "engine/iserverplugin.h"
#include "game/server/iplayerinfo.h"
#include "toolframework/itoolentity.h"
#include "icliententitylist.h"
#include "cdll_int.h"
#include "server_class.h"
#include "weapon-factory.h"
#include "globalvars_base.h"
#include "engine/IEngineTrace.h"
#include "gametrace.h"
#include "iserverunknown.h"
#include "proxy_config.h"
#include "weapon_config.h"
#include "physics_config.h"
#include "combat_config.h"
#include "hl1mp_config.h"
#include "tier1/convar.h"
#include <cmath>
#include <cstdio>
#include <cstring>
#include <map>
#include "portal-preview-wire.h"
#include "portal_preview_config.h"
#include "world_destinations.h"
static SourceboxPreviewPair previewPairs[12];static int previewCount=0;
extern "C" __declspec(dllexport) int SourceboxReadPreviews(int abi,SourceboxPreviewPair*out,int capacity){
 if(abi!=SOURCEBOX_PREVIEW_ABI||!out||capacity<previewCount)return -1;
 memcpy(out,previewPairs,sizeof(SourceboxPreviewPair)*previewCount);return previewCount;
}
#define SOURCEBOX_PREVIEW_BRIDGE
// Read only metadata-described network fields; never hard-code game offsets.
static int FindField(SendTable*t,const char*name,SendPropType type,int depth=0){
 if(!t||depth>16)return -1;
 for(int i=0;i<t->GetNumProps();++i){auto*p=t->GetProp(i);
  if(!strcmp(p->GetName(),name)&&p->GetType()==type)return p->GetOffset();
  // Only inline tables documented in the SDK; do not follow pointer-backed tables.
  const char*table=p->GetName();
  if(p->GetType()==DPT_DataTable&&(!strcmp(table,"LocalWeaponData")||!strcmp(table,"LocalActiveWeaponData")||!strcmp(table,"baseclass")||!strcmp(table,"localdata")||!strcmp(table,"tflocaldata")||!strcmp(table,"tfnonlocaldata")||!strcmp(table,"m_PlayerClass")||!strcmp(table,"serveranimdata"))){int off=FindField(p->GetDataTable(),name,type,depth+1);if(off>=0)return p->GetOffset()+off;}
 }return -1;
}
class SourceboxAdapter final:public IServerPluginCallbacks{
 bool worldTextSupported=false;
 std::deque<Json> pendingMessages;
 Bridge bridge;IPlayerInfoManager*players=nullptr;IServerTools*entities=nullptr;IServerPluginHelpers*helpers=nullptr;
 edict_t*edictBase=nullptr;int edictCount=0;unsigned long long lastDiagnostic=0;
 void Diagnostic(unsigned long long now){
  if(strcmp(SOURCEBOX_GAME,"hl2")||now-lastDiagnostic<1000||!proxy||!edictBase)return;lastDiagnostic=now;
  FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH,"a");if(!f)return;
  fprintf(f,"%llu class=%d desired=%d ",now,remoteClass,animation);
  for(auto*e:{proxy,weapon}){if(!e)continue;
   char parent[128]{},origin[128]{},effect[32]{};
   entities->GetKeyValue(e,"parentname",parent,sizeof(parent));entities->GetKeyValue(e,"origin",origin,sizeof(origin));entities->GetKeyValue(e,"effects",effect,sizeof(effect));
   fprintf(f,"%s parent=%s origin=%s effects=%s ",e==proxy?"player":"weapon",parent,origin,effect);
   for(int i=1;i<edictCount;i++){auto*ed=edictBase+i;if(ed->IsFree())continue;auto*n=ed->GetNetworkable();if(!n||n->GetBaseEntity()!=e)continue;
    auto*t=n->GetServerClass()->m_pTable;int seq=FindField(t,"m_nSequence",DPT_Int),cycle=FindField(t,"m_flCycle",DPT_Float),rate=FindField(t,"m_flPlaybackRate",DPT_Float);
    int s=-1;float c=-1,r=-1;if(seq>=0&&seq<65536)memcpy(&s,(char*)e+seq,4);if(cycle>=0&&cycle<65536)memcpy(&c,(char*)e+cycle,4);if(rate>=0&&rate<65536)memcpy(&r,(char*)e+rate,4);
    fprintf(f,"sequence=%d cycle=%.3f rate=%.3f ",s,c,r);break;
   }
  }fprintf(f,"\n");fclose(f);
 }
 edict_t*player=nullptr;CBaseEntity*proxy=nullptr;CBaseEntity*label=nullptr;unsigned long long lastSample=0,remoteSeq=0;
 std::string remoteSession,shownWeapon;std::deque<Json>samples;int animation=-1,remoteClass=0,remoteTeam=0;CBaseEntity*weapon=nullptr;
 CBaseEntity*poseX=nullptr,*poseY=nullptr;unsigned long long poseReady=0,lastPoseUpdate=0;
 void RemovePoses(){if(poseX)entities->RemoveEntityImmediate(poseX);if(poseY)entities->RemoveEntityImmediate(poseY);poseX=nullptr;poseY=nullptr;poseReady=0;}
 void CreatePoses(unsigned long long now){
  RemovePoses();
  for(int axis=0;axis<2;axis++){
   auto*p=entities->CreateEntityByName("point_posecontroller");if(!p)continue;
   entities->SetKeyValue(p,"targetname",axis?"sourcebox_pose_y":"sourcebox_pose_x");entities->SetKeyValue(p,"PropName",ProxyName());
   entities->SetKeyValue(p,"PoseParameterName",axis?"move_y":"move_x");entities->SetKeyValue(p,"PoseValue","0.5");entities->SetKeyValue(p,"InterpolationTime","0.1");entities->SetKeyValue(p,"CycleFrequency","0");entities->DispatchSpawn(p);
   if(axis)poseY=p;else poseX=p;
  }
  // The controller's first think resolves its prop and resets its sequence.
  // Reapply the movement sequence after that documented initialization.
  poseReady=now+1200;lastPoseUpdate=0;
 }
 // Dispatch documented entity inputs through a one-shot engine timer.
 // This lets the game initialize animation playback and parenting itself.
 void Input(const char*target,const char*input,const char*value){
  auto*t=entities->CreateEntityByName("logic_timer");if(!t)return;
  char output[512];snprintf(output,sizeof(output),"%s,%s,%s,0,1",target,input,value);
  entities->SetKeyValue(t,"RefireTime","0.01");entities->SetKeyValue(t,"StartDisabled","0");
  entities->SetKeyValue(t,"OnTimer",output);entities->SetKeyValue(t,"OnTimer","!self,Kill,,0.01,1");entities->DispatchSpawn(t);
 }
 const ProxyConfig&Appearance()const{return !strcmp(SOURCEBOX_GAME,"hl2")&&remoteClass>=1&&remoteClass<=9?tfClasses[remoteClass-1]:gordon;}
 const char*ProxyName()const{return !strcmp(SOURCEBOX_GAME,"hl2")?"sourcebox_soldier":"sourcebox_hl2_stock_player";}
 // TF2 round cleanup removes map entities without unloading this plugin.
 // Resolve from the live engine list before using any cached entity pointer.
 // Also verify targetname: a deleted entity's address may have been reused.
 void ResolveOwned(){
  CBaseEntity*liveProxy=nullptr,*liveLabel=nullptr,*liveWeapon=nullptr,*liveX=nullptr,*liveY=nullptr;
  if(entities)for(auto*e=entities->FirstEntity();e;e=entities->NextEntity(e)){
   char name[128]{};if(!entities->GetKeyValue(e,"targetname",name,sizeof(name)))continue;
   if(!strcmp(name,ProxyName()))liveProxy=e;
   else if(!strcmp(name,"sourcebox_player_label"))liveLabel=e;
   else if(!strcmp(name,"sourcebox_visual_weapon"))liveWeapon=e;
   else if(!strcmp(name,"sourcebox_pose_x"))liveX=e;
   else if(!strcmp(name,"sourcebox_pose_y"))liveY=e;
  }
  if(liveProxy!=proxy)animation=-1;proxy=liveProxy;label=liveLabel;weapon=liveWeapon;poseX=liveX;poseY=liveY;
 }
 void ClearProxy(){ResolveOwned();RemovePoses();if(weapon&&entities)entities->RemoveEntity(weapon);if(proxy&&entities)entities->RemoveEntity(proxy);if(label&&entities)entities->RemoveEntity(label);proxy=nullptr;label=nullptr;weapon=nullptr;shownWeapon.clear();samples.clear();remoteSeq=0;remoteSession.clear();animation=-1;}

 CBaseEntity*Named(const char*name){for(auto*e=entities->FirstEntity();e;e=entities->NextEntity(e)){char n[128]{};if(entities->GetKeyValue(e,"targetname",n,sizeof(n))&&!strcmp(n,name))return e;}return nullptr;}
 Json ReadVector(CBaseEntity*e,const char*key){char text[128]{};float x=0,y=0,z=0;if(entities->GetKeyValue(e,key,text,sizeof(text)))sscanf(text,"%f %f %f",&x,&y,&z);return Json::array({x,y,z});}
 Json ReadProps(){Json out=Json::array();for(const auto&def:sharedProps)if(!strcmp(def.owner,SOURCEBOX_GAME)){auto*e=Named(def.id);Json p={{"id",def.id},{"alive",e!=nullptr}};if(e){p["position"]=ReadVector(e,"origin");p["rotation"]=ReadVector(e,"angles");p["velocity"]=ReadVector(e,"velocity");p["angularVelocity"]=ReadVector(e,"avelocity");}out.push_back(p);}return out;}
 void ClearProps(){for(const auto&def:sharedProps)if(strcmp(def.owner,SOURCEBOX_GAME))if(auto*e=Named(def.id))entities->RemoveEntityImmediate(e);}
 void RenderProps(const Json&a,const Json&b,double fraction){
  if(!b.contains("props"))return;
  for(const auto&p:b["props"]){const SharedProp*def=nullptr;std::string id=p.value("id","");for(const auto&d:sharedProps)if(id==d.id&&strcmp(d.owner,SOURCEBOX_GAME))def=&d;if(!def)continue;
   auto*e=Named(def->id);if(!p.value("alive",false)){if(e)entities->RemoveEntityImmediate(e);continue;}
   if(!e){e=entities->CreateEntityByName("prop_dynamic_override");if(!e)continue;entities->SetKeyValue(e,"targetname",def->id);entities->SetKeyValue(e,"model",def->model);entities->SetKeyValue(e,"solid","0");entities->SetKeyValue(e,"DisableBoneFollowers","1");entities->DispatchSpawn(e);}
   const Json*previous=&p;if(a.contains("props"))for(const auto&q:a["props"])if(q.value("id","")==id&&q.value("alive",false))previous=&q;
   for(const char*key:{"position","rotation"}){float values[3];for(int k=0;k<3;k++){double v=(*previous)[key][k],delta=p[key][k].get<double>()-v;if(!strcmp(key,"rotation"))delta=fmod(delta+540.0,360.0)-180.0;values[k]=float(v+delta*fraction);}char text[128];snprintf(text,sizeof(text),"%.5f %.5f %.5f",values[0],values[1],values[2]);entities->SetKeyValue(e,!strcmp(key,"position")?"origin":"angles",text);}
  }
 }
 std::deque<Json>physicsSamples;std::string physicsSession;unsigned long long physicsSeq=0,lastProps=0,lastReset=0;
 void QueuePhysics(const Json&j){if(!j["state"].contains("props"))return;if(j.value("session","")!=physicsSession){ClearProps();physicsSamples.clear();physicsSeq=0;physicsSession=j.value("session","");}const auto&s=j["state"];auto seq=s.value("seq",0ULL);if(seq<=physicsSeq)return;physicsSeq=seq;physicsSamples.push_back(s);while(physicsSamples.size()>8)physicsSamples.pop_front();}
 void TickPhysics(unsigned long long now){if(physicsSamples.empty())return;if(now>physicsSamples.back()["timestamp"].get<unsigned long long>()+1500){ClearProps();physicsSamples.clear();return;}auto target=now>SOURCEBOX_INTERPOLATION_MS?now-SOURCEBOX_INTERPOLATION_MS:0;while(physicsSamples.size()>2&&physicsSamples[1]["timestamp"].get<unsigned long long>()<=target)physicsSamples.pop_front();auto&a=physicsSamples.front();auto&b=physicsSamples.size()>1?physicsSamples[1]:a;double ta=a["timestamp"],tb=b["timestamp"],f=tb>ta?(double(target)-ta)/(tb-ta):1;RenderProps(a,b,f<0?0:f>1?1:f);}
 void ResetProps(){for(const auto&d:sharedProps)if(!strcmp(d.owner,SOURCEBOX_GAME)){if(auto*old=Named(d.id))entities->RemoveEntityImmediate(old);auto*e=entities->CreateEntityByName("prop_physics");if(!e)continue;entities->SetKeyValue(e,"targetname",d.id);entities->SetKeyValue(e,"model",d.model);entities->SetKeyValue(e,"origin",d.spawn);entities->SetKeyValue(e,"spawnflags","256");entities->DispatchSpawn(e);}}
 #include "gmod-scene.inc"
 #include "gmod.inc"
 #include "cube.inc"
 #include "extra-players.inc"
 #include "combat.inc"
 unsigned long long missingPropSince[4]{};
 void RespawnProps(unsigned long long now){int index=0;for(const auto&d:sharedProps){auto&missing=missingPropSince[index++];if(strcmp(d.owner,SOURCEBOX_GAME))continue;if(Named(d.id)){missing=0;continue;}if(!missing){missing=now;continue;}if(now-missing<SOURCEBOX_RESPAWN_MS)continue;auto*e=entities->CreateEntityByName("prop_physics");if(e){entities->SetKeyValue(e,"targetname",d.id);entities->SetKeyValue(e,"model",d.model);entities->SetKeyValue(e,"origin",d.spawn);entities->SetKeyValue(e,"spawnflags","256");entities->DispatchSpawn(e);}missing=now;}}
 void Track(edict_t*e){if(players&&e){auto*p=players->GetPlayerInfo(e);if(p&&p->IsPlayer()&&!p->IsFakeClient())player=e;}}
 bool portalScriptStarted=false,hl1PortalGranted=false;unsigned long long hl1PortalGrantAfter=0;
 IVEngineClient* hl1ProbeClient=nullptr;unsigned long long hl1ProbeStart=0;int hl1ProbeStage=0;bool hl1PropProbe=false,hl1DamageProbe=false;Json hl1PropEntry;Vector hl1PropSaved;QAngle hl1PropLook;bool hl1ProbeEnabled=false,hl1MonitorProbe=false,hl1FallProbe=false,hl1RespawnProbe=false;
 bool hl1LethalProbe=false;std::string hl1DamageTarget="gmod";int hl1DamageCount=0,hl1LastHealth=0;unsigned long long hl1DamageAt=0;
 void HL1PortalProbe(unsigned long long now){
  if(hl1ProbeClient&&!hl1ProbeEnabled){
   static unsigned long long nextCheck=0;if(now<nextCheck)return;nextCheck=now+1000;
   if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH "-hl1-fixture","r")){
    const int mode=fgetc(f);if(mode=='L'){char target[32]{};if(fscanf(f,"%31s",target)==1)hl1DamageTarget=target;hl1DamageCount=0;hl1LastHealth=0;hl1DamageAt=0;}hl1LethalProbe=mode=='L';fclose(f);remove(SOURCEBOX_DIAGNOSTIC_PATH "-hl1-fixture");
    hl1DamageProbe=mode=='D';hl1PropProbe=mode=='P';hl1MonitorProbe=mode=='M';hl1FallProbe=mode=='F';hl1RespawnProbe=mode=='R';hl1ProbeEnabled=true;hl1ProbeStart=0;hl1ProbeStage=0;
   }
  }
  if(!hl1ProbeEnabled||!hl1ProbeClient||hl1ProbeStage>=11)return;
  if(!hl1ProbeClient->IsInGame()||hl1ProbeClient->IsDrawingLoadingImage())return;
  if(!hl1ProbeStart)hl1ProbeStart=now;
  if(hl1LethalProbe){
   std::string target;int health=0;
   if(hl1DamageTarget=="gmod"&&gmodPose.is_object()){target="sourcebox_gmod_player";health=gmodPose.value("health",0);}
   else if(extraPeers.count(hl1DamageTarget)){target=ExtraName(hl1DamageTarget);health=extraPeers[hl1DamageTarget].pose.value("health",0);}
   if(target.empty()||health<=0||!Named(target.c_str()))return;
   const bool respawn=hl1DamageCount>0&&hl1LastHealth<=20&&health>hl1LastHealth;
   if(respawn||hl1DamageCount>=30){if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH,"a")){fprintf(f,"native lethal probe target=%s respawn=%d hits=%d health=%d\n",hl1DamageTarget.c_str(),respawn,hl1DamageCount,health);fclose(f);}hl1ProbeEnabled=false;hl1ProbeStart=0;return;}
   hl1LastHealth=health;
   if(now-hl1DamageAt>=150){RunCubeScript("self.TakeDamage(10,2,Entities.FindByClassname(null,\"player\"));",target.c_str());hl1DamageCount++;hl1DamageAt=now;}return;
  }
  if(hl1DamageProbe){
   for(const char*target:{"sourcebox_peer_hl2","sourcebox_peer_tf2","sourcebox_peer_cs2","sourcebox_peer_portal2","sourcebox_gmod_player"})if(Named(target))RunCubeScript("self.TakeDamage(10,2,Entities.FindByClassname(null,\"player\"));",target);
   hl1ProbeEnabled=false;hl1ProbeStart=0;return;
  }
  if(hl1PropProbe){
   if(hl1ProbeStage==0){
    if(!cubeSnapshot.is_object()||!cubeSnapshot.value("alive",false))return;
    for(const auto&pair:cubePortalPairs)if(pair.value("linked",false))for(const auto&end:pair["endpoints"])if(double(end["normal"][2])>0.99)hl1PropEntry=end;
    if(!hl1PropEntry.is_object())return;
    auto*info=players->GetPlayerInfo(player);hl1PropSaved=info->GetAbsOrigin();Vector eye;ReadPortalEye(info,eye,hl1PropLook);
    const auto&p=cubeSnapshot["position"];SnapPortalPlayer(Vector(float(p[0])+70,float(p[1]),72),hl1PropLook);ApplyPortalVelocity(Vector(0,0,0));hl1ProbeStart=now;hl1ProbeStage=1;return;
   }
   if(hl1ProbeStage==1&&now-hl1ProbeStart>1000){bridge.Command({{"type","cube_take"}});hl1ProbeStage=2;}
   if(hl1ProbeStage==2&&cubeAuthority){
    const auto&p=hl1PropEntry["position"];char code[512];snprintf(code,sizeof(code),"SBPrevious=null;self.SetOrigin(Vector(%.4f,%.4f,%.4f));self.SetAngles(0,0,0);self.SetPhysVelocity(Vector(0,0,0));self.SetPhysAngularVelocity(Vector(0,0,0));",double(p[0]),double(p[1]),double(p[2])+160);RunCubeScript(code);hl1ProbeStart=now;hl1ProbeStage=3;
   }
   if((hl1ProbeStage==3&&now-hl1ProbeStart>4000)||now-hl1ProbeStart>8000){
    SnapPortalPlayer(hl1PropSaved+Vector(0,0,64),hl1PropLook);ApplyPortalVelocity(Vector(0,0,0));
    if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH,"a")){fprintf(f,"HL1 native prop probe owner=%d state=%s\n",cubeAuthority,CubeRead(now).dump().c_str());fclose(f);}hl1ProbeEnabled=false;hl1ProbeStart=0;hl1ProbeStage=0;
   }return;
  }
  if(hl1RespawnProbe){
   const unsigned long long times[]={2000,4000,6000,7000,8000,9000,10000,12000,14000,15000,16000};
   const char*commands[]={"use weapon_egon; gameui_hide","jpeg sourcebox_hl1_respawn_before","kill","+attack","-attack","+attack","-attack","jpeg sourcebox_hl1_respawn_after","use weapon_crowbar","jpeg sourcebox_hl1_crowbar_preserved","use weapon_egon"};
   if(now-hl1ProbeStart>=times[hl1ProbeStage]){hl1ProbeClient->ClientCmd_Unrestricted(commands[hl1ProbeStage]);hl1ProbeStage++;}return;
  }
  const unsigned long long times[]={2000,4000,6000,7000,7300,8000,8300,10000,11000,13000,14000};
  const char*commands[]={"use weapon_egon","setpos 0 -320 2; setang 0 0 0; gameui_hide","jpeg sourcebox_hl1_live_gun","+attack","-attack; setang 0 180 0","+attack2","-attack2","jpeg sourcebox_hl1_live_portals","setpos 350 -320 2; setang 0 0 0; +forward","-forward","jpeg sourcebox_hl1_live_traversal"};
  const char*monitor[]={"setpos 0 0 8; setang 0 0 0; gameui_hide","ent_fire sb_camera_probe SetOn","jpeg sourcebox_hl1_monitor_live","ent_fire sb_camera_probe SetOn","setang 0 0 0","jpeg sourcebox_hl1_monitor_live_after","-attack; -attack2","jpeg sourcebox_hl1_monitor_final","-forward","-forward","jpeg sourcebox_hl1_monitor_final"};
  const char*fall[]={"use weapon_egon; noclip; setpos 128 256 128; setang 90 0 0; gameui_hide","+attack","-attack; setang 0 0 0","+attack2","-attack2; setpos 128 256 150; setang 0 0 0; noclip","jpeg sourcebox_hl1_fall_entry","-attack; -attack2","jpeg sourcebox_hl1_fall_exit","-forward","-forward","jpeg sourcebox_hl1_fall_final"};
  if(now-hl1ProbeStart>=times[hl1ProbeStage]){hl1ProbeClient->ClientCmd_Unrestricted(hl1FallProbe?fall[hl1ProbeStage]:(hl1MonitorProbe?monitor[hl1ProbeStage]:commands[hl1ProbeStage]));hl1ProbeStage++;}
 }
 void PortalCarrierTick(){
  if(strcmp(SOURCEBOX_GAME,"hl1mp")||!portalEnabled||!edictBase)return;
  auto*factories=entities->GetEntityFactoryDictionary();
  if(!factories||!factories->FindFactory("weapon_egon"))return;
  bool ownsPortalGun=false;
  for(int i=1;i<edictCount;i++){auto*ed=edictBase+i;if(ed->IsFree())continue;auto*n=ed->GetNetworkable();if(!n)continue;auto*e=n->GetBaseEntity();char name[96];if(!entities->GetKeyValue(e,"classname",name,sizeof(name))||strcmp(name,"weapon_egon"))continue;
   auto*table=n->GetServerClass()->m_pTable;const int ownerOffset=FindField(table,"m_hOwnerEntity",DPT_Int);if(ownerOffset<0||ownerOffset>65536)continue;
   CBaseHandle owner;memcpy(&owner,(char*)e+ownerOffset,sizeof(owner));if(!player||owner.GetEntryIndex()!=player-edictBase)continue;
   ownsPortalGun=true;float next=players->GetGlobalVars()->curtime+2;
   for(const char*key:{"m_flNextPrimaryAttack","m_flNextSecondaryAttack"}){int off=FindField(table,key,DPT_Float);if(off>=0&&off<65536){memcpy((char*)e+off,&next,4);ed->m_fStateFlags|=FL_EDICT_CHANGED|FL_FULL_EDICT_CHANGED;}}
  }
  const auto now=GetTickCount64();
  if(!ownsPortalGun&&helpers&&player&&now>=hl1PortalGrantAfter){helpers->ClientCommand(player,"give weapon_egon");helpers->ClientCommand(player,"use weapon_egon");hl1PortalGrantAfter=now+750;}
  hl1PortalGranted=ownsPortalGun;
 }

 void PortalInventory(){
#ifdef SOURCEBOX_SDK
  return; // Native SDK inventory survives respawn and owns the weapon lifecycle.
#else
  if(strcmp(SOURCEBOX_GAME,"tf2")||!portalEnabled||portalScriptStarted)return;auto*e=entities->CreateEntityByName("logic_script");if(!e)return;entities->SetKeyValue(e,"targetname","sourcebox_portal_boot");entities->SetKeyValue(e,"vscripts","sourcebox_portals.nut");entities->DispatchSpawn(e);portalScriptStarted=true;
#endif
 }
 void PortalDelete(const std::string&name){if(auto*e=Named(name.c_str()))entities->RemoveEntityImmediate(e);}
 IClientEntity* PortalClientPlayer(){
  if(strcmp(SOURCEBOX_GAME,"hl1mp")||!player||!edictBase)return nullptr;
  auto module=GetModuleHandleA("client.dll");
  if(!module)return nullptr;
  auto create=(CreateInterfaceFn)GetProcAddress(module,"CreateInterface");
  auto*list=create?(IClientEntityList*)create(VCLIENTENTITYLIST_INTERFACE_VERSION,nullptr):nullptr;
  return list?list->GetClientEntity(int(player-edictBase)):nullptr;
 }
 bool SnapPortalPlayer(const Vector&eye,const QAngle&angle){
  return entities->SnapPlayerToPosition(eye,angle,PortalClientPlayer());
 }
 bool ApplyPortalVelocity(const Vector&velocity){
  auto*actor=player->GetNetworkable()->GetBaseEntity();char text[128];
  snprintf(text,sizeof(text),"%.4f %.4f %.4f",velocity.x,velocity.y,velocity.z);
  if(!entities->SetKeyValue(actor,"velocity",text))return false;
  // KeyValue writes local velocity but does not invalidate the cached absolute
  // velocity. Ask the game's native script method to perform that update.
  if(!strcmp(SOURCEBOX_GAME,"hl1mp")||!strcmp(SOURCEBOX_GAME,"hl2")||!strcmp(SOURCEBOX_GAME,"tf2")){
   entities->SetKeyValue(actor,"targetname","sourcebox_portal_actor");
   auto*t=entities->CreateEntityByName("logic_timer");if(!t)return false;
   char output[512];snprintf(output,sizeof(output),"sourcebox_portal_actor%cRunScriptCode%cself.SetAbsVelocity(Vector(%.4f,%.4f,%.4f));%c0%c1",27,27,velocity.x,velocity.y,velocity.z,27,27);
   entities->SetKeyValue(t,"RefireTime","0.01");entities->SetKeyValue(t,"StartDisabled","0");entities->SetKeyValue(t,"OnTimer",output);entities->SetKeyValue(t,"OnTimer","!self,Kill,,0.01,1");entities->DispatchSpawn(t);
  }
  return true;
 }
 bool ReadPortalEye(IPlayerInfo*p,Vector&eye,QAngle&angle){
  if(entities->GetPlayerPosition(eye,angle,PortalClientPlayer()))return true;
  if(strcmp(SOURCEBOX_GAME,"hl1mp")||!player||!player->GetNetworkable())return false;
  auto*n=player->GetNetworkable();auto*base=(char*)n->GetBaseEntity();auto*table=n->GetServerClass()->m_pTable;
  eye=p->GetAbsOrigin();angle=p->GetLastUserCommand().viewangles;
  const char*keys[]={"m_vecViewOffset[0]","m_vecViewOffset[1]","m_vecViewOffset[2]"};
  for(int i=0;i<3;i++){int offset=FindField(table,keys[i],DPT_Float);if(offset<0||offset>65536)return false;float value;memcpy(&value,base+offset,4);eye[i]+=value;}
  return true;
 }
 #include "portals.inc"
public:
 bool Load(CreateInterfaceFn engine,CreateInterfaceFn factory)override{
  if(!strcmp(SOURCEBOX_GAME,"hl1mp")){
   hl1ProbeClient=(IVEngineClient*)engine(VENGINE_CLIENT_INTERFACE_VERSION,nullptr);
   if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH "-hl1-fixture","r")){const int mode=fgetc(f);hl1DamageProbe=mode=='D';hl1PropProbe=mode=='P';hl1MonitorProbe=mode=='M';hl1FallProbe=mode=='F';hl1RespawnProbe=mode=='R';fclose(f);hl1ProbeEnabled=true;hl1ProbeClient=(IVEngineClient*)engine(VENGINE_CLIENT_INTERFACE_VERSION,nullptr);}
  }
  helpers=(IServerPluginHelpers*)engine(INTERFACEVERSION_ISERVERPLUGINHELPERS,nullptr);
  players=(IPlayerInfoManager*)factory(INTERFACEVERSION_PLAYERINFOMANAGER,nullptr);
  entities=(IServerTools*)factory(VSERVERTOOLS_INTERFACE_VERSION,nullptr);
  if(!entities)entities=(IServerTools*)factory(VSERVERTOOLS_INTERFACE_VERSION_2,nullptr);
   if(!players||!entities)return false;
   if(auto*factory=entities->GetEntityFactoryDictionary()){FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH,"a");if(f){for(const char*name:{"point_camera","func_monitor","info_camera_link","weapon_sourcebox_portalgun","weapon_cubemap","weapon_physgun","weapon_portalgun","weapon_alyxgun","weapon_bugbait","weapon_stunstick","weapon_annabelle","weapon_slam","weapon_pistol","weapon_ar2","tf_weapon_spellbook","tf_weapon_grapplinghook"})fprintf(f,"inventory_factory game=%s class=%s available=%d\n",SOURCEBOX_GAME,name,factory->FindFactory(name)!=nullptr);fclose(f);}}
   if(auto*dictionary=entities->GetEntityFactoryDictionary()){
    worldTextSupported=dictionary->FindFactory("point_worldtext")!=nullptr;
    // A second factory name does not create a second client weapon identity.
    // Never alias the HL1 crowbar: it steals its inventory slot and prediction.
   }
   portalTrace=(IEngineTrace*)engine(INTERFACEVERSION_ENGINETRACE_SERVER,nullptr);bridge.PortalCapability(portalTrace!=nullptr);
   bridge.Start();return true;
 }
 void Unload()override{ClearPortalMarkers();bridge.Stop();ClearExtras();ClearScene();ClearGmod();DeleteCube();ClearProps();ClearProxy();player=nullptr;}
 void Pause()override{}void UnPause()override{}
 const char*GetPluginDescription()override{return "SOURCEBOX C candidate: animated installed player models; stock HL2 model is unfinished";}
 void LevelInit(const char*)override{portalWorldsReady=false;portalWorldSelection=0;portalWorldCheck=0;portalScriptStarted=false;hl1PortalGranted=false;player=nullptr;proxy=nullptr;label=nullptr;samples.clear();remoteSeq=0;remoteSession.clear();animation=-1;physicsSamples.clear();physicsSession.clear();physicsSeq=0;bridge.PublishProps(nullptr);bridge.Publish(nullptr);}
 void ServerActivate(edict_t*list,int count,int)override{edictBase=list;edictCount=count;bridge.Command({{"type","cube_sync"}});bridge.Command({{"type","portal_sync"}});}
 void GameFrame(bool simulating)override{
  auto now=GetTickCount64();
  // The initial snapshot may arrive before ServerActivate; a sync command sent
  // before welcome is deliberately discarded by the transport. Retry after both
  // prerequisites exist so reconnects do not leave native portals absent.
  static unsigned long long lastPortalSync=0;
  if(edictBase&&portalEnabled&&portalRevision<0&&now-lastPortalSync>1000){lastPortalSync=now;bridge.Command({{"type","portal_sync"}});}
  if(player&&players){auto*p=players->GetPlayerInfo(player);if(p&&(hl1ProbeStart||(!p->IsDead()&&!p->IsObserver())))HL1PortalProbe(now);}
  // Ownership traffic must run on the engine thread even while menus pause simulation.
  for(auto&j:bridge.Drain()){
   const auto type=j.value("type",std::string());
   if(type=="welcome"||type=="portal_lost")PortalMessage(j);
   else if(type=="portal_snapshot"){if(edictBase)PortalMessage(j);}
   else if(edictBase&&type.find("cube_")==0)CubeMessage(j);
   else {pendingMessages.push_back(j);if(pendingMessages.size()>256)pendingMessages.pop_front();}
  }
  if(edictBase)CubeTick(now);
  if(!simulating)return;ResolveOwned();
  if(!player)bridge.Publish(nullptr);
  if(now-lastProps>=SOURCEBOX_UPDATE_MS){bridge.PublishProps({{"timestamp",now},{"props",ReadProps()}});lastProps=now;}
  if(player&&now-lastSample>=SOURCEBOX_UPDATE_MS){
   auto*p=players->GetPlayerInfo(player);auto*n=player->GetNetworkable();
   if(p&&n&&p->IsConnected()&&!p->IsObserver()&&!p->IsDead()){
    PortalInventory();PortalCarrierTick();PortalInput(p);
    Vector pos=p->GetAbsOrigin(),eye;QAngle angles;char v[128]{};float vx=0,vy=0,vz=0;
    auto*table=n->GetServerClass()->m_pTable;auto*base=reinterpret_cast<char*>(n->GetBaseEntity());
    int off=FindField(table,"m_fFlags",DPT_Int),classId=0;bool viewOK=false;
    if(!strcmp(SOURCEBOX_GAME,"tf2")){
     int pitch=FindField(table,"m_angEyeAngles[0]",DPT_Float),yaw=FindField(table,"m_angEyeAngles[1]",DPT_Float),cls=FindField(table,"m_iClass",DPT_Int);
     if(pitch>=0&&pitch<65536&&yaw>=0&&yaw<65536&&cls>=0&&cls<65536){memcpy(&angles.x,base+pitch,4);memcpy(&angles.y,base+yaw,4);angles.z=0;memcpy(&classId,base+cls,4);viewOK=classId>=1&&classId<=9;}
    }else if(!strcmp(SOURCEBOX_GAME,"hl1mp")){angles=p->GetAbsAngles();int pitch=FindField(table,"m_angEyeAngles[0]",DPT_Float),yaw=FindField(table,"m_angEyeAngles[1]",DPT_Float);if(pitch>=0&&pitch<65536)memcpy(&angles.x,base+pitch,4);if(yaw>=0&&yaw<65536)memcpy(&angles.y,base+yaw,4);viewOK=true;}else viewOK=entities->GetPlayerPosition(eye,angles);
    if(off>=0&&off<65536&&viewOK&&entities->GetKeyValue(n->GetBaseEntity(),"velocity",v,sizeof(v))&&sscanf(v,"%f %f %f",&vx,&vy,&vz)==3){
     int flags;memcpy(&flags,(char*)n->GetBaseEntity()+off,sizeof(flags));
     Json state={{"health",p->GetHealth()},{"timestamp",now},{"entity",std::string("player:")+SOURCEBOX_GAME},{"position",{pos.x,pos.y,pos.z}},{"rotation",{angles.x,angles.y,angles.z}},{"velocity",{vx,vy,vz}},{"grounded",(flags&FL_ONGROUND)!=0}};
     if(!strcmp(SOURCEBOX_GAME,"hl1mp")){const char*model=p->GetModelName();state["model"]=model?model:"";}
     const char*localWeapon=p->GetWeaponName();state["weapon"]=localWeapon?localWeapon:"";
     if(!strcmp(SOURCEBOX_GAME,"hl2")&&localWeapon&&!strcmp(localWeapon,"weapon_cubemap"))state["weapon"]="weapon_sourcebox_portalgun";
     if(!strcmp(SOURCEBOX_GAME,"hl1mp")&&localWeapon&&!strcmp(localWeapon,"weapon_egon"))state["weapon"]="weapon_sourcebox_portalgun";
     if(classId){state["classId"]=classId;state["team"]=p->GetTeamIndex();const char*w=p->GetWeaponName();state["weapon"]=w?w:"";}bridge.Publish(state);
    }else bridge.Publish(nullptr);
   }else {bridge.Publish(nullptr);hl1PortalGranted=false;}lastSample=now;
  }
  std::deque<Json> messages;messages.swap(pendingMessages);
  for(auto&j:messages){
   if(j.value("type",std::string()).find("portal_")==0||j.value("type","")=="welcome"){PortalMessage(j);continue;}
   if(j.value("game","")=="portal2"||j.value("game","")=="cs2"||j.value("game","")=="hl1mp"||(!strcmp(SOURCEBOX_GAME,"hl1mp")&&(j.value("game","")=="hl2"||j.value("game","")=="tf2"))){ExtraMessage(j,now);continue;}
   if(j.value("game","")=="gmod"){if(j.value("type","")=="scene")SceneMessage(j,now);else{if(j.value("type","")=="remove"&&j.value("scope","game")=="game")ClearScene();GmodMessage(j,now);}continue;}
   if(j.value("type","")=="remove"&&!j.contains("game")){ClearGmod();ClearScene();ClearExtras();}
   if(j.value("type","")=="player_damage"){pendingDamage+=j.value("amount",0);continue;}
   if(j.value("type",std::string()).find("cube_")==0){CubeMessage(j);continue;}
   if(j.value("type","")=="remove"){ClearProxy();if(j.value("scope","game")!="player"){ClearProps();physicsSamples.clear();physicsSession.clear();physicsSeq=0;}continue;}
   if(j.value("game","")!=(!strcmp(SOURCEBOX_GAME,"hl2")?"tf2":"hl2"))continue;
   QueuePhysics(j);if(j.value("type","")=="physics")continue;
   if(j.value("session","")!=remoteSession){ClearProxy();remoteSession=j.value("session","");}
   auto state=j["state"];auto seq=state.value("seq",0ULL);if(seq<=remoteSeq)continue;
   remoteSeq=seq;samples.push_back(state);while(samples.size()>8)samples.pop_front();
  }
  PollProxyDamage();
  ApplyPlayerDamage(now);
  RespawnProps(now);
  CubeTick(now);
  TickPhysics(now);
  TickExtras(now);
  TickGmod(now);
  SceneTick(now);
  if(samples.empty())return;
  if(now>samples.back()["timestamp"].get<unsigned long long>()+1500){ClearProxy();return;}
  if(!strcmp(SOURCEBOX_GAME,"hl2")){
   int nextClass=samples.back().value("classId",0),nextTeam=samples.back().value("team",0);
   if(nextClass<1||nextClass>9)return;
   if(nextClass!=remoteClass){
    RemovePoses();
    if(weapon){entities->RemoveEntityImmediate(weapon);weapon=nullptr;shownWeapon.clear();}
    if(proxy){entities->RemoveEntityImmediate(proxy);proxy=nullptr;}
    remoteClass=nextClass;animation=-1;
   }
   remoteTeam=nextTeam;
  }
  if(!proxy){
   proxy=entities->CreateEntityByName("prop_dynamic_override");if(!proxy)return;
   const auto&c=Appearance();animation=-1;
   entities->SetKeyValue(proxy,"targetname",ProxyName());
   entities->SetKeyValue(proxy,"model",c.model);
   entities->SetKeyValue(proxy,"DefaultAnim",c.idle);
   PrepareHitDetector();entities->SetKeyValue(proxy,"OnTakeDamage","sourcebox_hit_relay,SetValue,1,0,-1");
   entities->SetKeyValue(proxy,"solid",SOURCEBOX_PLAYER_SOLID==2?"2":"0");entities->SetKeyValue(proxy,"DisableBoneFollowers","1");entities->DispatchSpawn(proxy);Input(ProxyName(),"EnableCollision","");
   if(worldTextSupported&&*c.label&&!label){label=entities->CreateEntityByName("point_worldtext");if(label){entities->SetKeyValue(label,"targetname","sourcebox_player_label");entities->SetKeyValue(label,"message",c.label);entities->SetKeyValue(label,"orientation","1");entities->SetKeyValue(label,"textsize","5");entities->SetKeyValue(label,"color","255 160 40 255");entities->DispatchSpawn(label);}}
   ResolveOwned();if(!proxy)return;
   if(!strcmp(SOURCEBOX_GAME,"hl2"))CreatePoses(now);
  }
  if(!strcmp(SOURCEBOX_GAME,"hl2"))entities->SetKeyValue(proxy,"skin",remoteTeam==3?"1":"0");
  {
   const auto desired=samples.back().value("weapon",std::string());const char*model=nullptr;
   if(!strcmp(SOURCEBOX_GAME,"hl2")){for(const auto&w:weaponModels)if(desired==w.name){model=w.model;break;}}else{for(const auto&w:hl2WeaponModels)if(desired==w.name){model=w.model;break;}}
   if(shownWeapon!=desired&&weapon){entities->RemoveEntityImmediate(weapon);weapon=nullptr;}
   if(model&&!weapon){
    weapon=entities->CreateEntityByName("prop_dynamic_override");
    if(weapon){entities->SetKeyValue(weapon,"targetname","sourcebox_visual_weapon");entities->SetKeyValue(weapon,"model",model);entities->SetKeyValue(weapon,"solid","0");entities->SetKeyValue(weapon,"effects",!strcmp(SOURCEBOX_GAME,"hl2")?"1":"0");entities->SetKeyValue(weapon,"DisableBoneFollowers","1");entities->DispatchSpawn(weapon);entities->SetMoveType(weapon,MOVETYPE_NONE);Input("sourcebox_visual_weapon","SetParent",ProxyName());}
   }
   shownWeapon=desired;
  }
  const auto target=now>SOURCEBOX_INTERPOLATION_MS?now-SOURCEBOX_INTERPOLATION_MS:0;
  while(samples.size()>2&&samples[1]["timestamp"].get<unsigned long long>()<=target)samples.pop_front();
  auto&a=samples.front();auto&b=samples.size()>1?samples[1]:samples[0];
  double ta=a["timestamp"],tb=b["timestamp"],f=tb>ta?(double(target)-ta)/(tb-ta):1;if(f<0)f=0;if(f>1)f=1;
  float xyz[3],ang[3];for(int k=0;k<3;k++){
   double pa=a["position"][k],pb=b["position"][k];xyz[k]=float(pa+(pb-pa)*f);
   double aa=a["rotation"][k],ab=b["rotation"][k];double d=fmod(ab-aa+540.0,360.0)-180.0;ang[k]=float(aa+d*f);
  }
  char text[160];snprintf(text,sizeof(text),"%.5f %.5f %.5f",xyz[0],xyz[1],xyz[2]);entities->SetKeyValue(proxy,"origin",text);
  // Eye pitch belongs to the head, never to the whole character body.
  snprintf(text,sizeof(text),"0 %.5f 0",ang[1]);entities->SetKeyValue(proxy,"angles",text);
  const auto&pose=f<0.5?a:b;const auto&c=Appearance();
  double vx=pose["velocity"][0],vy=pose["velocity"][1],speed=sqrt(vx*vx+vy*vy);
  if(!strcmp(SOURCEBOX_GAME,"hl2")){
   if(poseReady&&now>=poseReady){animation=-1;poseReady=0;}
   if(!poseReady&&now-lastPoseUpdate>=100&&poseX&&poseY){
    double yaw=ang[1]*3.141592653589793/180.0;
    double forward=speed>8?(cos(yaw)*vx+sin(yaw)*vy)/speed:0;
    double side=speed>8?(-sin(yaw)*vx+cos(yaw)*vy)/speed:0;
    snprintf(text,sizeof(text),"%.5f",0.5+0.5*forward);Input("sourcebox_pose_x","SetPoseValue",text);
    snprintf(text,sizeof(text),"%.5f",0.5+0.5*side);Input("sourcebox_pose_y","SetPoseValue",text);lastPoseUpdate=now;
   }
  }
  int next;const char*name;
  if(!pose["grounded"].get<bool>()){next=c.airIndex;name=c.air;}
  else if(speed>150){next=c.runIndex;name=c.run;}
  else if(speed>8){next=c.walkIndex;name=c.walk;}
  else{next=c.idleIndex;name=c.idle;}
  if(next!=animation){
   entities->SetKeyValue(proxy,"DefaultAnim",name);
   // SDK tool interface operates only on our prop_dynamic (a CBaseAnimating).
   // Reset only on state changes so frames can advance between network updates.
   Input(ProxyName(),"SetAnimation",name);animation=next;
  }
  if(label){snprintf(text,sizeof(text),"%.5f %.5f %.5f",xyz[0],xyz[1],xyz[2]+90);entities->SetKeyValue(label,"origin",text);}
  // KeyValue origin/angles are world-space. Align roots after SetParent
  // preserves the original world transform, giving bone merge zero local offset.
  if(weapon){double yaw=ang[1]*3.141592653589793/180.0;bool hl2Gun=!strcmp(SOURCEBOX_GAME,"tf2");snprintf(text,sizeof(text),"%.5f %.5f %.5f",xyz[0]+(hl2Gun?18*cos(yaw)+7*sin(yaw):0),xyz[1]+(hl2Gun?18*sin(yaw)-7*cos(yaw):0),xyz[2]+(hl2Gun?44:0));entities->SetKeyValue(weapon,"origin",text);snprintf(text,sizeof(text),"0 %.5f 0",ang[1]);entities->SetKeyValue(weapon,"angles",text);}
  Diagnostic(now);
 }
 void LevelShutdown()override{portalMarkers.clear();portalParts.clear();portalRevision=-1;extraPeers.clear();sceneObjects.clear();sceneSession.clear();sceneSequence=0;pendingMessages.clear();gmodPose=nullptr;gmodSession.clear();gmodSequence=0;bridge.PublishCube(nullptr);cubeAuthority=false;cubeFreezing=false;cubeFrozen=false;cubeSamples.clear();cubePrevious=nullptr;player=nullptr;proxy=nullptr;label=nullptr;weapon=nullptr;poseX=nullptr;poseY=nullptr;poseReady=0;edictBase=nullptr;edictCount=0;samples.clear();physicsSamples.clear();physicsSession.clear();physicsSeq=0;bridge.PublishProps(nullptr);bridge.Publish(nullptr);}
 void ClientActive(edict_t*e)override{Track(e);}void ClientDisconnect(edict_t*e)override{if(e==player){player=nullptr;bridge.Publish(nullptr);}}
 void ClientPutInServer(edict_t*e,const char*)override{Track(e);}void SetCommandClient(int)override{}
 void ClientSettingsChanged(edict_t*)override{}
 PLUGIN_RESULT ClientConnect(bool*,edict_t*,const char*,const char*,char*,int)override{return PLUGIN_CONTINUE;}
 PLUGIN_RESULT ClientCommand(edict_t*e,const CCommand&args)override{if(args.ArgC()&&!strcmp(args[0],"sourcebox_take")){if(e==player&&!SceneUse())bridge.Command({{"type","cube_take"}});return PLUGIN_STOP;}if(args.ArgC()&& !strcmp(args[0],"sourcebox_resetprops")){auto now=GetTickCount64();if(e==player&&now-lastReset>1000){ResetProps();ResetCube();lastReset=now;}return PLUGIN_STOP;}return PLUGIN_CONTINUE;}
 PLUGIN_RESULT NetworkIDValidated(const char*,const char*)override{return PLUGIN_CONTINUE;}
 void OnQueryCvarValueFinished(QueryCvarCookie_t,edict_t*,EQueryCvarValueStatus,const char*,const char*)override{}
 void OnEdictAllocated(edict_t*e)override{if(edictBase&&e>=edictBase&&e-edictBase<8192){int count=int(e-edictBase)+1;if(count>edictCount)edictCount=count;}}void OnEdictFreed(const edict_t*e)override{if(e==player)player=nullptr;}
};
static SourceboxAdapter adapter;
extern "C" __declspec(dllexport) void*CreateInterface(const char*name,int*result){
 if(!strcmp(name,INTERFACEVERSION_ISERVERPLUGINCALLBACKS)||!strcmp(name,INTERFACEVERSION_ISERVERPLUGINCALLBACKS_VERSION_2)){if(result)*result=0;return &adapter;}
 if(result)*result=1;return nullptr;
}













