// Local HL1 preview renderer using the public Source SDK tool interfaces.
#include <windows.h>
#include <cstdio>
#include <cstring>
#include "toolframework/itooldictionary.h"
#include "toolframework/itoolsystem.h"
#include "toolframework/ienginetool.h"
#include "materialsystem/imaterialsystem.h"
#include "materialsystem/itexture.h"
#include "view_shared.h"
#include "generated_config.h"
static IEngineTool* engineTool=nullptr;
static IMaterialSystem* materials=nullptr;
static bool drawing=false;
static void PreviewLog(const char*text){if(FILE*f=fopen(SOURCEBOX_DIAGNOSTIC_PATH "-preview","a")){fprintf(f,"%s\n",text);fclose(f);}}
#include "hl1-preview-renderer.inc"
class PreviewTool:public IToolSystem{public:
char const *GetToolName() override {return "sourcebox-preview";}
bool	Init() override {return true;}
void	Shutdown() override {}
bool	ServerInit( CreateInterfaceFn serverFactory ) override {return true;}
bool	ClientInit( CreateInterfaceFn clientFactory ) override {previewEntities=(IClientEntityList*)clientFactory(VCLIENTENTITYLIST_INTERFACE_VERSION,nullptr);return true;}
void	ServerShutdown() override {}
void	ClientShutdown() override {}
bool	CanQuit() override {return true;}
void	PostMessage( HTOOLHANDLE hEntity, KeyValues *message ) override {}
void	Think( bool finalTick ) override {}
void	ServerLevelInitPreEntity() override {}
void	ServerLevelInitPostEntity() override {}
void	ServerLevelShutdownPreEntity() override {}
void	ServerLevelShutdownPostEntity() override {}
void	ServerFrameUpdatePreEntityThink() override {}
void	ServerFrameUpdatePostEntityThink() override {}
void	ServerPreClientUpdate() override {}
void	ServerPreSetupVisibility() override {}
const char* GetEntityData( const char *pActualEntityData ) override {return pActualEntityData;}
void	ClientLevelInitPreEntity() override {}
void	ClientLevelInitPostEntity() override {}
void	ClientLevelShutdownPreEntity() override {}
void	ClientLevelShutdownPostEntity() override {}
void ClientPreRender() override {RenderPortalPreviews();}
void	ClientPostRender() override {CompositePortalPreviews();}
void	AdjustEngineViewport( int& x, int& y, int& width, int& height ) override {}
bool	SetupEngineView( Vector &origin, QAngle &angles, float &fov ) override {return false;}
bool	SetupAudioState( AudioState_t &audioState ) override {return false;}
bool	ShouldGameRenderView() override {return true;}
bool	IsThirdPersonCamera() override {return drawing;}
bool	IsToolRecording() override {return false;}
IMaterialProxy *LookupProxy( const char *proxyName ) override {return nullptr;}
void	OnToolActivate() override {}
void	OnToolDeactivate() override {}
bool	TrapKey( ButtonCode_t key, bool down ) override {return false;}
bool	GetSoundSpatialization( int iUserData, int guid, SpatializationInfo_t& info ) override {return false;}
void		RenderFrameBegin() override {if(engineTool&&!engineTool->IsTopmostTool(this))engineTool->SwitchToTool(0);}
void		RenderFrameEnd() override {}
void		HostRunFrameBegin() override {}
void		HostRunFrameEnd() override {}
void		VGui_PreRender( int paintMode ) override {}
void		VGui_PostRender( int paintMode ) override {}
void		VGui_PreSimulate() override {}
void		VGui_PostSimulate() override {}
};static PreviewTool tool;
class Dictionary:public IToolDictionary{public:
 bool Connect(CreateInterfaceFn factory)override{engineTool=(IEngineTool*)factory(VENGINETOOL_INTERFACE_VERSION,nullptr);materials=(IMaterialSystem*)factory(MATERIAL_SYSTEM_INTERFACE_VERSION,nullptr);renderView=(IVRenderView*)factory(VENGINE_RENDERVIEW_INTERFACE_VERSION,nullptr);previewEngine=(IVEngineClient*)factory(VENGINE_CLIENT_INTERFACE_VERSION,nullptr);char msg[160];snprintf(msg,sizeof(msg),"Tool connected engine=%p materials=%p",engineTool,materials);PreviewLog(msg);return engineTool&&materials&&renderView&&previewEngine;}
 void Disconnect()override{engineTool=nullptr;materials=nullptr;}
 void*QueryInterface(const char*)override{return nullptr;}
 InitReturnVal_t Init()override{MathLib_Init();PreviewLog("Tool dictionary initialized");return INIT_OK;}
 void Shutdown()override{}
 void CreateTools()override{PreviewLog("Tool registered");}
 int GetToolCount()const override{return 1;}
 IToolSystem*GetTool(int index)override{return index==0?&tool:nullptr;}
};static Dictionary dictionary;
extern "C" __declspec(dllexport) void*CreateInterface(const char*name,int*result){if(!strcmp(name,VTOOLDICTIONARY_INTERFACE_VERSION)){if(result)*result=0;return &dictionary;}if(result)*result=1;return nullptr;}

