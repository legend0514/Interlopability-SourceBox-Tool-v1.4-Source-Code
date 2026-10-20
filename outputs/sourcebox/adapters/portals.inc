bool portalEnabled=false;IEngineTrace* portalTrace=nullptr;std::string portalConnection;
bool portalWorldsReady=false;int portalWorldSelection=0;unsigned long long portalWorldCheck=0;
void PortalNotice(const std::string&text){
 if(!entities)return;auto*e=Named("sourcebox_portal_notice");
 if(!e){e=entities->CreateEntityByName("game_text");if(!e)return;
  entities->SetKeyValue(e,"targetname","sourcebox_portal_notice");entities->SetKeyValue(e,"spawnflags","1");entities->SetKeyValue(e,"channel","4");entities->SetKeyValue(e,"x","-1");entities->SetKeyValue(e,"y","0.75");entities->SetKeyValue(e,"color","120 220 255");entities->SetKeyValue(e,"holdtime","4");entities->SetKeyValue(e,"fadein","0.1");entities->SetKeyValue(e,"fadeout","0.3");entities->DispatchSpawn(e);}
 entities->SetKeyValue(e,"message",text.c_str());Input("sourcebox_portal_notice","Display","");
}
void PortalWorldReadiness(){
 if(portalWorldsReady||!entities||portalConnection.empty()||GetTickCount64()<portalWorldCheck)return;
 portalWorldCheck=GetTickCount64()+1000;
 if(!Named("sourcebox_worlds_baked"))return;
 Json ids=Json::array();for(const auto&w:worldDestinations)if(strcmp(w.id,"sourcebox_room_001"))ids.push_back(w.id);
 bridge.Command({{"type","portal_world_ready"},{"worlds",ids}});portalWorldsReady=true;
 PortalNotice("Portal worlds loaded. Equip Portal Gun; hold E and press R to select a destination.");
}
long long portalRevision=-1,portalPendingRevision=-1;int portalPartCount=0,portalButtons=0;
static long long PortalClock(){FILETIME f;GetSystemTimeAsFileTime(&f);ULARGE_INTEGER t;t.LowPart=f.dwLowDateTime;t.HighPart=f.dwHighDateTime;return (t.QuadPart-116444736000000000ULL)/10000;}
std::map<int,Json>portalParts;std::vector<std::string>portalMarkers;std::deque<std::string>portalApplied;
struct PortalFilter:public ITraceFilter{IHandleEntity*skip;explicit PortalFilter(IHandleEntity*p):skip(p){}bool ShouldHitEntity(IHandleEntity*e,int)override{return e!=skip;}TraceType_t GetTraceType()const override{return TRACE_EVERYTHING;}};
void ClearPortalMarkers(){for(const auto&name:portalMarkers)PortalDelete(name);portalMarkers.clear();
#ifdef SOURCEBOX_PREVIEW_BRIDGE
 previewCount=0;
#endif
}
void PortalMessage(const Json&m){
 const auto type=m.value("type",std::string());
 if(type=="portal_notice"){PortalNotice(m.value("reason",std::string("Portal unavailable")));return;}
 if(type=="portal_lost"||type=="welcome"){portalWorldsReady=false;portalWorldSelection=0;portalWorldCheck=0;portalEnabled=m.value("portalsEnabled",false);ClearPortalMarkers();portalConnection=m.value("session",std::string());portalRevision=portalPendingRevision=-1;portalParts.clear();portalApplied.clear();cubePortalPairs=Json::array();UpdateCubePortals();return;}
 if(m.value("session",std::string())!=portalConnection)return;
 if(type=="portal_snapshot"){
  const auto revision=m.value("revision",-1LL);const int count=m.value("count",0),index=m.value("index",-1);
  if(revision<=portalRevision||revision<portalPendingRevision||count<1||count>6||index<0||index>=count)return;
  if(revision>portalPendingRevision){portalParts.clear();portalPendingRevision=revision;portalPartCount=count;}if(count!=portalPartCount)return;
  portalParts[index]=m["pairs"];if(portalParts.size()!=size_t(count))return;ClearPortalMarkers();
  for(const auto&part:portalParts)for(const auto&pair:part.second)for(const auto&end:pair["endpoints"]){if(end.is_null())continue;
   const auto name="sourcebox_portal_"+pair["owner"].get<std::string>()+"_"+std::to_string(end["endpoint"].get<int>())+"_"+std::to_string(revision);
#ifdef SOURCEBOX_SDK
   auto*e=entities->CreateEntityByName("sourcebox_portal");if(!e)continue;
   const char*owners[]={"hl2","tf2","hl1mp","gmod","cs2","portal2"};int pairIndex=-1;
   for(int i=0;i<6;i++)if(pair["owner"]==owners[i])pairIndex=i;
   if(pairIndex<0)continue;
   entities->SetKeyValue(e,"pairid",std::to_string(pairIndex).c_str());
   entities->SetKeyValue(e,"endpoint",std::to_string(end["endpoint"].get<int>()).c_str());
#else
   auto*e=entities->CreateEntityByName("prop_dynamic_override");if(!e)continue;
#endif
   entities->SetKeyValue(e,"targetname",name.c_str());entities->SetKeyValue(e,"model","models/sourcebox/portal2/portal_ring.mdl");entities->SetKeyValue(e,"solid","0");
   entities->SetKeyValue(e,"rendercolor",end["endpoint"].get<int>()?"255 130 20":"40 150 255");
   char v[128];snprintf(v,sizeof(v),"%.4f %.4f %.4f",double(end["position"][0])+double(end["normal"][0]),double(end["position"][1])+double(end["normal"][1]),double(end["position"][2])+double(end["normal"][2]));entities->SetKeyValue(e,"origin",v);
   snprintf(v,sizeof(v),"%.4f %.4f 0",-asin(double(end["normal"][2]))*180/3.141592653589793,atan2(double(end["normal"][1]),double(end["normal"][0]))*180/3.141592653589793);entities->SetKeyValue(e,"angles",v);entities->DispatchSpawn(e);portalMarkers.push_back(name);
  }
#ifdef SOURCEBOX_PREVIEW_BRIDGE
  for(const auto&part:portalParts)for(const auto&pair:part.second){
   const auto&ends=pair["endpoints"];if(ends.size()!=2||ends[0].is_null()||ends[1].is_null())continue;
   for(int side=0;side<2&&previewCount<12;side++){
    auto&out=previewPairs[previewCount++];out.halfWidth=SOURCEBOX_PREVIEW_HALF_WIDTH;out.halfHeight=SOURCEBOX_PREVIEW_HALF_HEIGHT;
    for(int j=0;j<2;j++){auto&dest=j?out.exit:out.entry;const auto&src=ends[j?1-side:side];
     for(int k=0;k<3;k++){dest.position[k]=src["position"][k];dest.normal[k]=src["normal"][k];dest.right[k]=src["right"][k];dest.up[k]=src["up"][k];}
    }
   }
  }
#endif
  cubePortalPairs=Json::array();for(const auto&part:portalParts)for(const auto&pair:part.second)cubePortalPairs.push_back(pair);UpdateCubePortals();portalRevision=revision;portalParts.clear();return;
 }
 if(type!="portal_traverse"||!portalTrace||!player)return;
 const auto event=m.value("event",std::string());if(event.empty()||std::find(portalApplied.begin(),portalApplied.end(),event)!=portalApplied.end())return;
 portalApplied.push_back(event);if(portalApplied.size()>128)portalApplied.pop_front();bool applied=false;
 auto*p=players->GetPlayerInfo(player);if(m.value("expiresAt",0LL)>PortalClock()&&p&&!p->IsDead()&&!p->IsObserver()){
  Vector destination{m["position"][0].get<float>(),m["position"][1].get<float>(),m["position"][2].get<float>()};
  Ray_t ray;ray.Init(destination,destination,p->GetPlayerMins(),p->GetPlayerMaxs());trace_t trace;PortalFilter filter(player->GetUnknown());portalTrace->TraceRay(ray,MASK_PLAYERSOLID,&filter,&trace);
  if(!trace.startsolid&&!trace.allsolid){QAngle angles(float(m["rotation"][0]),float(m["rotation"][1]),0);Vector currentEye;QAngle currentLook; if(ReadPortalEye(p,currentEye,currentLook)){const Vector viewOffset=currentEye-p->GetAbsOrigin();applied=SnapPortalPlayer(destination+viewOffset,angles);}if(applied){applied=ApplyPortalVelocity(Vector(float(m["velocity"][0]),float(m["velocity"][1]),float(m["velocity"][2])));}}
 }if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH,"a")){fprintf(f,"portal_result game=%s event=%s applied=%d\n",SOURCEBOX_GAME,event.c_str(),applied);fclose(f);}bridge.Command({{"type","portal_ack"},{"event",event},{"applied",applied}});
}
void PortalFire(IPlayerInfo*p,int endpoint){
 if(!portalTrace)return;
 Vector eye;QAngle angle;if(!ReadPortalEye(p,eye,angle))return;
 const float pitch=angle.x*3.141592653589793/180,yaw=angle.y*3.141592653589793/180;
 Vector direction(cos(pitch)*cos(yaw),cos(pitch)*sin(yaw),-sin(pitch));
 Ray_t ray;ray.Init(eye,eye+direction*2048);trace_t trace;PortalFilter filter(player->GetUnknown());portalTrace->TraceRay(ray,MASK_SHOT,&filter,&trace);
 if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH,"a")){fprintf(f,"portal_trace game=%s endpoint=%d eye=%.2f,%.2f,%.2f fraction=%.4f solid=%d/%d hit=%.2f,%.2f,%.2f\n",SOURCEBOX_GAME,endpoint,eye.x,eye.y,eye.z,trace.fraction,trace.startsolid,trace.allsolid,trace.endpos.x,trace.endpos.y,trace.endpos.z);fclose(f);}
 if(trace.startsolid||trace.allsolid||trace.fraction>=1)return;
 Json shot={{"type","portal_fire"},{"endpoint",endpoint},{"hit",{trace.endpos.x,trace.endpos.y,trace.endpos.z}},{"eye",{eye.x,eye.y,eye.z}}};
 if(portalWorldsReady&&portalWorldSelection>0)shot["destination"]=worldDestinations[portalWorldSelection-1].id;
 bridge.Command(shot);
}
bool PortalWeaponEquipped(IPlayerInfo*p){const char*w=p?p->GetWeaponName():nullptr;return w&&((!strcmp(SOURCEBOX_GAME,"hl2")&&(!strcmp(w,"weapon_cubemap")||!strcmp(w,"weapon_sourcebox_portalgun")))||(!strcmp(SOURCEBOX_GAME,"hl1mp")&&!strcmp(w,"weapon_egon"))||(!strcmp(SOURCEBOX_GAME,"tf2")&&!strcmp(w,"tf_weapon_grapplinghook"))||(!strcmp(SOURCEBOX_GAME,"portal2")&&!strcmp(w,"weapon_portalgun")));}
void PortalInput(IPlayerInfo*p){PortalWorldReadiness();
 static unsigned long long probeCheck=0;
 if(portalWorldsReady&&helpers&&GetTickCount64()>probeCheck){probeCheck=GetTickCount64()+1000;
  const std::string path=std::string(SOURCEBOX_DIAGNOSTIC_PATH)+"-world-fixture-"+SOURCEBOX_GAME;
  if(FILE*f=fopen(path.c_str(),"r")){fclose(f);remove(path.c_str());helpers->ClientCommand(player,"exec sourcebox_world_probe.cfg");}
 }
 const int buttons=p->GetLastUserCommand().buttons,pressed=buttons&~portalButtons;portalButtons=buttons; if(pressed){if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH,"a")){fprintf(f,"portal_input game=%s buttons=%d enabled=%d weapon=%s\n",SOURCEBOX_GAME,pressed,portalEnabled,p->GetWeaponName()?p->GetWeaponName():"none");fclose(f);}}
 if(!PortalWeaponEquipped(p))return;
 if((buttons&(1<<5))&&(buttons&(1<<13))&&(pressed&((1<<5)|(1<<13)))){
  if(!portalEnabled){PortalNotice("Portal connection unavailable. Check SourceBox connection status.");return;}
  if(!portalWorldsReady){PortalNotice("Destination maps are not loaded in this session.");return;}
  portalWorldSelection=(portalWorldSelection+1)%(int(sizeof(worldDestinations)/sizeof(worldDestinations[0]))+1);
  PortalNotice(std::string("Portal destination: ")+(portalWorldSelection?worldDestinations[portalWorldSelection-1].label:"Normal portals"));
  return;
 }
 if(!portalEnabled)return;
 if(pressed&1)PortalFire(p,0);
 if(pressed&(1<<11))PortalFire(p,1);
 if(pressed&(1<<13))bridge.Command({{"type","portal_clear"}});
}










