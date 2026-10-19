struct ExtraPeer {Json pose;std::string session,model,animation,weapon;unsigned long long seq=0,received=0,poseReady=0,lastPose=0;};
std::map<std::string,ExtraPeer> extraPeers;
Json cs2Teams=Json::parse(cs2TeamConfig),cs2Sequences=Json::parse(cs2SequenceConfig);
Json hl1Models=Json::parse(hl1mpModelConfig),hl1Weapons=Json::parse(hl1mpWeaponConfig);
std::string ExtraName(const std::string&game){return "sourcebox_peer_"+game;}
void ClearExtra(const std::string&game){auto name=ExtraName(game);for(const auto&suffix:{"","_weapon","_hit","_pose_x","_pose_y"})if(auto*e=Named((name+suffix).c_str()))entities->RemoveEntityImmediate(e);extraPeers.erase(game);}
void ClearExtras(){while(!extraPeers.empty())ClearExtra(extraPeers.begin()->first);}
void ExtraMessage(const Json&j,unsigned long long now){
 auto game=j.value("game",std::string());if(j.value("type","")=="remove"){ClearExtra(game);return;}
 if(j.contains("state")&&j["state"].contains("props"))for(const auto&p:j["state"]["props"]){auto name=p["id"].get<std::string>();const SharedProp*def=nullptr;for(const auto&d:sharedProps)if(name==d.id)def=&d;if(!def)continue;auto*e=Named(name.c_str());if(!p.value("alive",false)){if(e)entities->RemoveEntityImmediate(e);continue;}if(!e){e=entities->CreateEntityByName("prop_dynamic_override");if(!e)continue;entities->SetKeyValue(e,"targetname",name.c_str());entities->SetKeyValue(e,"model",def->model);entities->SetKeyValue(e,"solid","2");entities->DispatchSpawn(e);}CubeTransform(e,p);}
 if(j.value("type","")!="remote")return;
 auto session=j.value("session",std::string());if(extraPeers.count(game)&&extraPeers[game].session!=session)ClearExtra(game);
 auto&peer=extraPeers[game];auto pose=j["state"];if(pose.value("seq",0ULL)<=peer.seq)return;peer.seq=pose.value("seq",0ULL);peer.session=session;peer.pose=pose;peer.received=now;
}
void TickExtras(unsigned long long now){
 std::vector<std::string>expired;
 for(auto&entry:extraPeers){const auto&game=entry.first;auto&peer=entry.second;if(now-peer.received>2000){expired.push_back(game);continue;}const auto&s=peer.pose;std::string model,anim,weaponModel;double vx=s["velocity"][0],vy=s["velocity"][1];bool moving=vx*vx+vy*vy>64;bool grounded=s.value("grounded",true);
  if(game=="hl1mp"){model=s.value("model",std::string("models/player/mp/gordon/gordon.mdl"));if(!hl1Models.contains(model))continue;auto key=!grounded?"air":moving?"run":"idle";anim=hl1Models[model]["sequences"][key]["name"].get<std::string>();auto w=s.value("weapon",std::string());if(hl1Weapons.contains(w))weaponModel=hl1Weapons[w]["world"].get<std::string>();}
  else if(game=="portal2"){model="models/sourcebox/portal2/chell.mdl";anim=!grounded?"air":moving?"run":"idle";}
  else if(game=="cs2"){model=cs2Teams[s.value("team",3)==2?"2":"3"]["model"].get<std::string>();anim=cs2Sequences[!grounded?"air":moving?"run":"idle"].get<std::string>();}
  else if(game=="tf2"){int cls=s.value("classId",0);if(cls<1||cls>9)continue;const auto&c=tfClasses[cls-1];model=c.model;anim=!grounded?c.air:moving?c.run:c.idle;auto w=s.value("weapon",std::string());for(const auto&def:weaponModels)if(w==def.name)weaponModel=def.model;}
  else{model="models/sourcebox/hl2/player.mdl";anim=moving?gordon.run:gordon.idle;auto w=s.value("weapon",std::string());for(const auto&def:hl2WeaponModels)if(w==def.name)weaponModel=def.model;}
  auto name=ExtraName(game);auto*e=Named(name.c_str());if(e&&peer.model!=model){entities->RemoveEntityImmediate(e);e=nullptr;}
  if(!e){auto*relay=Named((name+"_hit").c_str());if(!relay){relay=entities->CreateEntityByName("logic_branch");if(relay){entities->SetKeyValue(relay,"targetname",(name+"_hit").c_str());entities->DispatchSpawn(relay);}}
   e=entities->CreateEntityByName("prop_dynamic_override");if(!e)continue;entities->SetKeyValue(e,"targetname",name.c_str());entities->SetKeyValue(e,"model",model.c_str());entities->SetKeyValue(e,"solid","2");entities->SetKeyValue(e,"DisableBoneFollowers","1");entities->SetKeyValue(e,"OnTakeDamage",(name+"_hit,SetValue,1,0,-1").c_str());CubeTransform(e,s);entities->DispatchSpawn(e);Input(name.c_str(),"EnableCollision","");peer.model=model;peer.animation.clear();
   if(game=="cs2"){for(int axis=0;axis<2;axis++){auto key=name+(axis?"_pose_y":"_pose_x");if(auto*old=Named(key.c_str()))entities->RemoveEntityImmediate(old);auto*c=entities->CreateEntityByName("point_posecontroller");if(c){entities->SetKeyValue(c,"targetname",key.c_str());entities->SetKeyValue(c,"PropName",name.c_str());entities->SetKeyValue(c,"PoseParameterName",axis?"move_y":"move_x");entities->SetKeyValue(c,"PoseValue","0.5");entities->SetKeyValue(c,"InterpolationTime","0.1");entities->SetKeyValue(c,"CycleFrequency","0");entities->DispatchSpawn(c);}}peer.poseReady=now+1200;}
  }
  auto old=ReadVector(e,"origin");char text[128];double p[3];for(int i=0;i<3;i++)p[i]=double(old[i])+(double(s["position"][i])-double(old[i]))*.35;snprintf(text,sizeof(text),"%.4f %.4f %.4f",p[0],p[1],p[2]);entities->SetKeyValue(e,"origin",text);double yaw=s["rotation"][1];snprintf(text,sizeof(text),"0 %.4f 0",yaw);entities->SetKeyValue(e,"angles",text);entities->SetKeyValue(e,"skin",game=="tf2"&&s.value("team",2)==3?"1":"0");
  if(game=="cs2"){if(peer.poseReady&&now>=peer.poseReady){peer.animation.clear();peer.poseReady=0;}if(!peer.poseReady&&now-peer.lastPose>=100){double speed=sqrt(vx*vx+vy*vy),r=yaw*3.141592653589793/180;double forward=speed>8?(cos(r)*vx+sin(r)*vy)/speed:0,side=speed>8?(-sin(r)*vx+cos(r)*vy)/speed:0;snprintf(text,sizeof(text),"%.5f",.5+.5*forward);Input((name+"_pose_x").c_str(),"SetPoseValue",text);snprintf(text,sizeof(text),"%.5f",.5+.5*side);Input((name+"_pose_y").c_str(),"SetPoseValue",text);peer.lastPose=now;}}
  if(peer.animation!=anim){Input(name.c_str(),"SetAnimation",anim.c_str());peer.animation=anim;}
  auto*weapon=Named((name+"_weapon").c_str());if(weapon&&peer.weapon!=weaponModel){entities->RemoveEntityImmediate(weapon);weapon=nullptr;}if(!weapon&&!weaponModel.empty()){weapon=entities->CreateEntityByName("prop_dynamic_override");if(weapon){entities->SetKeyValue(weapon,"targetname",(name+"_weapon").c_str());entities->SetKeyValue(weapon,"model",weaponModel.c_str());entities->SetKeyValue(weapon,"solid","0");entities->DispatchSpawn(weapon);}peer.weapon=weaponModel;}
  if(weapon){double r=yaw*3.141592653589793/180;snprintf(text,sizeof(text),"%.4f %.4f %.4f",p[0]+18*cos(r)+7*sin(r),p[1]+18*sin(r)-7*cos(r),p[2]+44);entities->SetKeyValue(weapon,"origin",text);snprintf(text,sizeof(text),"0 %.4f 0",yaw);entities->SetKeyValue(weapon,"angles",text);}
  if(auto*relay=Named((name+"_hit").c_str())){char value[32]{};if(entities->GetKeyValue(relay,"InitialValue",value,sizeof(value))&&(atoi(value)||!strcmp(value,"true"))){entities->SetKeyValue(relay,"InitialValue","0");bridge.Command({{"type","player_hit"},{"target",game}});}}
 }
 for(const auto&game:expired)ClearExtra(game);
}
