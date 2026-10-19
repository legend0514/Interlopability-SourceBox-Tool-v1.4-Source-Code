-- Compatibility adapter for separately installed Workshop 1800764828.
-- No dependency code or assets are copied into Sourcebox.
local S={pairs={}}
-- The successor calls its shared particle routine on the server, but creates
-- portal_borders only in its client file. Server particles are unnecessary;
-- give that routine a realm-local disabled switch without changing the client.
if not GetConVar("portal_borders") then CreateConVar("portal_borders","0",FCVAR_NONE) end
local settings=util.JSONToTable(file.Read("sourcebox/portals.json","DATA") or "{}") or {}
local function vec(v) return Vector(v[1],v[2],v[3]) end
function S:NeedsRepair(snapshot)
 for _,pair in ipairs(snapshot.pairs or {}) do
  local cached=self.pairs[pair.id]
  for n=1,2 do
   if istable(pair.endpoints[n]) then
    local ent=cached and cached.entities[n]
    if not IsValid(ent) then return true end
    if pair.linked and not IsValid(ent:GetOther()) then return true end
   end
  end
 end
 return false
end
function S:Clear()
 for _,pair in pairs(self.pairs) do for _,ent in pairs(pair.entities) do
  if IsValid(ent) then ent:CleanMeUp() if IsValid(ent) then ent:Remove() end end
 end end
 self.pairs={}
end
function S:Receive(snapshot,physics)
 if not scripted_ents.GetStored("prop_portal") then
  if not self.warned then physics:Notice("Required portal successor is not loaded") self.warned=true end
  return false
 end
 RunConsoleCommand("portal_width",tostring(settings.aperture.halfWidth*2))
 RunConsoleCommand("portal_height",tostring(settings.aperture.halfHeight*2))
 local live={}
 for _,pair in ipairs(snapshot.pairs or {}) do
  live[pair.id]=true
  local old=self.pairs[pair.id]
  if not old then old={entities={}} self.pairs[pair.id]=old end
  local index=table.KeyFromValue(settings.owners,pair.owner)
  if not index then return false end
  for n=1,2 do
   local endpoint=pair.endpoints[n]
   local ent=old.entities[n]
   local signature=endpoint and util.TableToJSON(endpoint) or nil
   if istable(endpoint) and (not IsValid(ent) or old[n]~=signature) then
    local pos=vec(endpoint.position) local ang=vec(endpoint.normal):Angle()
    if not IsValid(ent) then
     ent=ents.Create("prop_portal")
     if not IsValid(ent) then return false end
     ent:SetName("sourcebox_portal_"..pair.owner.."_"..n)
     ent:SetNWBool("SourceboxPortal",true)
     ent:SetPos(pos) ent:SetAngles(ang) ent:Spawn()
     ent.WeaponOwner=true ent.Ownr=player.GetHumans()[1]
     -- The addon grounds the player on its exit trigger. Clear that synthetic
     -- ground after a completed teleport, while leaving its hull/clone handling
     -- intact. Set absolute local velocity once; never add a second impulse.
     local nativeDoPort=ent.DoPort
     ent.DoPort=function(portal,actor)
      if not IsValid(actor) or SysTime()<(actor.SourceboxPortalCooldown or 0) then return end
      local localPlayer=IsValid(actor) and actor==player.GetHumans()[1]
      local before=actor:GetPos()
      local velocity=localPlayer and actor:GetVelocity() or nil
      local other=portal:GetOther()
      nativeDoPort(portal,actor)
      if before and IsValid(actor) and IsValid(other) and actor:GetPos():DistToSqr(before)>128*128 then
       actor.SourceboxPortalCooldown=SysTime()+0.2
       if localPlayer then
        local transformed=portal:TransformOffset(velocity,portal:GetAngles(),other:GetAngles())*-1
        actor:SetGroundEntity(NULL)
        actor:SetLocalVelocity(transformed)
        actor.SourceboxPortalExit=other
       end
      end
     end
     local nativeTouch=ent.Touch
     ent.Touch=function(portal,actor)
      nativeTouch(portal,actor)
      if IsValid(actor) and actor.SourceboxPortalExit==portal and actor:GetGroundEntity()==portal then actor:SetGroundEntity(NULL) end
     end
     -- The addon may simulate only this game's player and authoritative props.
     -- Remote proxies and frozen ownership-transfer objects remain untouched.
     ent.CanPort=function(_,actor)
      if not IsValid(actor) or actor.isClone then return false end
      if SysTime()<(actor.SourceboxPortalCooldown or 0) then return false end
      if actor:IsPlayer() then return actor==player.GetHumans()[1] and actor:Alive() end
      if actor==physics.entity then return physics.owner=="gmod" and not physics.freeze end
      if string.StartWith(actor:GetName(),"sourcebox_") then return false end
      local ph=actor:GetPhysicsObject()
      return IsValid(ph) and ph:IsMotionEnabled() and (actor:GetClass()=="prop_physics" or actor:GetClass()=="prop_physics_multiplayer" or actor:IsNPC())
     end
     old.entities[n]=ent
    else ent:MoveToNewPos(pos,ang) end
    ent.BSPPortal=string.StartWith(endpoint.surface or "","destination:") or (endpoint.world~=nil and endpoint.world~="sourcebox_room_001")
    ent.PortalType=n ent:SetType(n) ent:SetPortalIndex(index)
    -- Pair explicitly: the addon auto-pair search also sees non-Sourcebox guns.
    ent.Activated=true ent:SetNWBool("Potal:Activated",true)
    ent:SetUpEffects(n) old[n]=signature
   elseif not istable(endpoint) and IsValid(ent) then ent:CleanMeUp() old.entities[n]=nil old[n]=nil end
  end
  local a,b=old.entities[1],old.entities[2]
  for _,entry in pairs(old.entities) do
   if IsValid(entry) then
    entry:SetNWBool("Potal:Linked",pair.linked and IsValid(a) and IsValid(b))
    local other=entry==a and b or nil
    if entry==b then other=a end
    entry:SetNWEntity("Potal:Other",IsValid(other) and other or NULL)
   end
  end
 end
 for id,pair in pairs(self.pairs) do if not live[id] then
  for _,ent in pairs(pair.entities) do if IsValid(ent) then ent:CleanMeUp() if IsValid(ent) then ent:Remove() end end end
  self.pairs[id]=nil
 end end
 return true
end
return S
