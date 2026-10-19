#pragma once
#include "engine/IEngineTrace.h"
#include "gametrace.h"
#include "iserverunknown.h"
// Portal 2 EngineTraceServer004 differs from SDK2013 at both vtable and ray ABI.
// Layout verified against alliedmodders/hl2sdk portal2 public/cmodel.h and
// public/engine/IEngineTrace.h. The CGameTrace result and filter ABI are shared.
struct PortalRay {
 VectorAligned start,delta,offset,extents;
 const matrix3x4_t* transform=nullptr;
 bool isRay,isSwept;
 void Init(const Vector&a,const Vector&b,const Vector&mins,const Vector&maxs){
  delta=b-a;extents=(maxs-mins)*0.5f;offset=(maxs+mins)*0.5f;
  start=a+offset;offset*=-1;transform=nullptr;isRay=extents.LengthSqr()<1e-6f;isSwept=delta.LengthSqr()!=0;
 }
 void Init(const Vector&a,const Vector&b){Init(a,b,Vector(0,0,0),Vector(0,0,0));}
};
class PortalEngineTrace {
public:
 virtual int GetPointContents(const Vector&,int,IHandleEntity**)=0;
 virtual int GetPointContentsWorld(const Vector&,int)=0;
 virtual int GetPointContentsCollideable(ICollideable*,const Vector&)=0;
 virtual void ClipRayToEntity(const PortalRay&,unsigned int,IHandleEntity*,trace_t*)=0;
 virtual void ClipRayToCollideable(const PortalRay&,unsigned int,ICollideable*,trace_t*)=0;
 virtual void TraceRay(const PortalRay&,unsigned int,ITraceFilter*,trace_t*)=0;
};
