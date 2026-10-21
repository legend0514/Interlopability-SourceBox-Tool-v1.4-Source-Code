// Native SDK portal views. Render the linked camera into a texture, then composite
// it through a depth-tested stencil aperture. Screen-space sampling preserves perspective.
#include "materialsystem/imesh.h"
#include "tier1/KeyValues.h"
#include "igamesystem.h"
#ifdef TF_CLIENT_DLL
#include "game/client/iviewport.h"
#include "tf/vgui/tf_classmenu.h"
#endif
#ifdef TF_CLIENT_DLL
// Explicit acceptance fixture: wait for a real loaded client, never run on menu frames.
static ConVar sbClassProbe("sourcebox_class_probe","0",FCVAR_CHEAT,"Run two native nine-class selection/respawn passes.");
class CSourceboxClassProbe : public CAutoGameSystemPerFrame {
 float next;int stage;
public:
 CSourceboxClassProbe():CAutoGameSystemPerFrame("SourceboxClassProbe"),next(0),stage(0){}
 void Update(float) OVERRIDE {
  if(!sbClassProbe.GetBool()||!engine->IsInGame()||!C_BasePlayer::GetLocalPlayer()||gpGlobals->curtime<next)return;
  static const char*classes[]={"scout","soldier","pyro","demoman","heavyweapons","engineer","medic","sniper","spy"};
  if(stage==0){engine->ClientCmd_Unrestricted("mp_disable_respawn_times 1; mp_respawnwavetime 0; jointeam red");stage++;next=gpGlobals->curtime+5;return;}
  if(stage>18){Msg("[Sourcebox class test] COMPLETE alive=%d\n",C_BasePlayer::GetLocalPlayer()->IsAlive());sbClassProbe.SetValue(0);return;}
  char command[256];Q_snprintf(command,sizeof(command),"joinclass %s; kill",classes[(stage-1)%9]);
  Msg("[Sourcebox class test] step=%d class=%s previousAlive=%d\n",stage,classes[(stage-1)%9],C_BasePlayer::GetLocalPlayer()->IsAlive());
  if(sbClassProbe.GetInt()==2 && gViewPortInterface){
   static const int classIds[]={1,3,7,4,6,9,5,2,8};
   CTFClassMenu*menu=static_cast<CTFClassMenu*>(gViewPortInterface->FindPanelByName(PANEL_CLASS_RED));
   if(!menu){Warning("[Sourcebox class test] FAIL missing class menu\n");sbClassProbe.SetValue(0);return;}
   Q_snprintf(command,sizeof(command),"select%d",classIds[(stage-1)%9]);
   menu->OnCommand(command);
   engine->ClientCmd_Unrestricted("kill");
  }else engine->ClientCmd_Unrestricted(command);
  stage++;next=gpGlobals->curtime+12;
 }
};
static CSourceboxClassProbe g_SourceboxClassProbe;
#endif
static ConVar sbProbe("sourcebox_portal_probe","0",FCVAR_CHEAT,"Run the explicit local Sourcebox portal gameplay fixture once.");
class CSourceboxPortalProbe : public CAutoGameSystemPerFrame
{
 float start;int stage;
public:
 CSourceboxPortalProbe():CAutoGameSystemPerFrame("SourceboxPortalProbe"),start(-1),stage(0){}
 void Update(float) OVERRIDE {
  if(!sbProbe.GetBool()||!engine->IsInGame())return;
  C_BasePlayer*p=C_BasePlayer::GetLocalPlayer();if(!p)return;
#ifdef TF_CLIENT_DLL
  if(!p->IsAlive()){static float retry=0;if(gpGlobals->curtime>=retry){engine->ClientCmd_Unrestricted("jointeam red; joinclass soldier");retry=gpGlobals->curtime+3;}return;}
#else
  if(!p->IsAlive())return;
#endif
  if(start<0)start=gpGlobals->curtime;
  float t=gpGlobals->curtime-start;
  const float times[]={2,3,4,4.2f,5,6,6.2f,7,9,10,12,13};
  if(stage>=ARRAYSIZE(times)||t<times[stage])return;
#ifdef TF_CLIENT_DLL
  if(stage==0 && gViewPortInterface){
   gViewPortInterface->ShowPanel(PANEL_ALL,false);
   engine->ClientCmd_Unrestricted("gameui_hide");
  }
#endif
  const char*commands[]={
#ifdef TF_CLIENT_DLL
   "hud_fastswitch 1; use tf_weapon_grapplinghook",
#else
   "use weapon_sourcebox_portalgun",
#endif
#ifdef TF_CLIENT_DLL
   "setpos 0 320 1; setang 0 0 0","+attack","-attack","setang 0 180 0","+attack2","-attack2",
   "setpos 350 320 1; setang 0 0 0","jpeg sourcebox_portal_before","+forward","-forward","jpeg sourcebox_portal_after"
#else
   "setpos 0 -320 1; setang 0 0 0","+attack","-attack","setang 0 180 0","+attack2","-attack2",
   "setpos 350 -320 1; setang 0 0 0","jpeg sourcebox_portal_before","+forward","-forward","jpeg sourcebox_portal_after"
#endif
  };
  const char*command=commands[stage];
  // Mode 2 measures actual native falling momentum; mode 1 retains the
  // existing walking fixture. No fixture runs in an ordinary launch.
  if(sbProbe.GetInt()==2){
#ifdef TF_CLIENT_DLL
   if(stage==1)command="noclip; setpos 0 320 128; setang 90 0 0";
   if(stage==4)command="setang 0 0 0";
   if(stage==7)command="setpos 0 320 150; setang 0 0 0; noclip";
#else
   if(stage==1)command="noclip; setpos 0 -320 128; setang 90 0 0";
   if(stage==4)command="setang 0 0 0";
   if(stage==7)command="setpos 0 -320 150; setang 0 0 0; noclip";
#endif
   if(stage==9||stage==10)command="-forward; -attack; -attack2";
  }
  engine->ClientCmd_Unrestricted(command);
  if(auto*w=p->GetActiveWeapon()){
   auto*vm=p->GetViewModel();
   Msg("[Sourcebox weapon probe] weapon=%s desired=%s actual=%s sequence=%d\n",w->GetClassname(),w->GetViewModel(),vm&&vm->GetModel()?modelinfo->GetModelName(vm->GetModel()):"none",vm?vm->GetSequence():-1);
  }
  Msg("[Sourcebox portal probe] stage=%d position=%.3f %.3f %.3f\n",stage,p->GetAbsOrigin().x,p->GetAbsOrigin().y,p->GetAbsOrigin().z);
  if(++stage==ARRAYSIZE(times)){sbProbe.SetValue(0);start=-1;stage=0;}
 }
};
static CSourceboxPortalProbe sbPortalProbe;
static bool sbRenderingPortal = false;
static ViewCustomVisibility_t *sbPortalVisibility = NULL;
class C_SourceboxPortal;
static CUtlVector<C_SourceboxPortal*> sbPortals;
class C_SourceboxPortal : public C_BaseAnimating
{
public:
 DECLARE_CLASS(C_SourceboxPortal, C_BaseAnimating);
 DECLARE_CLIENTCLASS();
 int m_iPair, m_iEndpoint;
 CTextureReference target;
 CMaterialReference screen;
 bool ready;
 C_SourceboxPortal() : m_iPair(-1),m_iEndpoint(-1),ready(false) {sbPortals.AddToTail(this);}
 ~C_SourceboxPortal(){sbPortals.FindAndRemove(this);}
 bool ShouldDraw() OVERRIDE {return !sbRenderingPortal && BaseClass::ShouldDraw();}
 C_SourceboxPortal *Other(){FOR_EACH_VEC(sbPortals,i){auto*p=sbPortals[i];if(p!=this&&p->m_iPair==m_iPair&&p->m_iEndpoint!=m_iEndpoint)return p;}return NULL;}
 void Basis(Vector&n,Vector&r,Vector&u){AngleVectors(GetAbsAngles(),&n,&r,&u);}
 Vector Through(const Vector&v,C_SourceboxPortal*other){Vector n,r,u,N,R,U;Basis(n,r,u);other->Basis(N,R,U);return -N*DotProduct(v,n)-R*DotProduct(v,r)+U*DotProduct(v,u);}
 void EnsureTarget(){
  if(target.IsValid())return;
  char name[80];Q_snprintf(name,sizeof(name),"_rt_sourcebox_%d_%d",m_iPair,m_iEndpoint);
  target.InitRenderTarget(1024,1024,RT_SIZE_NO_CHANGE,IMAGE_FORMAT_RGB888,MATERIAL_RT_DEPTH_SEPARATE,false,name);
  KeyValues*k=new KeyValues("UnlitGeneric");k->SetString("$basetexture",name);k->SetInt("$ignorez",1);k->SetInt("$nofog",1);
  char material[80];Q_snprintf(material,sizeof(material),"sourcebox/screen_%d_%d",m_iPair,m_iEndpoint);
  screen.Init(materials->CreateMaterial(material,k));
 }
};
IMPLEMENT_CLIENTCLASS_DT(C_SourceboxPortal,DT_SourceboxPortal,CSourceboxPortal)
 RecvPropInt(RECVINFO(m_iPair)), RecvPropInt(RECVINFO(m_iEndpoint)),
END_RECV_TABLE()

static void SourceboxRenderViews(CViewRender*renderer,const CViewSetup&mainView)
{
 if(sbRenderingPortal||!engine->IsInGame())return;
 sbRenderingPortal=true;
 FOR_EACH_VEC(sbPortals,i){
  auto*p=sbPortals[i];p->ready=false;auto*other=p->Other();if(!other)continue;
  Vector n,r,u;p->Basis(n,r,u);if(DotProduct(mainView.origin-p->GetAbsOrigin(),n)<=0.5f)continue;
  p->EnsureTarget();if(!p->target.IsValid()||!p->screen.IsValid())continue;
  CViewSetup remote=mainView;remote.x=remote.y=0;remote.width=remote.height=1024;
  remote.m_flAspectRatio=mainView.m_flAspectRatio>0?mainView.m_flAspectRatio:float(mainView.width)/mainView.height;
  remote.origin=other->GetAbsOrigin()+p->Through(mainView.origin-p->GetAbsOrigin(),other);
  Vector f,up;AngleVectors(mainView.angles,&f,NULL,&up);f=p->Through(f,other);up=p->Through(up,other);VectorAngles(f,up,remote.angles);
  remote.zNear=1;remote.m_bDoBloomAndToneMapping=false;
  Vector N,R,U;other->Basis(N,R,U);float plane[4]={N.x,N.y,N.z,DotProduct(N,other->GetAbsOrigin())+0.1f};
  ViewCustomVisibility_t vis;vis.AddVisOrigin(other->GetAbsOrigin()+N*8);sbPortalVisibility=&vis;
  CMatRenderContextPtr ctx(materials);ctx->PushRenderTargetAndViewport(p->target);bool clip=ctx->EnableClipping(true);ctx->PushCustomClipPlane(plane);
  renderer->RenderView(remote,VIEW_CLEAR_COLOR|VIEW_CLEAR_DEPTH,RENDERVIEW_SUPPRESSMONITORRENDERING);
  ctx->PopCustomClipPlane();ctx->EnableClipping(clip);ctx->PopRenderTargetAndViewport();sbPortalVisibility=NULL;p->ready=true;
 }
 sbRenderingPortal=false;
}

static void SourceboxCompositePortals(const CViewSetup&view,VPlane*frustum)
{
 if(sbRenderingPortal||!sbPortals.Count())return;
 static CMaterialReference mask, border;
 if(!mask.IsValid()){
  KeyValues*k=new KeyValues("UnlitGeneric");k->SetString("$basetexture","vgui/white");k->SetInt("$nocull",1);k->SetInt("$nofog",1);mask.Init(materials->CreateMaterial("sourcebox/aperture_mask",k));
 }
 if(!border.IsValid()){
  KeyValues*k=new KeyValues("UnlitGeneric");k->SetString("$basetexture","vgui/white");k->SetInt("$vertexcolor",1);k->SetInt("$nocull",1);k->SetInt("$nofog",1);border.Init(materials->CreateMaterial("sourcebox/aperture_border",k));
 }
 CMatRenderContextPtr ctx(materials);
 FOR_EACH_VEC(sbPortals,i){
  auto*p=sbPortals[i];if(!p->ready||!p->Other())continue;
  ctx->ClearStencilBufferRectangle(view.x,view.y,view.x+view.width,view.y+view.height,0);
  ctx->SetStencilEnable(true);ctx->SetStencilWriteMask(0xFF);ctx->SetStencilTestMask(0xFF);ctx->SetStencilReferenceValue(1);
  ctx->SetStencilCompareFunction(STENCILCOMPARISONFUNCTION_ALWAYS);ctx->SetStencilPassOperation(STENCILOPERATION_REPLACE);ctx->SetStencilFailOperation(STENCILOPERATION_KEEP);ctx->SetStencilZFailOperation(STENCILOPERATION_KEEP);
  ctx->OverrideColorWriteEnable(true,false);ctx->OverrideDepthEnable(true,false);
  render->Push3DView(view,0,NULL,frustum);ctx->Bind(mask);
  IMesh*mesh=ctx->GetDynamicMesh(true,NULL,NULL,mask);CMeshBuilder b;b.Begin(mesh,MATERIAL_TRIANGLES,48);
  Vector n,r,u;p->Basis(n,r,u);Vector center=p->GetAbsOrigin()+n*0.25f;
  for(int j=0;j<48;j++){
   Vector vertices[3]={center,center+r*(32*cosf(j*2*M_PI/48))+u*(64*sinf(j*2*M_PI/48)),center+r*(32*cosf((j+1)*2*M_PI/48))+u*(64*sinf((j+1)*2*M_PI/48))};
   for(int k=0;k<3;k++){b.Position3fv(vertices[k].Base());b.Color4ub(255,255,255,255);b.TexCoord2f(0,0,0);b.AdvanceVertex();}
  }b.End();mesh->Draw();render->PopView(frustum);
  ctx->OverrideColorWriteEnable(false,true);ctx->OverrideDepthEnable(false,true);
  ctx->SetStencilCompareFunction(STENCILCOMPARISONFUNCTION_EQUAL);ctx->SetStencilPassOperation(STENCILOPERATION_KEEP);ctx->SetStencilWriteMask(0);
  ctx->DrawScreenSpaceRectangle(p->screen,view.x,view.y,view.width,view.height,0,0,1023,1023,1024,1024);
  ctx->SetStencilEnable(false);ctx->SetStencilWriteMask(0xFF);
  // Draw a depth-tested rim in the aperture's own basis. It remains visible
  // when a peer has no imported ring model or its model axes differ.
  ctx->OverrideDepthEnable(true,false);render->Push3DView(view,0,NULL,frustum);ctx->Bind(border);
  mesh=ctx->GetDynamicMesh(true,NULL,NULL,border);b.Begin(mesh,MATERIAL_TRIANGLES,96);
  center=p->GetAbsOrigin()+n*0.5f;
  for(int j=0;j<48;j++){
   float a=j*2*M_PI/48,c=(j+1)*2*M_PI/48;
   Vector innerA=center+r*(32*cosf(a))+u*(64*sinf(a)),innerB=center+r*(32*cosf(c))+u*(64*sinf(c));
   Vector outerA=center+r*(34*cosf(a))+u*(66*sinf(a)),outerB=center+r*(34*cosf(c))+u*(66*sinf(c));
   Vector vertices[6]={innerA,outerA,outerB,innerA,outerB,innerB};
   for(int k=0;k<6;k++){b.Position3fv(vertices[k].Base());b.Color4ub(p->m_iEndpoint==0?35:255,p->m_iEndpoint==0?150:135,p->m_iEndpoint==0?255:20,255);b.TexCoord2f(0,0,0);b.AdvanceVertex();}
  }
  b.End();mesh->Draw();render->PopView(frustum);ctx->OverrideDepthEnable(false,true);
 }
}

