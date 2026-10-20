#include "../transport.h"
#include "filesystem.h"
#include "iserverplugin.h"
#include "iplayerinfo.h"
#include "tools-prefix.h"
#include "trace-prefix.h"
#include "proxy_config.h"
#include "hl1mp_config.h"
#include "physics_config.h"
#include "world_destinations.h"
#include <cmath>
#include <algorithm>
#include <map>
#include <cstdio>
class PortalAdapter final:public IServerPluginCallbacks {
 Bridge bridge;PortalTools* tools=nullptr;IPlayerInfoManager* players=nullptr;
 IServerPluginHelpers* helpers=nullptr;edict_t* player=nullptr;unsigned long long last=0;bool active=false;
 struct Peer{std::string session,model,clip;unsigned long long seq=0,received=0;void* entity=nullptr;};std::map<std::string,Peer> peers;
 Json hl1Models=Json::parse(hl1mpModelConfig),csTeams=Json::parse(cs2TeamConfig),csClips=Json::parse(cs2SequenceConfig);
 int crossplayHealthCeiling=100;unsigned long long protectedUntil=0;bool useDown=false;
 void* Named(const std::string&name){char n[256];for(auto*e=tools->FirstEntity();e;e=tools->NextEntity(e))if(tools->GetKeyValue(e,"targetname",n,sizeof(n))&&name==n)return e;return nullptr;}
 Json ReadVector(void*e,const char*k){char s[128]{};float x=0,y=0,z=0;if(e&&tools->GetKeyValue(e,k,s,sizeof(s)))sscanf(s,"%f %f %f",&x,&y,&z);return {x,y,z};}
 void Transform(void*e,const Json&s){for(auto pair:{std::pair<const char*,const char*>("position","origin"),{"rotation","angles"}}){char v[128];snprintf(v,sizeof(v),"%.4f %.4f %.4f",double(s[pair.first][0]),double(s[pair.first][1]),double(s[pair.first][2]));tools->SetKeyValue(e,pair.second,v);}}
 std::pair<std::string,std::string> Appearance(const std::string&id,const Json&s){double x=s["velocity"][0],y=s["velocity"][1];bool moving=x*x+y*y>64;auto key=!s.value("grounded",true)?"air":moving?"run":"idle";
  if(id=="tf2"){int cls=s.value("classId",6);if(cls<1||cls>9)cls=6;if(cls==6)return {"models/sourcebox/portal2/peers/heavy.mdl",key};if(cls==3)return {"models/sourcebox/portal2/peers/soldier.mdl",key};/* Retail TF2 class animation models are not compatible with Portal 2. Use a compiled peer until that class is converted. */ return {"models/sourcebox/portal2/peers/soldier.mdl",key};}
  if(id=="hl1mp"){auto model=s.value("model",std::string("models/player/mp/gordon/gordon.mdl"));if(!hl1Models.contains(model))model="models/player/mp/gordon/gordon.mdl";return {model,hl1Models[model]["sequences"][key]["name"].get<std::string>()};}
  if(id=="cs2")return {csTeams[s.value("team",3)==2?"2":"3"]["model"].get<std::string>(),csClips[key].get<std::string>()};
  if(id=="gmod"){auto model=s.value("model",std::string("models/player/kleiner.mdl"));if(model=="models/goblin/playerm/goblin.mdl")return {"models/sourcebox/portal2/peers/goblin.mdl",key};if(model=="models/player/kleiner.mdl")return {"models/sourcebox/portal2/peers/gmod.mdl",key};return {model,s.value("sequence",std::string(moving?"run_all":"idle_all_01"))};}
  return {"models/sourcebox/hl2/player.mdl",moving?gordon.run:gordon.idle};
 }
 void Log(const char* msg){FILE*f=fopen(SOURCEBOX_PORTAL2_LOG,"a");if(f){fprintf(f,"%llu %s\n",GetTickCount64(),msg);fclose(f);}}
 void* Local(){char n[128];for(auto*e=tools->FirstEntity();e;e=tools->NextEntity(e))if(tools->GetKeyValue(e,"classname",n,sizeof(n))&&!strcmp(n,"player"))return e;return nullptr;}
 unsigned long long momentumProbeAt=0,momentumProbeCheck=0;Vector momentumOriginal;QAngle momentumLook;
 void MomentumProbe(unsigned long long now){
  if(momentumProbeAt){
   auto*info=players->GetPlayerInfo(player);if(info&&Local()&&info->GetHealth()>crossplayHealthCeiling)tools->SetKeyValue(Local(),"health",std::to_string(crossplayHealthCeiling).c_str());if(!info)return;
   const auto p=info->GetAbsOrigin();const auto v=ReadVector(Local(),"velocity");
   Log(("momentum_probe "+Json({{"elapsed",now-momentumProbeAt},{"position",{p.x,p.y,p.z}},{"velocity",v}}).dump()).c_str());
   if(now-momentumProbeAt>=3000){tools->SnapPlayerToPosition(momentumOriginal,momentumLook);momentumProbeAt=0;}
   return;
  }
  if(now<momentumProbeCheck)return;momentumProbeCheck=now+1000;
  FILE*f=fopen(SOURCEBOX_PORTAL2_LOG "-momentum-probe","r");if(!f)return;fclose(f);
  // The preceding HL2 fixture places this floor-to-wall pair. Require both
  // native entities before dropping the actual Portal 2 player above it.
  if(!Named("sourcebox_portal_hl2_0")||!Named("sourcebox_portal_hl2_1"))return;
  if(!tools->GetPlayerPosition(momentumOriginal,momentumLook))return;
  remove(SOURCEBOX_PORTAL2_LOG "-momentum-probe");
  if(tools->SnapPlayerToPosition(Vector(0,-320,214),QAngle(0,0,0))){momentumProbeAt=now;Log("momentum_probe_begin");}
 }
 void Input(const std::string&name,const std::string&input,const std::string&value=""){if(player&&helpers)helpers->ClientCommand(player,("ent_fire "+name+" "+input+" "+value+"\n").c_str());}
 std::string RetireEntity(const std::string&name){
  auto*e=Named(name);if(!e)return {};static unsigned long long serial=0;
  const auto retired=name+"_retired_"+std::to_string(++serial);
  tools->SetKeyValue(e,"targetname",retired.c_str());Input(retired,"Kill");return retired;
 }
 void Remove(const std::string&id){auto i=peers.find(id);if(i==peers.end())return;RetireEntity("sourcebox_peer_"+id);RetireEntity("sourcebox_peer_"+id+"_hit");peers.erase(i);}
 void Clear(){while(!peers.empty())Remove(peers.begin()->first);bridge.Publish(nullptr);}
 #include "interactions.inc"
 void PortalDelete(const std::string&name){RetireEntity(name);}
 bool ReadPortalEye(IPlayerInfo*,Vector&eye,QAngle&angle){return tools->GetPlayerPosition(eye,angle);}
 bool SnapPortalPlayer(const Vector&eye,const QAngle&angle){return tools->SnapPlayerToPosition(eye,angle);} bool ApplyPortalVelocity(const Vector&v){char text[128];snprintf(text,sizeof(text),"%.4f %.4f %.4f",v.x,v.y,v.z);return tools->SetKeyValue(player->GetNetworkable()->GetBaseEntity(),"velocity",text);}
 #define IEngineTrace PortalEngineTrace
 #define Ray_t PortalRay
 #define entities tools
 #define PortalMessage LegacyPortalMessage
 #include "../portals.inc"
 #undef PortalMessage
 #undef entities
 #undef Ray_t
 #undef IEngineTrace
 #include "native-portals.inc"
 #define SOURCEBOX_PORTAL2_RAGDOLLS
 #define entities tools
 #include "../ragdolls.inc"
 #undef entities

public:
 bool Load(CreateInterfaceFn engine,CreateInterfaceFn game)override{
  tools=(PortalTools*)game("VSERVERTOOLS001",nullptr);players=(IPlayerInfoManager*)game("PlayerInfoManager002",nullptr);helpers=(IServerPluginHelpers*)engine("ISERVERPLUGINHELPERS001",nullptr);
  if(!tools||!players||!helpers){Log("missing required public interface");return false;}Log("Portal 2 public interfaces acquired");portalTrace=(PortalEngineTrace*)engine("EngineTraceServer004",nullptr);bridge.PortalCapability(false);bridge.Start();return true;
 }
 void Unload()override{ClearPortalMarkers();Clear();bridge.Stop();}void Pause()override{active=false;bridge.Publish(nullptr);}void UnPause()override{active=true;}
 const char*GetPluginDescription()override{return "SOURCEBOX Portal 2 native portal replication candidate";}
 void LevelInit(const char*map)override{portalWorldsReady=false;portalWorldSelection=0;portalWorldCheck=0;peers.clear();player=nullptr;crossplayHealthCeiling=100;active=!strcmp(map,"sourcebox_room_001");bridge.Publish(nullptr);Log(map);}
 void ServerActivate(edict_t*,int,int)override{bridge.Command({{"type","portal_sync"}});bridge.Command({{"type","cube_sync"}});}
 void GameFrame(bool simulating)override{
  if(!active||!simulating||!player)return;auto now=GetTickCount64();if(now-last<50)return;last=now;static unsigned long long retry=0;if(now-retry>1000){retry=now;if(portalEnabled&&portalRevision<0)bridge.Command({{"type","portal_sync"}});if(!cubeSnapshot.is_object())bridge.Command({{"type","cube_sync"}});}
  auto*info=players->GetPlayerInfo(player);if(info&&Local()&&info->GetHealth()>crossplayHealthCeiling)tools->SetKeyValue(Local(),"health",std::to_string(crossplayHealthCeiling).c_str());if(!info||!info->IsPlayer()||info->IsDead()){bridge.Publish(nullptr);return;}
  if(portalEnabled){char cls[128],a[16],b[16];for(auto*e=tools->FirstEntity();e;e=tools->NextEntity(e)){
   if(!tools->GetKeyValue(e,"classname",cls,sizeof(cls)))continue;
   if(!strcmp(cls,"weapon_portalgun")){tools->SetKeyValue(e,"CanFirePortal1","0");tools->SetKeyValue(e,"CanFirePortal2","0");
    if(!tools->GetKeyValue(e,"CanFirePortal1",a,sizeof(a))||!tools->GetKeyValue(e,"CanFirePortal2",b,sizeof(b))||atoi(a)||atoi(b)){Log("native portal authority disable failed");portalEnabled=false;break;}}
   if(!strcmp(cls,"prop_portal")){char name[160]{};if(tools->GetKeyValue(e,"targetname",name,sizeof(name))&&!strncmp(name,"sourcebox_portal_",17))continue;tools->SetKeyValue(e,"targetname","sourcebox_native_portal");Input("sourcebox_native_portal","Fizzle");}
  }}
  PortalInput(info);
  MomentumProbe(now);PropProbe(now);
  auto pos=info->GetAbsOrigin();QAngle angle=info->GetAbsAngles();Vector eye;tools->GetPlayerPosition(eye,angle);auto*local=Local();char velocity[128]{},flags[32]{};float x=0,y=0,z=0;bool velocityOK=local&&tools->GetKeyValue(local,"velocity",velocity,sizeof(velocity));if(velocityOK)sscanf(velocity,"%f %f %f",&x,&y,&z);
  bool flagsOK=local&&tools->GetKeyValue(local,"flags",flags,sizeof(flags));
  // Portal 2 does not expose flags through GetKeyValue on this build. Until a
  // native trace interface is verified, vertical velocity is an explicit estimate.
  bridge.Publish({{"health",info->GetHealth()},{"weapon",info->GetWeaponName()?info->GetWeaponName():""},{"entity","player:portal2"},{"timestamp",now},{"position",{pos.x,pos.y,pos.z}},{"rotation",{angle.x,angle.y,angle.z}},{"velocity",{x,y,z}},{"grounded",flagsOK?(atoi(flags)&1)!=0:velocityOK&&fabs(z)<1.0f}});
  static unsigned long long diagnostic=0;if(now-diagnostic>5000){char msg[256];snprintf(msg,sizeof(msg),"native sample %.2f %.2f %.2f velocity_read=%d flags_read=%d",pos.x,pos.y,pos.z,velocityOK,flagsOK);Log(msg);diagnostic=now;}
  if(local)tools->SetKeyValue(local,"targetname","sourcebox_local_player");
  for(auto&m:bridge.Drain()){
   // Keep retail Portal 2 stable until custom corpse replication is validated.
   if(m.value("type","")=="player_ragdoll"){continue;}
   if(m.value("type",std::string()).find("portal_")==0||m.value("type","")=="welcome"){PortalMessage(m);continue;}
   if(m.value("type",std::string()).find("cube_")==0){CubeMessage(m);continue;}
   if(m.value("type","")=="scene"){SceneMessage(m);continue;}
   if(m.value("type","")=="player_damage"){if(now>=protectedUntil){int health=info->GetHealth()-m.value("amount",10);if(health<=0){bridge.Command({{"type","player_death"}});health=100;tools->SnapPlayerToPosition(Vector(0,320,72),QAngle(0,-90,0));protectedUntil=now+1000;}crossplayHealthCeiling=health;if(local)tools->SetKeyValue(local,"health",std::to_string(health).c_str());Log("applied remote player damage");}continue;}
   auto type=m.value("type",std::string()),id=m.value("game",std::string());if(type=="remove"){if(id.empty())Clear();else Remove(id);continue;}if(type!="remote")continue;
   if(id!="hl2"&&id!="tf2"&&id!="gmod"&&id!="hl1mp"&&id!="cs2")continue;
   auto session=m.value("session",std::string());if(peers.count(id)&&peers[id].session!=session)Remove(id);auto&s=m["state"];auto&peer=peers[id];auto seq=s.value("seq",0ULL);if(seq<=peer.seq)continue;peer.seq=seq;peer.session=session;peer.received=now;
   auto appearance=Appearance(id,s);auto name="sourcebox_peer_"+id;
   if(peer.entity&&peer.model!=appearance.first){Remove(id);continue;}
   if(!peer.entity){auto*h=tools->CreateEntityByName("logic_branch");if(h){tools->SetKeyValue(h,"targetname",(name+"_hit").c_str());tools->DispatchSpawn(h);}peer.entity=tools->CreateEntityByName("prop_dynamic_override");if(!peer.entity)continue;tools->SetKeyValue(peer.entity,"targetname",name.c_str());tools->SetKeyValue(peer.entity,"model",appearance.first.c_str());tools->SetKeyValue(peer.entity,"solid","2");tools->SetKeyValue(peer.entity,"DisableBoneFollowers","1");tools->SetKeyValue(peer.entity,"OnTakeDamage",(name+"_hit,SetValue,1,0,-1").c_str());Transform(peer.entity,s);tools->DispatchSpawn(peer.entity);Input(name,"EnableCollision");peer.model=appearance.first;Log(("created "+id+" proxy "+peer.model).c_str());}
   if(peer.clip!=appearance.second){Input(name,"SetAnimation",appearance.second);peer.clip=appearance.second;}
   tools->SetKeyValue(peer.entity,"skin",id=="tf2"&&s.value("team",2)==3?"1":"0");
   char text[128];snprintf(text,sizeof(text),"%.3f %.3f %.3f",double(s["position"][0]),double(s["position"][1]),double(s["position"][2]));tools->SetKeyValue(peer.entity,"origin",text);snprintf(text,sizeof(text),"0 %.3f 0",double(s["rotation"][1]));tools->SetKeyValue(peer.entity,"angles",text);
  }
  CubeTick(now);bool pressed=(info->GetLastUserCommand().buttons&(1<<5))!=0;if(pressed&&!useDown)UseNearby(pos);useDown=pressed;
  for(const auto&entry:peers){auto name="sourcebox_peer_"+entry.first+"_hit";if(auto*h=Named(name)){char value[32]{};if(tools->GetKeyValue(h,"InitialValue",value,sizeof(value))&&(atoi(value)||!strcmp(value,"true"))){tools->SetKeyValue(h,"InitialValue","0");bridge.Command({{"type","player_hit"},{"target",entry.first}});}}}
  for(auto it=peers.begin();it!=peers.end();){auto id=it->first;bool stale=now-it->second.received>2000;++it;if(stale)Remove(id);}
 }
 void LevelShutdown()override{portalMarkers.clear();portalParts.clear();portalRevision=-1;peers.clear();player=nullptr;crossplayHealthCeiling=100;active=false;bridge.Publish(nullptr);}
 void ClientActive(edict_t*e)override{player=e;Log("local player active");}void ClientFullyConnect(edict_t*e)override{player=e;}
 void ClientDisconnect(edict_t*e)override{if(player==e){Clear();player=nullptr;}}void ClientPutInServer(edict_t*e,const char*)override{player=e;}
 void SetCommandClient(int)override{}void ClientSettingsChanged(edict_t*)override{}
 PLUGIN_RESULT ClientConnect(bool*,edict_t*,const char*,const char*,char*,int)override{return PLUGIN_CONTINUE;}
 PLUGIN_RESULT ClientCommand(edict_t*,const CCommand&)override{return PLUGIN_CONTINUE;}
 PLUGIN_RESULT NetworkIDValidated(const char*,const char*)override{return PLUGIN_CONTINUE;}
 void OnQueryCvarValueFinished(QueryCvarCookie_t,edict_t*,EQueryCvarValueStatus,const char*,const char*)override{}
 void OnEdictAllocated(edict_t*)override{}void OnEdictFreed(const edict_t*)override{}
};
static PortalAdapter adapter;
extern "C" __declspec(dllexport)void*CreateInterface(const char*n,int*r){if(!strcmp(n,"ISERVERPLUGINCALLBACKS003")){if(r)*r=0;return &adapter;}if(r)*r=1;return nullptr;}











