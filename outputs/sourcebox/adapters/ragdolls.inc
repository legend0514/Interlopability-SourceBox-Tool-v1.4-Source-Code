// Cosmetic bodies are local engine physics, never shared interactive props.
// Resolve names each time: round cleanup can invalidate entity pointers.
std::deque<std::string> deathBodies;
std::deque<std::string> deathEvents;
unsigned long long deathBodySerial=0;
void DeathMessage(const Json& message){
 const auto event=message.value("event",std::string());
 if(event.empty()||std::find(deathEvents.begin(),deathEvents.end(),event)!=deathEvents.end())return;
 deathEvents.push_back(event);while(deathEvents.size()>128)deathEvents.pop_front();
 auto game=message.value("game",std::string());const auto&s=message["state"];
 auto*source=Named(("sourcebox_peer_"+game).c_str());
#ifdef SOURCEBOX_PORTAL2_RAGDOLLS
 if(game=="portal2")source=Local();
#else
 if(game==SOURCEBOX_GAME&&player&&player->GetNetworkable())source=player->GetNetworkable()->GetBaseEntity();
 else if(game=="gmod")source=Named("sourcebox_gmod_player");
 else if(game==(!strcmp(SOURCEBOX_GAME,"hl2")?"tf2":"hl2")&&strcmp(SOURCEBOX_GAME,"hl1mp"))source=Named(ProxyName());
#endif
 char model[256]{},skin[32]{};
 if(!source||!entities->GetKeyValue(source,"model",model,sizeof(model))||!model[0])return;
 // Optional locally prepared models preserve the installed mesh and add joints.
 if(game=="hl2"||game=="portal2"){
  const char*alias=game=="hl2"?"models/sourcebox/ragdolls/hl2.mdl":"models/sourcebox/ragdolls/chell.mdl";
  auto module=GetModuleHandleA("filesystem_stdio.dll");
  auto factory=module?reinterpret_cast<CreateInterfaceFn>(GetProcAddress(module,"CreateInterface")):nullptr;
  auto*files=factory?static_cast<IFileSystem*>(factory(FILESYSTEM_INTERFACE_VERSION,nullptr)):nullptr;
  const auto physics=std::string(alias).substr(0,strlen(alias)-4)+".phy";
  if(files&&files->FileExists(alias,"GAME")&&files->FileExists(physics.c_str(),"GAME"))snprintf(model,sizeof(model),"%s",alias);
 }
 entities->GetKeyValue(source,"skin",skin,sizeof(skin));
 auto*body=entities->CreateEntityByName("prop_ragdoll");if(!body)return;
 const auto name="sourcebox_death_body_"+std::to_string(++deathBodySerial);
 entities->SetKeyValue(body,"targetname",name.c_str());entities->SetKeyValue(body,"model",model);entities->SetKeyValue(body,"skin",skin);
 char value[128];snprintf(value,sizeof(value),"%.4f %.4f %.4f",double(s["position"][0]),double(s["position"][1]),double(s["position"][2]));entities->SetKeyValue(body,"origin",value);
 snprintf(value,sizeof(value),"0 %.4f 0",double(s["rotation"][1]));entities->SetKeyValue(body,"angles",value);
 entities->SetKeyValue(body,"collisiongroup","2");entities->DispatchSpawn(body);
 // Engine-owned timer avoids keeping a raw pointer to a potentially deleted body.
 auto*timer=entities->CreateEntityByName("logic_timer");if(timer){entities->SetKeyValue(timer,"RefireTime",std::to_string(message.value("lifetimeMs",12000)/1000.0).c_str());entities->SetKeyValue(timer,"StartDisabled","0");entities->SetKeyValue(timer,"OnTimer",(name+",Kill,,0,1").c_str());entities->SetKeyValue(timer,"OnTimer","!self,Kill,,0,1");entities->DispatchSpawn(timer);}
 deathBodies.push_back(name);while(deathBodies.size()>size_t(message.value("maxBodies",6))){Input(deathBodies.front().c_str(),"Kill","");deathBodies.pop_front();}
}
