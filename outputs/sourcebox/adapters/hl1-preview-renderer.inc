// Called only by the SDK tool framework on the render thread. No executable hooks.
#include "portal-preview-wire.h"
#include "portal_preview_config.h"
#include "materialsystem/imesh.h"
#include "tier1/KeyValues.h"
#include "ivrenderview.h"
#include <cmath>
#include "icliententitylist.h"
#include "icliententity.h"
#include "cdll_int.h"
static IVRenderView* renderView=nullptr;
static IVEngineClient* previewEngine=nullptr;
static IClientEntityList* previewEntities=nullptr;
static void DrawPreviewPlayer(const CViewSetup&view){
 if(!previewEntities||!previewEngine||!renderView)return;
 auto*player=previewEntities->GetClientEntity(previewEngine->GetLocalPlayer());if(!player)return;
 VPlane frustum[6];renderView->Push3DView(view,0,nullptr,frustum);
 player->GetClientRenderable()->DrawModel(1);
 renderView->PopView(frustum);
}
static SourceboxPreviewPair framePairs[12];static int frameCount=0;
static ITexture* targets[12]{};static IMaterial* screens[12]{};static IMaterial* mask=nullptr;
static bool ready[12]{};
static Vector PV(const float*v){return Vector(v[0],v[1],v[2]);}
static Vector Through(const Vector&v,const SourceboxPreviewPair&p){return -PV(p.exit.right)*DotProduct(v,PV(p.entry.right))+PV(p.exit.up)*DotProduct(v,PV(p.entry.up))-PV(p.exit.normal)*DotProduct(v,PV(p.entry.normal));}
static void InitPreviewTargets(){
 if(targets[0])return;
 for(int i=0;i<12;i++){
  char name[80],mat[80];snprintf(name,sizeof(name),"_rt_sourcebox_hl1_%d",i);snprintf(mat,sizeof(mat),"sourcebox/hl1_preview_%d",i);
  targets[i]=materials->CreateNamedRenderTargetTextureEx(name,SOURCEBOX_PREVIEW_SIZE,SOURCEBOX_PREVIEW_SIZE,RT_SIZE_NO_CHANGE,IMAGE_FORMAT_RGB888,MATERIAL_RT_DEPTH_SEPARATE);
  auto*k=new KeyValues("UnlitGeneric");k->SetString("$basetexture",name);k->SetInt("$ignorez",1);k->SetInt("$nofog",1);screens[i]=materials->CreateMaterial(mat,k);screens[i]->IncrementReferenceCount();
 }
 auto*k=new KeyValues("UnlitGeneric");k->SetString("$basetexture","vgui/white");k->SetInt("$nocull",1);k->SetInt("$nofog",1);mask=materials->CreateMaterial("sourcebox/hl1_preview_mask",k);mask->IncrementReferenceCount();
}
static void RenderPortalPreviews(){
 if(drawing||!engineTool||!materials||!renderView||!engineTool->IsInGame())return;
 auto module=GetModuleHandleA("sourcebox_hl1mp.dll");auto read=module?(SourceboxReadPreviewsFn)GetProcAddress(module,"SourceboxReadPreviews"):nullptr;
 frameCount=read?read(SOURCEBOX_PREVIEW_ABI,framePairs,12):0;memset(ready,0,sizeof(ready));if(frameCount<=0)return;
 int screenWidth,screenHeight;previewEngine->GetScreenSize(screenWidth,screenHeight);
 CViewSetup main;if(!engineTool->GetPlayerView(main,0,0,screenWidth,screenHeight))return;
 drawing=true;InitPreviewTargets();
 for(int i=0;i<frameCount;i++){
  const auto&p=framePairs[i];if(DotProduct(main.origin-PV(p.entry.position),PV(p.entry.normal))<=0.5f)continue;
  if(!targets[i]||targets[i]->IsError())continue;
  CViewSetup view=main;view.x=view.y=view.m_nUnscaledX=view.m_nUnscaledY=0;
  view.width=view.m_nUnscaledWidth=SOURCEBOX_PREVIEW_SIZE;view.height=view.m_nUnscaledHeight=SOURCEBOX_PREVIEW_SIZE;
  view.m_flAspectRatio=main.m_flAspectRatio>0?main.m_flAspectRatio:float(main.width)/main.height;
  view.origin=PV(p.exit.position)+Through(main.origin-PV(p.entry.position),p);
  Vector f,u;AngleVectors(main.angles,&f,nullptr,&u);VectorAngles(Through(f,p),Through(u,p),view.angles);
  view.zNear=1;view.m_bDoBloomAndToneMapping=false;
  float plane[4]={p.exit.normal[0],p.exit.normal[1],p.exit.normal[2],DotProduct(PV(p.exit.normal),PV(p.exit.position))+0.1f};
  auto*ctx=materials->GetRenderContext();ctx->BeginRender();ctx->PushRenderTargetAndViewport(targets[i]);
  bool clip=ctx->EnableClipping(true);ctx->PushCustomClipPlane(plane);
  engineTool->RenderView(view,VIEW_CLEAR_COLOR|VIEW_CLEAR_DEPTH,4);DrawPreviewPlayer(view);
  ctx->PopCustomClipPlane();ctx->EnableClipping(clip);ctx->PopRenderTargetAndViewport();ctx->EndRender();ctx->Release();ready[i]=true;
 }
 drawing=false;
}
static void CompositePortalPreviews(){
 if(drawing||frameCount<=0||!engineTool||!materials||!renderView||!engineTool->IsInGame())return;
 int w,h;auto*ctx=materials->GetRenderContext();ctx->BeginRender();ctx->GetRenderTargetDimensions(w,h);
 CViewSetup view;if(!engineTool->GetPlayerView(view,0,0,w,h)){ctx->EndRender();ctx->Release();return;}
 for(int i=0;i<frameCount;i++)if(ready[i]){
  const auto&p=framePairs[i];ctx->ClearStencilBufferRectangle(view.x,view.y,view.x+view.width,view.y+view.height,0);
  ctx->SetStencilEnable(true);ctx->SetStencilWriteMask(255);ctx->SetStencilTestMask(255);ctx->SetStencilReferenceValue(1);
  ctx->SetStencilCompareFunction(STENCILCOMPARISONFUNCTION_ALWAYS);ctx->SetStencilPassOperation(STENCILOPERATION_REPLACE);ctx->SetStencilFailOperation(STENCILOPERATION_KEEP);ctx->SetStencilZFailOperation(STENCILOPERATION_KEEP);
  ctx->OverrideColorWriteEnable(true,false);ctx->OverrideDepthEnable(true,false);
  VPlane frustum[6];renderView->Push3DView(view,0,nullptr,frustum);ctx->Bind(mask);
  auto*mesh=ctx->GetDynamicMesh(true,nullptr,nullptr,mask);CMeshBuilder b;b.Begin(mesh,MATERIAL_TRIANGLES,48);
  auto center=PV(p.entry.position)+PV(p.entry.normal)*1.1f;
  for(int j=0;j<48;j++){
   Vector vertices[3]={center,center+PV(p.entry.right)*(p.halfWidth*cosf(j*6.2831853f/48))+PV(p.entry.up)*(p.halfHeight*sinf(j*6.2831853f/48)),center+PV(p.entry.right)*(p.halfWidth*cosf((j+1)*6.2831853f/48))+PV(p.entry.up)*(p.halfHeight*sinf((j+1)*6.2831853f/48))};
   for(auto&v:vertices){b.Position3fv(v.Base());b.Color4ub(255,255,255,255);b.TexCoord2f(0,0,0);b.AdvanceVertex();}
  }b.End();mesh->Draw();renderView->PopView(frustum);
  ctx->OverrideColorWriteEnable(false,true);ctx->OverrideDepthEnable(false,true);ctx->SetStencilCompareFunction(STENCILCOMPARISONFUNCTION_EQUAL);ctx->SetStencilPassOperation(STENCILOPERATION_KEEP);ctx->SetStencilWriteMask(0);
  ctx->DrawScreenSpaceRectangle(screens[i],view.x,view.y,view.width,view.height,0,0,SOURCEBOX_PREVIEW_SIZE-1,SOURCEBOX_PREVIEW_SIZE-1,SOURCEBOX_PREVIEW_SIZE,SOURCEBOX_PREVIEW_SIZE);
  ctx->SetStencilEnable(false);ctx->SetStencilWriteMask(255);
 }
 ctx->EndRender();ctx->Release();
}
