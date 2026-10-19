// Third player has independent identity, lifetime, and interpolation state.
Json gmodPose=nullptr;std::string gmodSession;unsigned long long gmodSequence=0,gmodReceived=0;int gmodAnimation=-1;std::string gmodModel,gmodClip,gmodWeapon;
void ClearGmod(){if(auto*h=Named("sourcebox_gmod_hit"))entities->RemoveEntityImmediate(h);if(auto*w=Named("sourcebox_gmod_weapon"))entities->RemoveEntityImmediate(w);gmodWeapon.clear();if(auto*e=Named("sourcebox_gmod_player"))entities->RemoveEntityImmediate(e);if(auto*e=Named("sourcebox_gmod_label"))entities->RemoveEntityImmediate(e);gmodPose=nullptr;gmodSequence=0;gmodSession.clear();gmodAnimation=-1;gmodModel.clear();gmodClip.clear();}
void GmodMessage(const Json&j,unsigned long long now){
 if(j.value("type","")=="remove"){ClearGmod();return;}
 if(j.value("type","")!="remote")return;
 auto session=j.value("session","");if(session!=gmodSession){ClearGmod();gmodSession=session;}
 auto pose=j["state"];if(pose.value("seq",0ULL)<=gmodSequence)return;
 gmodSequence=pose.value("seq",0ULL);gmodPose=pose;gmodReceived=now;
}
void TickGmod(unsigned long long now){
 if(!gmodPose.is_object())return;if(now-gmodReceived>2000){ClearGmod();return;}
 auto*e=Named("sourcebox_gmod_player");bool created=false;
 auto desired=gmodPose.value("model",std::string("models/sourcebox/hl2/player.mdl"));
 if(e&&desired!=gmodModel){entities->RemoveEntityImmediate(e);e=nullptr;gmodAnimation=-1;gmodClip.clear();}
 gmodModel=desired;
 if(!e){if(!Named("sourcebox_gmod_hit")){auto*h=entities->CreateEntityByName("logic_branch");if(h){entities->SetKeyValue(h,"targetname","sourcebox_gmod_hit");entities->DispatchSpawn(h);}}e=entities->CreateEntityByName("prop_dynamic_override");if(!e)return;entities->SetKeyValue(e,"targetname","sourcebox_gmod_player");entities->SetKeyValue(e,"model",desired.c_str());entities->SetKeyValue(e,"solid","2");entities->SetKeyValue(e,"DisableBoneFollowers","1");entities->SetKeyValue(e,"rendercolor",gmodPose.contains("model")?"255 255 255":"80 255 120");entities->SetKeyValue(e,"OnTakeDamage","sourcebox_gmod_hit,SetValue,1,0,-1");entities->DispatchSpawn(e);Input("sourcebox_gmod_player","EnableCollision","");gmodAnimation=-1;gmodClip.clear();created=true;}
 if(auto*h=Named("sourcebox_gmod_hit")){char v[32]{};if(entities->GetKeyValue(h,"InitialValue",v,sizeof(v))&&(atoi(v)||!strcmp(v,"true"))){entities->SetKeyValue(h,"InitialValue","0");bridge.Command({{"type","player_hit"},{"target","gmod"}});}}
 auto old=ReadVector(e,"origin");char text[128];double p[3];for(int i=0;i<3;i++)p[i]=created?double(gmodPose["position"][i]):double(old[i])+(double(gmodPose["position"][i])-double(old[i]))*0.25;
 snprintf(text,sizeof(text),"%.4f %.4f %.4f",p[0],p[1],p[2]);entities->SetKeyValue(e,"origin",text);snprintf(text,sizeof(text),"0 %.4f 0",double(gmodPose["rotation"][1]));entities->SetKeyValue(e,"angles",text);
 double vx=gmodPose["velocity"][0],vy=gmodPose["velocity"][1];int moving=vx*vx+vy*vy>64?1:0;
 auto clip=gmodPose.value("sequence",std::string(moving?gordon.run:gordon.idle));if(!clip.empty()&&gmodClip!=clip){Input("sourcebox_gmod_player","SetAnimation",clip.c_str());gmodClip=clip;gmodAnimation=moving;}
 std::string weaponModel;auto held=gmodPose.value("weapon",std::string());for(const auto&def:hl2WeaponModels)if(held==def.name)weaponModel=def.model;
 auto*w=Named("sourcebox_gmod_weapon");if(w&&(created||gmodWeapon!=weaponModel)){entities->RemoveEntityImmediate(w);w=nullptr;}gmodWeapon=weaponModel;
 if(!w&&!weaponModel.empty()){w=entities->CreateEntityByName("prop_dynamic_override");if(w){entities->SetKeyValue(w,"targetname","sourcebox_gmod_weapon");entities->SetKeyValue(w,"model",weaponModel.c_str());entities->SetKeyValue(w,"solid","0");entities->DispatchSpawn(w);Input("sourcebox_gmod_weapon","SetParent","sourcebox_gmod_player");Input("sourcebox_gmod_weapon","SetParentAttachment","anim_attachment_RH");}}
 auto*l=Named("sourcebox_gmod_label");if(!l&&worldTextSupported){l=entities->CreateEntityByName("point_worldtext");if(l){entities->SetKeyValue(l,"targetname","sourcebox_gmod_label");entities->SetKeyValue(l,"message","GMOD PLAYER");entities->SetKeyValue(l,"textsize","6");entities->SetKeyValue(l,"color","80 255 120 255");entities->DispatchSpawn(l);}}
 if(l){snprintf(text,sizeof(text),"%.4f %.4f %.4f",p[0],p[1],p[2]+88);entities->SetKeyValue(l,"origin",text);}
}
