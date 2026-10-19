// Only native authoritative physics bodies travel. Replicated props never do.
#include "vphysics_interface.h"
#include "filesystem.h"
class CSourceboxPropTravel : public CAutoGameSystemPerFrame {
 struct Sample { EHANDLE entity; Vector center, velocity; float time, cooldown; };
 CUtlVector<Sample> samples;
 float probeCheck=0;
 float probeUntil=0,probeLogAt=0;
 static Vector Rotate(const Vector& v,CBaseEntity* a,CBaseEntity* b) {
  Vector af,ar,au,bf,br,bu;AngleVectors(a->GetAbsAngles(),&af,&ar,&au);AngleVectors(b->GetAbsAngles(),&bf,&br,&bu);
  return -bf*DotProduct(v,af)-br*DotProduct(v,ar)+bu*DotProduct(v,au);
 }
public:
 CSourceboxPropTravel():CAutoGameSystemPerFrame("SourceboxPropTravel"){}
 void LevelShutdownPostEntity() OVERRIDE {samples.RemoveAll();}
 void FrameUpdatePostEntityThink() OVERRIDE {
  if(Q_strcmp(STRING(gpGlobals->mapname),"sourcebox_room_001"))return;
  if(gpGlobals->curtime>=probeCheck){
   probeCheck=gpGlobals->curtime+1;
#ifdef TF_DLL
   const char* marker="sourcebox-prop-probe-tf2.txt";
   const char* crate="sourcebox_tf2_crate";
#else
   const char* marker="sourcebox-prop-probe-hl2.txt";
   const char* crate="sourcebox_hl2_crate";
#endif
   if(filesystem->FileExists(marker,"GAME")){
    CBaseEntity* object=gEntList.FindEntityByName(NULL,crate);CBaseEntity* portal=NULL;
    while((portal=gEntList.FindEntityByClassname(portal,"sourcebox_portal"))!=NULL){
     Vector normal;AngleVectors(portal->GetAbsAngles(),&normal);if(normal.z<0.99f)continue;
     auto* entry=static_cast<CSourceboxPortal*>(portal);bool linked=false;CBaseEntity* other=NULL;
     while((other=gEntList.FindEntityByClassname(other,"sourcebox_portal"))!=NULL)if(other!=entry&&static_cast<CSourceboxPortal*>(other)->m_iPair==entry->m_iPair)linked=true;
     if(!linked)continue;
     if(!object||!object->VPhysicsGetObject())break;
     Vector position=portal->GetAbsOrigin()+Vector(0,0,160),velocity(0,0,0);QAngle angles(0,0,0);AngularImpulse spin(0,0,0);
     object->Teleport(&position,&angles,&velocity);object->VPhysicsGetObject()->SetPosition(position,angles,true);object->VPhysicsGetObject()->SetVelocity(&velocity,&spin);object->VPhysicsGetObject()->Wake();filesystem->RemoveFile(marker,"GAME");probeUntil=gpGlobals->curtime+4;
     Msg("[Sourcebox] prop probe dropped %s above %s\n",crate,STRING(portal->GetEntityName()));break;
    }
   }
  }
  for(int i=samples.Count()-1;i>=0;--i)if(!samples[i].entity.Get())samples.Remove(i);
  CBaseEntity* e=NULL;
  while((e=gEntList.FindEntityByClassname(e,"prop_physics"))!=NULL){
   const char* name=STRING(e->GetEntityName());
   if(Q_strncmp(name,"sourcebox_hl2_",14)&&Q_strncmp(name,"sourcebox_tf2_",14)&&Q_strcmp(name,"sourcebox_shared_cube"))continue;
   IPhysicsObject* ph=e->VPhysicsGetObject();if(!ph||!ph->IsMotionEnabled())continue;
   Vector center=e->WorldSpaceCenter(),vel;AngularImpulse spin;ph->GetVelocity(&vel,&spin);e->SetAbsVelocity(vel);
   int index=-1;for(int j=0;j<samples.Count();j++)if(samples[j].entity.Get()==e){index=j;break;}
   if(index<0){Sample s;s.entity=e;s.center=center;s.velocity=vel;s.time=gpGlobals->curtime;s.cooldown=0;samples.AddToTail(s);continue;}
   Sample& s=samples[index];Vector previous=s.center,oldVelocity=s.velocity;float dt=gpGlobals->curtime-s.time;
   s.center=center;s.velocity=vel;s.time=gpGlobals->curtime;
   if(dt<=0||dt>0.25f||s.cooldown>gpGlobals->curtime)continue;
   Vector mins,maxs;e->CollisionProp()->WorldSpaceAABB(&mins,&maxs);Vector half=(maxs-mins)*0.5f;
   CBaseEntity* p=NULL;
   while((p=gEntList.FindEntityByClassname(p,"sourcebox_portal"))!=NULL){
    CSourceboxPortal* entry=static_cast<CSourceboxPortal*>(p);CSourceboxPortal* exit=NULL;CBaseEntity* q=NULL;
    while((q=gEntList.FindEntityByClassname(q,"sourcebox_portal"))!=NULL){auto* other=static_cast<CSourceboxPortal*>(q);if(other!=entry&&other->m_iPair==entry->m_iPair){exit=other;break;}}
    if(!exit)continue;
    Vector f,r,u;AngleVectors(entry->GetAbsAngles(),&f,&r,&u);
    float radius=fabs(f.x)*half.x+fabs(f.y)*half.y+fabs(f.z)*half.z; if(probeUntil>gpGlobals->curtime&&probeLogAt<gpGlobals->curtime&&f.z>0.99f&&Q_strstr(name,"crate")){probeLogAt=gpGlobals->curtime+0.1f;Msg("[Sourcebox] prop sample %s z=%.2f vz=%.2f radius=%.2f half=%.2f %.2f %.2f\n",name,center.z,vel.z,radius,half.x,half.y,half.z);}
    float a=DotProduct(previous-entry->GetAbsOrigin(),f)-radius-2,b=DotProduct(center-entry->GetAbsOrigin(),f)-radius-2;
    if(b>0||(b<-radius-2&&a<=0)||b>a+0.001f)continue;
    Vector incoming=vel;if(DotProduct(oldVelocity,f)<DotProduct(incoming,f))incoming=oldVelocity;
    if(DotProduct(incoming,f)>-1)continue;
    Vector point=previous+(center-previous)*(a>0?clamp(a/(a-b),0.f,1.f):1.f),offset=point-entry->GetAbsOrigin();
    float rw=fabs(r.x)*half.x+fabs(r.y)*half.y+fabs(r.z)*half.z,uh=fabs(u.x)*half.x+fabs(u.y)*half.y+fabs(u.z)*half.z;
    if(fabs(DotProduct(offset,r))+rw>32||fabs(DotProduct(offset,u))+uh>64){if(probeUntil>gpGlobals->curtime)Msg("[Sourcebox] prop aperture blocked %s extent %.2f %.2f offset %.2f %.2f\n",name,rw,uh,DotProduct(offset,r),DotProduct(offset,u));continue;}
    Vector ef,er,eu;AngleVectors(exit->GetAbsAngles(),&ef,&er,&eu);
    // Conservative sphere clearance covers rotated object bounds.
    float clearance=half.Length();Vector destCenter=exit->GetAbsOrigin()-er*DotProduct(offset,r)+eu*DotProduct(offset,u)+ef*(clearance+4);
    Vector bodyForward,bodyUp;AngleVectors(e->GetAbsAngles(),&bodyForward,NULL,&bodyUp);QAngle angles;VectorAngles(Rotate(bodyForward,entry,exit),Rotate(bodyUp,entry,exit),angles);
    Vector pos=destCenter-Rotate(center-e->GetAbsOrigin(),entry,exit),out=Rotate(incoming,entry,exit),angular=Rotate(spin,entry,exit);
    trace_t tr;Vector bound(clearance,clearance,clearance);UTIL_TraceHull(destCenter,destCenter,-bound,bound,MASK_SOLID,e,COLLISION_GROUP_NONE,&tr);
    if(tr.startsolid||tr.allsolid){if(probeUntil>gpGlobals->curtime)Msg("[Sourcebox] prop exit blocked %s by %s\n",name,tr.m_pEnt?tr.m_pEnt->GetClassname():"world");continue;}
    e->Teleport(&pos,&angles,&out);ph->SetPosition(pos,angles,true);ph->SetVelocity(&out,&angular);ph->Wake();
    s.center=destCenter;s.velocity=out;s.cooldown=gpGlobals->curtime+0.25f;
    Msg("[Sourcebox] prop portal %s pair=%d speed=%.2f -> %.2f\n",name,entry->m_iPair.Get(),incoming.Length(),out.Length());break;
   }
  }
 }
};
static CSourceboxPropTravel sourceboxPropTravel;



