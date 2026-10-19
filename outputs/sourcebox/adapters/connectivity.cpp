// Phase A only. Uses Valve's public IServerPluginCallbacks, not executable patches.
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <atomic>
#include <thread>
#include <cstdio>
#include <cstring>
#include "engine/iserverplugin.h"
#include "generated_config.h"

class SourceboxAdapter final : public IServerPluginCallbacks {
 std::atomic<bool> running{false};
 std::thread worker;
 void Run(){
  WSADATA data;if(WSAStartup(MAKEWORD(2,2),&data))return;
  while(running){
   SOCKET s=socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);
   if(s==INVALID_SOCKET)break;
   DWORD timeout=500;setsockopt(s,SOL_SOCKET,SO_RCVTIMEO,(char*)&timeout,sizeof(timeout));setsockopt(s,SOL_SOCKET,SO_SNDTIMEO,(char*)&timeout,sizeof(timeout));
   sockaddr_in addr{};addr.sin_family=AF_INET;addr.sin_port=htons(SOURCEBOX_PORT);inet_pton(AF_INET,"127.0.0.1",&addr.sin_addr);
   if(connect(s,(sockaddr*)&addr,sizeof(addr))==0){
    char line[512];unsigned long long seq=0;
    int n=snprintf(line,sizeof(line),"{\"type\":\"hello\",\"protocol\":1,\"game\":\"%s\",\"pid\":%lu}\n",SOURCEBOX_GAME,GetCurrentProcessId());
    bool ok=send(s,line,n,0)==n;
    while(running&&ok){
     // Drain server replies; closed connection triggers reconnect. No engine calls on this thread.
     char reply[4096];int got=recv(s,reply,sizeof(reply),0);
     if(got<=0){ok=false;break;}
     for(int i=0;i<SOURCEBOX_HEARTBEAT_MS/50&&running;++i)Sleep(50);
     n=snprintf(line,sizeof(line),"{\"type\":\"heartbeat\",\"seq\":%llu,\"timestamp\":%llu}\n",++seq,GetTickCount64());
     ok=send(s,line,n,0)==n;
    }
   }
   closesocket(s);
   for(int i=0;i<20&&running;++i)Sleep(50);
  }
  WSACleanup();
 }
public:
 bool Load(CreateInterfaceFn,CreateInterfaceFn) override {running=true;worker=std::thread(&SourceboxAdapter::Run,this);return true;}
 void Unload() override {running=false;if(worker.joinable())worker.join();}
 void Pause() override {} void UnPause() override {}
 const char* GetPluginDescription() override {return "Sourcebox Phase A connectivity probe";}
 void LevelInit(const char*) override {} void ServerActivate(edict_t*,int,int) override {}
 void GameFrame(bool) override {} void LevelShutdown() override {}
 void ClientActive(edict_t*) override {} void ClientDisconnect(edict_t*) override {}
 void ClientPutInServer(edict_t*,const char*) override {} void SetCommandClient(int) override {}
 void ClientSettingsChanged(edict_t*) override {}
 PLUGIN_RESULT ClientConnect(bool*,edict_t*,const char*,const char*,char*,int) override {return PLUGIN_CONTINUE;}
 PLUGIN_RESULT ClientCommand(edict_t*,const CCommand&) override {return PLUGIN_CONTINUE;}
 PLUGIN_RESULT NetworkIDValidated(const char*,const char*) override {return PLUGIN_CONTINUE;}
 void OnQueryCvarValueFinished(QueryCvarCookie_t,edict_t*,EQueryCvarValueStatus,const char*,const char*) override {}
 void OnEdictAllocated(edict_t*) override {} void OnEdictFreed(const edict_t*) override {}
};
static SourceboxAdapter adapter;
extern "C" __declspec(dllexport) void* CreateInterface(const char* name,int* result){
 if(!strcmp(name,INTERFACEVERSION_ISERVERPLUGINCALLBACKS)||!strcmp(name,INTERFACEVERSION_ISERVERPLUGINCALLBACKS_VERSION_2)){if(result)*result=0;return &adapter;}
 if(result)*result=1;return nullptr;
}
