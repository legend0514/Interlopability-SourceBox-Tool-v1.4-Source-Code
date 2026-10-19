#pragma once
// ABI prefix of Valve IServerTools001, verified against AlliedModders hl2sdk/portal2
// public/toolframework/itoolentity.h. Never cast to SDK2013 IServerTools003.
class PortalTools : public IBaseInterface {
public:
 virtual void* GetIServerEntity(void*)=0;
 virtual bool SnapPlayerToPosition(const Vector&,const QAngle&,void* =nullptr)=0;
 virtual bool GetPlayerPosition(Vector&,QAngle&,void* =nullptr)=0;
 virtual bool SetPlayerFOV(int,void* =nullptr)=0;
 virtual int GetPlayerFOV(void* =nullptr)=0;
 virtual bool IsInNoClipMode(void* =nullptr)=0;
 virtual void* FirstEntity()=0;
 virtual void* NextEntity(void*)=0;
 virtual void* FindEntityByHammerID(int)=0;
 virtual bool GetKeyValue(void*,const char*,char*,int)=0;
 virtual bool SetKeyValue(void*,const char*,const char*)=0;
 virtual bool SetKeyValue(void*,const char*,float)=0;
 virtual bool SetKeyValue(void*,const char*,const Vector&)=0;
 virtual void* CreateEntityByName(const char*)=0;
 virtual void DispatchSpawn(void*)=0;
};
