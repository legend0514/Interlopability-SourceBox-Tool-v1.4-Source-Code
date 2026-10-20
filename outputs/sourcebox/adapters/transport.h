#pragma once
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <atomic>
#include <thread>
#include <mutex>
#include <deque>
#include <cstdio>
#include "vendor/json.hpp"
#include "generated_config.h"
using Json=nlohmann::json;
// No Source interfaces are accessed by this network worker.
class Bridge {
 std::atomic<bool> running{false};std::thread worker;std::mutex lock;
 std::atomic<bool> controlOverflow{false};
 Json local,props,cubeState,lifecycle;std::deque<Json> incoming,outgoing,portalIncoming,controlIncoming;
 std::string portalSession;bool portalTraversal=false;
 void Push(Json j){std::lock_guard<std::mutex>g(lock);const auto type=j.value("type",std::string());
  // Connection state cannot be evicted by movement while a game is loading.
  if(type=="welcome"||type=="portal_lost"){lifecycle=std::move(j);portalIncoming.clear();controlIncoming.clear();return;}
  // Player snapshots are disposable; hits and ownership transitions are not.
  // Keep control traffic separate so loading another model cannot evict it.
  if(type=="player_ragdoll"||type=="player_damage"||type=="cube_owner"||type=="cube_freeze"||type=="cube_lost"||type=="cube_denied"){
   if(controlIncoming.size()<256)controlIncoming.push_back(std::move(j));else controlOverflow=true;return;
  }
  if(type.find("portal_")==0){if(portalIncoming.size()==32)portalIncoming.pop_front();portalIncoming.push_back(std::move(j));return;}
  if(incoming.size()==32)incoming.pop_front();incoming.push_back(std::move(j));}
 bool Send(SOCKET s,const Json&j){auto t=j.dump()+"\n";size_t i=0;while(i<t.size()){int n=send(s,t.data()+i,int(t.size()-i),0);if(n<=0)return false;i+=n;}return true;}
 void Run(){
  WSADATA w;if(WSAStartup(MAKEWORD(2,2),&w))return;
  while(running){
   SOCKET s=socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);if(s==INVALID_SOCKET)break;
   DWORD timeout=500;setsockopt(s,SOL_SOCKET,SO_SNDTIMEO,(char*)&timeout,sizeof(timeout));
   BOOL nd=TRUE;setsockopt(s,IPPROTO_TCP,TCP_NODELAY,(char*)&nd,sizeof(nd));
   sockaddr_in a{};a.sin_family=AF_INET;a.sin_port=htons(SOURCEBOX_PORT);inet_pton(AF_INET,"127.0.0.1",&a.sin_addr);
   if(connect(s,(sockaddr*)&a,sizeof(a))==0){
    bool ok=Send(s,{{"type","hello"},{"protocol",1},{"cubeProtocol",1},{"portalProtocol",1},{"portalTraverse",portalTraversal},{"game",SOURCEBOX_GAME},{"pid",GetCurrentProcessId()}}),welcomed=false;
    unsigned long long seq=0,lastSent=0,lastReceived=GetTickCount64();std::string buffer;
    while(running&&ok){
     if(controlOverflow.exchange(false)){if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH,"a")){fprintf(f,"control queue overflow: reconnecting %s\n",SOURCEBOX_GAME);fclose(f);}break;}
     auto now=GetTickCount64();if(now-lastReceived>5000)break;
     if(welcomed&&now-lastSent>=SOURCEBOX_UPDATE_MS){
      std::deque<Json>commands;Json cube;{std::lock_guard<std::mutex>g(lock);commands.swap(outgoing);cube=cubeState;}
      for(auto&command:commands){command["seq"]=++seq;command["timestamp"]=now;if(!Send(s,command)){ok=false;break;}}
      if(!ok)break;
      if(cube.is_object()){cube["seq"]=++seq;cube["timestamp"]=now;if(!Send(s,cube))break;lastSent=now;}
      Json j,physics;{std::lock_guard<std::mutex>g(lock);j=local;physics=props;}
      if(!j.is_object()&&physics.is_object())j={{"type","heartbeat"},{"timestamp",physics["timestamp"]}};
      if(j.is_object()&&physics.is_object())j["props"]=physics["props"];
      if(j.is_object()){if(!j.contains("type"))j["type"]="state";j["seq"]=++seq;ok=Send(s,j);lastSent=now;}
      else if(now-lastSent>=1000){ok=Send(s,{{"type","heartbeat"},{"seq",++seq},{"timestamp",now}});lastSent=now;}
     }
     fd_set set;FD_ZERO(&set);FD_SET(s,&set);timeval tv{0,10000};int ready=select(0,&set,nullptr,nullptr,&tv);
     if(ready<0)break;if(!ready)continue;
     char buf[4096];int n=recv(s,buf,sizeof(buf),0);if(n<=0)break;
     lastReceived=GetTickCount64();buffer.append(buf,n);
     size_t end;while((end=buffer.find('\n'))!=std::string::npos){
      if(end>4096){ok=false;break;}
      auto j=Json::parse(buffer.substr(0,end),nullptr,false);buffer.erase(0,end+1);
      if(j.is_discarded()||!j.is_object()){ok=false;break;}
      if(j.value("type","")=="welcome"){welcomed=true;{std::lock_guard<std::mutex>g(lock);portalSession=j.value("session",std::string());}Push(j);}
      if(j.value("type",std::string()).find("portal_")==0)Push(j);
      if(j.value("type","")=="scene"||j.value("type","")=="remote"||j.value("type","")=="remove"||j.value("type","")=="physics"||j.value("type","")=="player_ragdoll"||j.value("type","")=="player_damage"||j.value("type",std::string()).find("cube_")==0)Push(j);
     }
     if(buffer.size()>4096)break;
    }
   }
   closesocket(s);{std::lock_guard<std::mutex>g(lock);outgoing.clear();cubeState=nullptr;portalSession.clear();}Push({{"type","portal_lost"}});Push({{"type","cube_lost"}});Push({{"type","remove"}});
   for(int i=0;i<20&&running;++i)Sleep(50);
  }WSACleanup();
 }
public:
 void PortalCapability(bool traverse){portalTraversal=traverse;}
 void Start(){if(running.exchange(true))return;worker=std::thread(&Bridge::Run,this);}
 void Stop(){running=false;if(worker.joinable())worker.join();}
 void Publish(Json j){std::lock_guard<std::mutex>g(lock);local=std::move(j);}
 void PublishProps(Json j){std::lock_guard<std::mutex>g(lock);props=std::move(j);}
 void PublishCube(Json j){std::lock_guard<std::mutex>g(lock);cubeState=std::move(j);}
 void Command(Json j){std::lock_guard<std::mutex>g(lock);if(j.value("type",std::string()).find("portal_")==0){if(portalSession.empty())return;j["session"]=portalSession;}if(outgoing.size()<16)outgoing.push_back(std::move(j));}
 std::deque<Json> Drain(){std::lock_guard<std::mutex>g(lock);std::deque<Json>out;
  if(lifecycle.is_object()){out.push_back(std::move(lifecycle));lifecycle=nullptr;}
  while(!portalIncoming.empty()){out.push_back(std::move(portalIncoming.front()));portalIncoming.pop_front();}
  while(!controlIncoming.empty()){out.push_back(std::move(controlIncoming.front()));controlIncoming.pop_front();}
  while(!incoming.empty()){out.push_back(std::move(incoming.front()));incoming.pop_front();}return out;}
};
