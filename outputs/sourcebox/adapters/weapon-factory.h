#pragma once
// ABI declarations from Valve Source SDK 2013 game/server/util.h, exposed by
// IServerTools::GetEntityFactoryDictionary. No offsets or executable patches.
class IEntityFactory;
class IEntityFactoryDictionary {
public:
 virtual void InstallFactory(IEntityFactory*,const char*)=0;
 virtual IServerNetworkable* Create(const char*)=0;
 virtual void Destroy(const char*,IServerNetworkable*)=0;
 virtual IEntityFactory* FindFactory(const char*)=0;
 virtual const char* GetCannonicalName(const char*)=0;
};
class IEntityFactory {
public:
 virtual IServerNetworkable* Create(const char*)=0;
 virtual void Destroy(IServerNetworkable*)=0;
 virtual size_t GetEntitySize()=0;
};
