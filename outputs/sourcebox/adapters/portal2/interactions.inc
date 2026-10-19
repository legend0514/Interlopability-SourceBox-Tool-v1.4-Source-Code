// Engine calls remain on the game thread. A handoff is acknowledged only after
// the old physical entity has actually disappeared from the native world.
Json cubeSnapshot;int cubeEpoch=0;bool cubeOwner=false,cubeSpawn=false,cubeFreeze=false;
std::string cubeRetiring;
 void RetireCube(){auto retired=RetireEntity(SOURCEBOX_CUBE_ID);if(!retired.empty())cubeRetiring=retired;}
 bool CubeRetired(){return cubeRetiring.empty()||!Named(cubeRetiring);}
 Json cubePortalPairs=Json::array();void UpdateCubePortals(){} // Native prop_portal handles this engine's physics.
unsigned long long propProbeAt=0,propProbeCheck=0;int propProbeStage=0;Vector propProbeEye;QAngle propProbeLook;Json propProbePosition;
void PropProbe(unsigned long long now){
 if(!propProbeStage){
  if(now<propProbeCheck)return;propProbeCheck=now+1000;
  FILE*f=fopen(SOURCEBOX_PORTAL2_LOG "-prop-probe","r");if(!f)return;fclose(f);
  void*entry=nullptr;
  for(const char*owner:{"hl1mp","gmod","hl2","tf2","cs2","portal2"})for(int endpoint=0;endpoint<2;endpoint++){
   auto name=std::string("sourcebox_portal_")+owner+"_"+std::to_string(endpoint);
   auto*e=Named(name);if(!e||!Named(std::string("sourcebox_portal_")+owner+"_"+std::to_string(1-endpoint)))continue;
   auto angle=ReadVector(e,"angles");if(-sin(double(angle[0])*3.141592653589793/180)>0.99)entry=e;
  }
  if(!entry||!cubeSnapshot.is_object()||!cubeSnapshot.value("alive",false))return;
  if(!tools->GetPlayerPosition(propProbeEye,propProbeLook))return;
  propProbePosition=ReadVector(entry,"origin");const auto&p=cubeSnapshot["position"];
  if(!tools->SnapPlayerToPosition(Vector(float(p[0])+70,float(p[1]),72),propProbeLook))return;
  remove(SOURCEBOX_PORTAL2_LOG "-prop-probe");propProbeAt=now;propProbeStage=1;
 }
 if(propProbeStage==1&&now-propProbeAt>1000){bridge.Command({{"type","cube_take"}});propProbeStage=2;}
 if(propProbeStage==2&&cubeOwner){RetireCube();propProbeStage=3;}
 if(propProbeStage==3&&CubeRetired()&&!Named(SOURCEBOX_CUBE_ID)){
  cubeSnapshot["alive"]=true;cubeSnapshot["position"]={propProbePosition[0],propProbePosition[1],double(propProbePosition[2])+160};cubeSnapshot["rotation"]={0,0,0};cubeSpawn=true;propProbeAt=now;propProbeStage=4;Log("native prop probe drop");
 }
 if((propProbeStage==4&&now-propProbeAt>5000)||now-propProbeAt>10000){tools->SnapPlayerToPosition(propProbeEye,propProbeLook);Log(("native prop probe result "+cubeSnapshot.dump()).c_str());propProbeStage=0;}
}
std::string cubeTransfer;unsigned long long cubeMissing=0;
struct SceneObject{Json state;std::string session;};std::map<int,SceneObject> scene;
void CubeMessage(const Json&m){auto type=m.value("type",std::string());
 if(type=="cube_lost"){bridge.PublishCube(nullptr);RetireCube();cubeEpoch=0;cubeOwner=false;cubeSpawn=false;cubeFreeze=false;return;}
 if(type=="cube_owner"){if(m.value("epoch",0)<cubeEpoch)return;bridge.PublishCube(nullptr);cubeEpoch=m["epoch"];cubeOwner=m.value("owner","")=="portal2";cubeSnapshot=m["state"];cubeSpawn=true;cubeFreeze=false;RetireCube();Log(cubeOwner?"cube ownership granted":"cube remote owner");}
 if(type=="cube_state"&&!cubeOwner&&m.value("epoch",0)==cubeEpoch){cubeSnapshot=m["state"];if(auto*e=Named(SOURCEBOX_CUBE_ID))Transform(e,cubeSnapshot);}
 if(type=="cube_freeze"&&cubeOwner&&m.value("epoch",0)==cubeEpoch){ReadCube();cubeTransfer=m.value("transfer","");cubeFreeze=true;cubeSpawn=false;bridge.PublishCube(nullptr);RetireCube();}
}
void ReadCube(){if(!cubeSnapshot.is_object())return;auto*e=Named(SOURCEBOX_CUBE_ID);cubeSnapshot["alive"]=e!=nullptr;cubeSnapshot["solid"]=e!=nullptr;cubeSnapshot["timestamp"]=GetTickCount64();if(e){cubeSnapshot["position"]=ReadVector(e,"origin");cubeSnapshot["rotation"]=ReadVector(e,"angles");cubeSnapshot["velocity"]=ReadVector(e,"velocity");cubeSnapshot["angularVelocity"]=ReadVector(e,"avelocity");}}
void CubeTick(unsigned long long now){
 if(cubeFreeze){if(CubeRetired()&&!Named(SOURCEBOX_CUBE_ID)){cubeOwner=false;cubeFreeze=false;cubeSnapshot["timestamp"]=now;cubeSnapshot["velocity"]={0,0,0};cubeSnapshot["angularVelocity"]={0,0,0};bridge.Command({{"type","cube_frozen"},{"epoch",cubeEpoch},{"transfer",cubeTransfer},{"state",cubeSnapshot}});}return;}
 if(cubeSpawn&&!CubeRetired())return;
 if(cubeSpawn&&!Named(SOURCEBOX_CUBE_ID)){cubeSpawn=false;if(!cubeSnapshot.value("alive",false))return;auto*e=tools->CreateEntityByName(cubeOwner?"prop_physics":"prop_dynamic_override");if(e){tools->SetKeyValue(e,"targetname",SOURCEBOX_CUBE_ID);tools->SetKeyValue(e,"model",SOURCEBOX_PORTAL2_CUBE_MODEL);tools->SetKeyValue(e,"rendercolor","80 180 255");tools->SetKeyValue(e,"solid",cubeOwner?"6":"0");if(cubeOwner)tools->SetKeyValue(e,"spawnflags","256");Transform(e,cubeSnapshot);tools->DispatchSpawn(e);}if(cubeOwner)bridge.Command({{"type","cube_ready"},{"epoch",cubeEpoch},{"created",e!=nullptr}});}
 if(cubeOwner){ReadCube();bridge.PublishCube({{"type","cube_state"},{"epoch",cubeEpoch},{"state",cubeSnapshot}});if(!Named(SOURCEBOX_CUBE_ID)){if(!cubeMissing)cubeMissing=now;if(now-cubeMissing>5000){cubeSnapshot["alive"]=true;cubeSnapshot["position"]=Json::parse(SOURCEBOX_CUBE_INITIAL);cubeSpawn=true;cubeMissing=0;}}else cubeMissing=0;}
}
void SceneMessage(const Json&m){auto session=m.value("session",std::string());auto live=m["live"].get<std::vector<int>>();for(auto it=scene.begin();it!=scene.end();){if(it->second.session!=session||std::find(live.begin(),live.end(),it->first)==live.end()){Input("sourcebox_scene_"+std::to_string(it->first),"Kill");it=scene.erase(it);}else ++it;}
 for(const auto&s:m["objects"]){int id=s["id"];auto name="sourcebox_scene_"+std::to_string(id);auto*e=Named(name);if(!e){e=tools->CreateEntityByName("prop_dynamic_override");if(!e)continue;tools->SetKeyValue(e,"targetname",name.c_str());auto model=s.value("renderModel",s.value("model",std::string()));if(s.value("model",std::string())=="models/gman.mdl")model="models/sourcebox/portal2/npc/gman.mdl";tools->SetKeyValue(e,"model",model.c_str());tools->SetKeyValue(e,"solid","2");Transform(e,s);tools->DispatchSpawn(e);Input(name,"EnableCollision");}Transform(e,s);auto clip=s.value("sequence",std::string());if(s.value("model",std::string())=="models/gman.mdl")clip="idle";if(!clip.empty())Input(name,"SetAnimation",clip);scene[id]={s,session};}
}
void UseNearby(const Vector&pos){double best=120*120;int nearest=-1;if(cubeSnapshot.is_object()&&cubeSnapshot.value("alive",false)){double d=0;for(int i=0;i<3;i++){double delta=pos[i]-double(cubeSnapshot["position"][i]);d+=delta*delta;}if(d<best&&!cubeOwner){best=d;nearest=0;}}
 for(const auto&entry:scene){double d=0;for(int i=0;i<3;i++){double delta=pos[i]-double(entry.second.state["position"][i]);d+=delta*delta;}if(d<best){best=d;nearest=entry.first;}}
 if(nearest==0){bridge.Command({{"type","cube_take"}});Log("E requested cube ownership");}else if(nearest>0){bridge.Command({{"type","scene_interact"},{"id",nearest},{"session",scene[nearest].session},{"action","use"}});Log("E requested scene interaction");}
}


