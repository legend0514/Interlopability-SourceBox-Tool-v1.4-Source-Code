// Use Portal 2's installed native portal renderer and traversal system.
// The core remains the only placement authority; the adapter owns native movement.
std::map<std::string,std::string> nativePortalPlacements;
void PortalMessage(const Json&m){
 const auto type=m.value("type",std::string());
 if(type!="portal_snapshot"){LegacyPortalMessage(m);return;}
 if(m.value("session",std::string())!=portalConnection)return;
 const auto revision=m.value("revision",-1LL);const int count=m.value("count",0),index=m.value("index",-1);
 if(revision<=portalRevision||revision<portalPendingRevision||count<1||count>6||index<0||index>=count)return;
 if(revision>portalPendingRevision){portalParts.clear();portalPendingRevision=revision;portalPartCount=count;}if(count!=portalPartCount)return;
 portalParts[index]=m["pairs"];if(portalParts.size()!=size_t(count))return;
 std::vector<std::string>keep;
 const char*owners[]={"hl2","tf2","hl1mp","gmod","cs2","portal2"};
 for(const auto&part:portalParts)for(const auto&pair:part.second){
  int group=-1;for(int i=0;i<6;i++)if(pair["owner"]==owners[i])group=i+8;if(group<0)continue;
  for(const auto&end:pair["endpoints"]){if(end.is_null())continue;
   const int endpoint=end["endpoint"].get<int>();const auto name="sourcebox_portal_"+pair["owner"].get<std::string>()+"_"+std::to_string(endpoint);
   auto*entity=Named(name);const bool created=!entity;
   if(created){entity=tools->CreateEntityByName("prop_portal");if(!entity){Log(("native portal factory failed: "+name).c_str());continue;}tools->SetKeyValue(entity,"targetname",name.c_str());tools->SetKeyValue(entity,"LinkageGroupID",std::to_string(group).c_str());tools->SetKeyValue(entity,"PortalTwo",endpoint?"1":"0");tools->SetKeyValue(entity,"Activated","0");}
   char origin[160],angles[160],location[350];
   snprintf(origin,sizeof(origin),"%.4f %.4f %.4f",double(end["position"][0]),double(end["position"][1]),double(end["position"][2]));
   snprintf(angles,sizeof(angles),"%.4f %.4f 0",-asin(double(end["normal"][2]))*180/3.141592653589793,atan2(double(end["normal"][1]),double(end["normal"][0]))*180/3.141592653589793);
   if(created){tools->SetKeyValue(entity,"origin",origin);tools->SetKeyValue(entity,"angles",angles);tools->DispatchSpawn(entity);}
   snprintf(location,sizeof(location),"\"%s %s\"",origin,angles);
   // A snapshot includes other players' unchanged portals too. Replaying their
   // activation restarts effects and audio whenever anyone places a portal.
   if(created||nativePortalPlacements[name]!=location){
    Input(name,"NewLocation",location);Input(name,"Resize","\"32 64\"");Input(name,"SetActivatedState","1");
    nativePortalPlacements[name]=location;
    Log(("native portal ready: "+name+" origin="+origin+" angles="+angles).c_str());
   }
   keep.push_back(name);
  }
 }
 for(const auto&name:portalMarkers)if(std::find(keep.begin(),keep.end(),name)==keep.end()){PortalDelete(name);nativePortalPlacements.erase(name);}
 portalMarkers.swap(keep);portalRevision=revision;portalParts.clear();Log("native canonical portal snapshot applied");
}

