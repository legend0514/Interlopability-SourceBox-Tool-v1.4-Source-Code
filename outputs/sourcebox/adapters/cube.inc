// Included inside SourceboxAdapter: all engine access stays on GameFrame.
unsigned long long cubeMissingSince=0;
 void CubeNotice(const char*message){auto*e=Named("sourcebox_cube_notice");if(!e){e=entities->CreateEntityByName("game_text");if(!e)return;entities->SetKeyValue(e,"targetname","sourcebox_cube_notice");entities->SetKeyValue(e,"spawnflags","1");entities->SetKeyValue(e,"channel","3");entities->SetKeyValue(e,"x","-1");entities->SetKeyValue(e,"y","0.2");entities->SetKeyValue(e,"color","100 200 255");entities->SetKeyValue(e,"holdtime","4");entities->SetKeyValue(e,"fadein","0.1");entities->SetKeyValue(e,"fadeout","0.5");entities->DispatchSpawn(e);}entities->SetKeyValue(e,"message",message);Input("sourcebox_cube_notice","Display","");}
 int cubeEpoch=0;bool cubeAuthority=false,cubeFreezing=false,cubeFrozen=false;
std::string cubeOwner,cubeTransfer;Json cubeSnapshot,cubePrevious;
std::deque<Json>cubeSamples;unsigned long long cubeLastSample=0;
Json cubePortalPairs=Json::array();
void RunCubeScript(const std::string&code,const char*target=SOURCEBOX_CUBE_ID){
 auto*t=entities->CreateEntityByName("logic_timer");if(!t)return;
 std::string separator(1,char(27));std::string output=std::string(target)+separator+"RunScriptCode"+separator+code+separator+"0"+separator+"1";
 entities->SetKeyValue(t,"RefireTime","0.02");entities->SetKeyValue(t,"StartDisabled","0");entities->SetKeyValue(t,"OnTimer",output.c_str());entities->SetKeyValue(t,"OnTimer","!self,Kill,,0.01,1");entities->DispatchSpawn(t);
}
void UpdateCubePortals(){
 if(strcmp(SOURCEBOX_GAME,"hl1mp")||!cubeAuthority||cubeFreezing||!Named(SOURCEBOX_CUBE_ID))return;
 std::string code="SBPortalPairs=[";bool first=true;
 for(const auto&pair:cubePortalPairs){if(!pair.value("linked",false))continue;if(!first)code+=",";first=false;code+="[";
  for(int side=0;side<2;side++){if(side)code+=",";code+="[";const auto&end=pair["endpoints"][side];
   for(const char*key:{"position","normal","right","up"}){const auto&v=end[key];code+="Vector("+v[0].dump()+","+v[1].dump()+","+v[2].dump()+"),";}
   code+=end["aperture"]["halfWidth"].dump()+","+end["aperture"]["halfHeight"].dump()+"]";
  }code+="]";
 }code+="];SBPrevious=null;";
 RunCubeScript(code);
}
void DeleteCube(){if(auto*e=Named(SOURCEBOX_CUBE_ID))entities->RemoveEntityImmediate(e);}
void CubeTransform(CBaseEntity*e,const Json&s){for(const auto&entry:{std::pair<const char*,const char*>("position","origin"),{"rotation","angles"}}){const auto&v=s[entry.first];char text[128];snprintf(text,sizeof(text),"%.5f %.5f %.5f",v[0].get<double>(),v[1].get<double>(),v[2].get<double>());entities->SetKeyValue(e,entry.second,text);}}
void SpawnCube(const Json&s,bool physical){DeleteCube();if(!s.value("alive",false))return;auto*e=entities->CreateEntityByName(physical?"prop_physics":"prop_dynamic_override");if(!e)return;entities->SetKeyValue(e,"targetname",SOURCEBOX_CUBE_ID);entities->SetKeyValue(e,"model",SOURCEBOX_CUBE_MODEL);CubeTransform(e,s);entities->SetKeyValue(e,"rendercolor","80 180 255");if(physical){entities->SetKeyValue(e,"spawnflags","256");if(!strcmp(SOURCEBOX_GAME,"hl1mp"))entities->SetKeyValue(e,"vscripts","sourcebox_prop_portals.nut");}else{entities->SetKeyValue(e,"solid","0");entities->SetKeyValue(e,"DisableBoneFollowers","1");}entities->DispatchSpawn(e);if(physical)UpdateCubePortals();}
Json CubeRead(unsigned long long now){auto*e=Named(SOURCEBOX_CUBE_ID);Json s=cubeSnapshot;if(!s.is_object())return nullptr;s["timestamp"]=now;s["alive"]=e!=nullptr;s["solid"]=e!=nullptr;s["velocity"]={0,0,0};s["angularVelocity"]={0,0,0};if(e){s["position"]=ReadVector(e,"origin");s["rotation"]=ReadVector(e,"angles");if(cubePrevious.is_object()){double dt=(double(now)-cubePrevious["timestamp"].get<double>())/1000.0;if(dt>0&&dt<0.25)for(int i=0;i<3;i++){s["velocity"][i]=(s["position"][i].get<double>()-cubePrevious["position"][i].get<double>())/dt;double delta=s["rotation"][i].get<double>()-cubePrevious["rotation"][i].get<double>();s["angularVelocity"][i]=(fmod(delta+540.0,360.0)-180.0)/dt;}}}cubePrevious=s;return s;}
void CubeMessage(const Json&j){if(!SOURCEBOX_CUBE_ENABLED)return;const auto type=j.value("type",std::string());
 if(type=="cube_denied"){CubeNotice(j.value("reason",std::string("Transfer unavailable")).c_str());return;}
 if(type=="cube_lost"){bridge.PublishCube(nullptr);DeleteCube();cubeEpoch=0;cubeAuthority=false;cubeFreezing=false;cubeFrozen=false;cubeSamples.clear();cubePrevious=nullptr;return;}
 if(type=="cube_owner"){
  int epoch=j.value("epoch",0);if(epoch<cubeEpoch)return;bridge.PublishCube(nullptr);cubeEpoch=epoch;cubeOwner=j.value("owner","");cubeAuthority=cubeOwner==SOURCEBOX_GAME;cubeFreezing=false;cubeFrozen=false;cubeSamples.clear();cubePrevious=nullptr;cubeSnapshot=j["state"];SpawnCube(cubeSnapshot,cubeAuthority);cubeMissingSince=0;cubeLastSample=0;if(cubeAuthority){CubeNotice("You control the BLUE CUBE. Use or shoot it. F6 resets props.");bridge.Command({{"type","cube_ready"},{"epoch",cubeEpoch},{"created",Named(SOURCEBOX_CUBE_ID)!=nullptr}});}else CubeNotice("BLUE CUBE: approach and press E to take control.");return;
 }
 if(type=="cube_freeze"&&cubeAuthority&&j.value("epoch",0)==cubeEpoch){cubeSnapshot=CubeRead(GetTickCount64());cubeTransfer=j.value("transfer","");cubeFreezing=true;bridge.PublishCube(nullptr);DeleteCube();return;}
 if(type=="cube_state"&&!cubeAuthority&&!cubeFreezing&&j.value("epoch",0)==cubeEpoch){auto s=j["state"];if(!cubeSamples.empty()&&s["timestamp"].get<unsigned long long>()<=cubeSamples.back()["timestamp"].get<unsigned long long>())return;cubeSamples.push_back(s);while(cubeSamples.size()>8)cubeSamples.pop_front();cubeSnapshot=s;}
}
void CubeTick(unsigned long long now){
  if(!SOURCEBOX_CUBE_ENABLED){bridge.PublishCube(nullptr);DeleteCube();return;}
 if(cubeFreezing){if(!Named(SOURCEBOX_CUBE_ID)){cubeAuthority=false;cubeFreezing=false;cubeFrozen=true;cubeSnapshot["velocity"]={0,0,0};cubeSnapshot["angularVelocity"]={0,0,0};cubeSnapshot["timestamp"]=now;bridge.Command({{"type","cube_frozen"},{"epoch",cubeEpoch},{"transfer",cubeTransfer},{"state",cubeSnapshot}});}return;}
 if(cubeAuthority){if(!Named(SOURCEBOX_CUBE_ID)){if(!cubeMissingSince)cubeMissingSince=now;else if(now-cubeMissingSince>=SOURCEBOX_RESPAWN_MS){ResetCube();cubeMissingSince=0;}}else cubeMissingSince=0;if(now-cubeLastSample>=SOURCEBOX_UPDATE_MS){cubeSnapshot=CubeRead(now);bridge.PublishCube({{"type","cube_state"},{"epoch",cubeEpoch},{"state",cubeSnapshot}});cubeLastSample=now;}return;}
 if(cubeFrozen||cubeSamples.empty())return;
 auto target=now>SOURCEBOX_INTERPOLATION_MS?now-SOURCEBOX_INTERPOLATION_MS:0;while(cubeSamples.size()>2&&cubeSamples[1]["timestamp"].get<unsigned long long>()<=target)cubeSamples.pop_front();const auto&a=cubeSamples.front();const auto&b=cubeSamples.size()>1?cubeSamples[1]:a;
 if(!b.value("alive",false)){DeleteCube();return;}auto*e=Named(SOURCEBOX_CUBE_ID);if(!e){SpawnCube(b,false);e=Named(SOURCEBOX_CUBE_ID);}if(!e)return;
 double ta=a["timestamp"],tb=b["timestamp"],f=tb>ta?(double(target)-ta)/(tb-ta):1;f=f<0?0:f>1?1:f;Json interpolated=b;for(const char*k:{"position","rotation"})for(int i=0;i<3;i++){double v=a[k][i],d=b[k][i].get<double>()-v;if(!strcmp(k,"rotation"))d=fmod(d+540.0,360.0)-180.0;interpolated[k][i]=v+d*f;}CubeTransform(e,interpolated);
}
void ResetCube(){if(!cubeAuthority||cubeFreezing)return;cubeSnapshot["alive"]=true;cubeSnapshot["solid"]=true;cubeSnapshot["position"]=Json::parse(SOURCEBOX_CUBE_INITIAL);cubeSnapshot["rotation"]={0,0,0};cubeSnapshot["velocity"]={0,0,0};cubeSnapshot["angularVelocity"]={0,0,0};cubePrevious=nullptr;SpawnCube(cubeSnapshot,true);}



