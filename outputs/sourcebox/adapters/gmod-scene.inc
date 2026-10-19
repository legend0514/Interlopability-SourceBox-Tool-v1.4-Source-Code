// GMod owns physics and NPC AI. Remote interactions are routed back to it.
std::map<int,std::string> sceneAnimations;std::map<int,Json> sceneObjects;std::string sceneSession;unsigned long long sceneSequence=0,sceneReceived=0;
std::string SceneName(int id){return "sourcebox_gmod_prop_"+std::to_string(id);}
void DeleteSceneEntity(int id){sceneAnimations.erase(id);for(auto name:{SceneName(id),SceneName(id)+"_hit"})if(auto*e=Named(name.c_str()))entities->RemoveEntityImmediate(e);}
void ClearScene(){for(const auto&entry:sceneObjects)DeleteSceneEntity(entry.first);sceneObjects.clear();sceneSession.clear();sceneSequence=0;}
void SceneMessage(const Json&j,unsigned long long now){
 if(sceneSession!=j.value("session","")){ClearScene();sceneSession=j.value("session","");}
 if(j.value("seq",0ULL)<=sceneSequence)return;sceneSequence=j.value("seq",0ULL);sceneReceived=now;
 const auto&live=j["live"];
 for(auto it=sceneObjects.begin();it!=sceneObjects.end();){bool found=false;for(const auto&id:live)if(id.get<int>()==it->first)found=true;if(!found){DeleteSceneEntity(it->first);it=sceneObjects.erase(it);}else ++it;}
 for(const auto&o:j["objects"])sceneObjects[o["id"].get<int>()]=o;
}
void SceneTick(unsigned long long now){
 if(now-sceneReceived>2000){ClearScene();return;}
 for(const auto&entry:sceneObjects){auto name=SceneName(entry.first);auto*e=Named(name.c_str());bool fresh=!e;const auto&o=entry.second;
  if(!e){e=entities->CreateEntityByName("prop_dynamic_override");if(!e)continue;entities->SetKeyValue(e,"targetname",name.c_str());entities->SetKeyValue(e,"model",o.value("renderModel",o["model"].get<std::string>()).c_str());entities->SetKeyValue(e,"solid","2");entities->SetKeyValue(e,"DisableBoneFollowers","1");entities->SetKeyValue(e,"health","100000");entities->SetKeyValue(e,"OnTakeDamage",(name+"_hit,SetValue,1,0,-1").c_str());entities->DispatchSpawn(e);
   auto*relay=entities->CreateEntityByName("logic_branch");if(relay){entities->SetKeyValue(relay,"targetname",(name+"_hit").c_str());entities->DispatchSpawn(relay);}
  }
  if(auto*relay=Named((name+"_hit").c_str())){char hit[32]{};if(entities->GetKeyValue(relay,"InitialValue",hit,sizeof(hit))&&(atoi(hit)||!strcmp(hit,"true"))){entities->SetKeyValue(relay,"InitialValue","0");bridge.Command({{"type","scene_interact"},{"id",entry.first},{"session",sceneSession},{"action","hit"}});}}
  if(o.value("kind","")=="npc"&&!o.value("sequence","").empty()){auto sequence=o["sequence"].get<std::string>();if(fresh||sceneAnimations[entry.first]!=sequence){sceneAnimations[entry.first]=sequence;Input(name.c_str(),"SetAnimation",sequence.c_str());}}
  char text[128];auto old=ReadVector(e,"origin"),angle=ReadVector(e,"angles");double p[3],a[3];for(int i=0;i<3;i++){p[i]=fresh?double(o["position"][i]):double(old[i])+(double(o["position"][i])-double(old[i]))*0.35;double delta=fmod(double(o["rotation"][i])-double(angle[i])+540.0,360.0)-180.0;a[i]=fresh?double(o["rotation"][i]):double(angle[i])+delta*0.35;}
  snprintf(text,sizeof(text),"%.5f %.5f %.5f",p[0],p[1],p[2]);entities->SetKeyValue(e,"origin",text);snprintf(text,sizeof(text),"%.5f %.5f %.5f",a[0],a[1],a[2]);entities->SetKeyValue(e,"angles",text);
 }
}

 bool SceneUse(){
  if(!player||GetTickCount64()-sceneReceived>2000)return false;auto*info=players->GetPlayerInfo(player);if(!info)return false;
  const auto p=info->GetAbsOrigin();int nearest=0;double best=160.0;
  for(const auto&entry:sceneObjects){const auto&v=entry.second["position"];double x=double(v[0])-p.x,y=double(v[1])-p.y,z=double(v[2])-p.z;double d=sqrt(x*x+y*y+z*z);if(d<best){best=d;nearest=entry.first;}}
  if(!nearest)return false;
  if(auto*cube=Named(SOURCEBOX_CUBE_ID)){const auto v=ReadVector(cube,"origin");double x=double(v[0])-p.x,y=double(v[1])-p.y,z=double(v[2])-p.z;if(sqrt(x*x+y*y+z*z)<best)return false;}
  bridge.Command({{"type","scene_interact"},{"id",nearest},{"session",sceneSession},{"action","use"}});return true;
 }
