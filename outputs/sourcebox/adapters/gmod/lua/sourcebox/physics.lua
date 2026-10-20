local P={epoch=0,owner=nil,entity=nil,commands={},commandId=0,connection=nil,freeze=nil,replicas={},lastState=nil,missingAt=nil}
local definitions=util.JSONToTable(file.Read("sourcebox/physics.json","DATA") or "{}") or {}
local function v(a) return Vector(a[1],a[2],a[3]) end
local function a(t) return Angle(t[1],t[2],t[3]) end
local function arr(v) return {v.x,v.y,v.z} end
for _,e in ipairs(ents.FindByName("sourcebox_shared_cube")) do e:Remove() end
for _,d in ipairs(definitions.objects or {}) do for _,e in ipairs(ents.FindByName(d.id)) do e:Remove() end end
function P:Command(m) if definitions.cube.enabled==false and string.sub(m.type or "",1,5)=="cube_" then return end self.commandId=self.commandId+1 self.commands[#self.commands+1]={id=self.commandId,message=m} if #self.commands>32 then table.remove(self.commands,1) end end
function P:Notice(text) for _,p in ipairs(player.GetHumans()) do p:ChatPrint("Sourcebox: "..text) end end
function P:Delete() if IsValid(self.entity) then self.entity:Remove() end self.entity=nil end
function P:Clear() self:Delete() for id,e in pairs(self.replicas) do if IsValid(e) then e:Remove() end self.replicas[id]=nil end self.epoch=0 self.owner=nil end
function P:Read()
 if not IsValid(self.entity) then local s=table.Copy(self.lastState or {position=definitions.cube.position,rotation={0,0,0},velocity={0,0,0},angularVelocity={0,0,0},solid=true,timestamp=0}) s.alive=false return s end
 local e=self.entity local angle=e:GetAngles() local ph=e:GetPhysicsObject()
 local s={alive=true,position=arr(e:GetPos()),rotation={angle.p,angle.y,angle.r},velocity=arr(IsValid(ph) and ph:GetVelocity() or Vector()),angularVelocity=arr(IsValid(ph) and ph:GetAngleVelocity() or Vector()),solid=true,timestamp=0}
 self.lastState=s return s
end
function P:Spawn(s)
 self:Delete() self.target=nil self.lastState=s
 if definitions.cube.enabled==false or not s or not s.alive then return end
 local owner=self.owner=="gmod"
 local e=ents.Create(owner and "prop_physics" or "prop_dynamic_override")
 e:SetModel(definitions.cube.model) e:SetName(definitions.cube.id) e:SetPos(v(s.position)) e:SetAngles(a(s.rotation)) e:Spawn() e:SetColor(Color(80,180,255))
 if owner then local ph=e:GetPhysicsObject() if IsValid(ph) then ph:Wake() end else e:SetMoveType(MOVETYPE_NONE) e:SetSolid(SOLID_BBOX) e:SetCollisionBounds(e:OBBMins(),e:OBBMaxs()) e:SetNotSolid(false) e:SetCollisionGroup(COLLISION_GROUP_NONE) end
 self.entity=e
end
function P:Receive(data,session)
 if data.commandSession==session then while self.commands[1] and self.commands[1].id<=(data.commandAck or 0) do table.remove(self.commands,1) end end
 if not data.connected then self:Clear() return end
 if self.connection~=data.connection then self:Delete() self.epoch=0 self.owner=nil self.freeze=nil self.connection=data.connection self:Command({type="cube_sync"}) end
 local packet=data.cube
 if packet and (self.epoch~=packet.epoch or self.owner~=packet.owner) then
  self.epoch=packet.epoch self.owner=packet.owner self.freeze=nil self:Spawn(packet.state)
  self:Notice("Blue cube owner: "..self.owner..". Press E nearby to take control.")
  if self.owner=="gmod" then self:Command({type="cube_ready",epoch=self.epoch,created=IsValid(self.entity)}) end
 elseif packet and self.owner~="gmod" then
  if not packet.state.alive then self:Delete() elseif not IsValid(self.entity) then self:Spawn(packet.state) else self.target=packet.state end
 end
 if data.freeze and self.owner=="gmod" and self.freeze~=data.freeze.transfer then
  self.freeze=data.freeze.transfer local state=self:Read() local old=self.entity self:Delete()
  self.freezePending={old=old,state=state,epoch=self.epoch,transfer=self.freeze}
 end
 for _,d in ipairs(definitions.objects or {}) do
  local state=nil local group=(data.physics or {})[d.owner]
  for _,s in ipairs(group and group.props or {}) do if s.id==d.id then state=s break end end
  local e=self.replicas[d.id]
  if not state or not state.alive then if IsValid(e) then e:Remove() end self.replicas[d.id]=nil
  else
   if not IsValid(e) then e=ents.Create("prop_dynamic_override") e:SetModel(d.model) e:SetName(d.id) e:Spawn() e:SetMoveType(MOVETYPE_NONE) e:SetSolid(SOLID_BBOX) e:SetCollisionBounds(e:OBBMins(),e:OBBMaxs()) e:SetNotSolid(false) e:SetCollisionGroup(COLLISION_GROUP_NONE) self.replicas[d.id]=e end
   local pos,ang=v(state.position),a(state.rotation)
   if e:GetPos()~=pos then e:SetPos(pos) end
   if e:GetAngles()~=ang then e:SetAngles(ang) end
  end
 end
 if data.notice and data.notice~="" and data.notice~=self.notice then self.notice=data.notice self:Notice(data.notice) end
end
function P:Tick()
 if self.freezePending and not IsValid(self.freezePending.old) then local f=self.freezePending self.freezePending=nil if self.epoch==f.epoch and self.freeze==f.transfer then self:Command({type="cube_frozen",epoch=f.epoch,transfer=f.transfer,state=f.state}) end end
 if self.owner~="gmod" and self.target and IsValid(self.entity) then
  local f=math.Clamp(FrameTime()*15,0,1)
  local pos,ang=v(self.target.position),a(self.target.rotation)
  if self.entity:GetPos():DistToSqr(pos)>0.0001 then self.entity:SetPos(LerpVector(f,self.entity:GetPos(),pos)) end
  local current=self.entity:GetAngles()
  if math.abs(math.AngleDifference(current.p,ang.p))+math.abs(math.AngleDifference(current.y,ang.y))+math.abs(math.AngleDifference(current.r,ang.r))>0.01 then self.entity:SetAngles(LerpAngle(f,current,ang)) end
 end
 if self.owner=="gmod" and not self.freeze then
  if IsValid(self.entity) then self.missingAt=nil elseif not self.missingAt then self.missingAt=CurTime() elseif CurTime()-self.missingAt>5 then self:Spawn({alive=true,position=definitions.cube.position,rotation={0,0,0},velocity={0,0,0},angularVelocity={0,0,0},solid=true,timestamp=0}) self.missingAt=nil end
 end
end
hook.Add("KeyPress","SourceboxCubeUse",function(p,key)
 if key==IN_USE and P.owner~="gmod" and IsValid(P.entity) and p:GetPos():Distance(P.entity:GetPos())<=160 then P:Command({type="cube_take"}) end
end)
local function requestPickup(p,e)
 if e==P.entity and P.owner~="gmod" then
  if p:GetPos():Distance(e:GetPos())<=160 then P:Command({type="cube_take"}) P:Notice("Requesting cube control; grab again after ownership changes.") end
  return false
 end
 for _,replica in pairs(P.replicas) do if e==replica then P:Notice("This prop is controlled by its original game. Use the BLUE CUBE for shared pickup.") return false end end
 for _,proxy in ipairs(ents.FindByName("sourcebox_remote_*")) do if e==proxy then return false end end
end
hook.Add("PhysgunPickup","SourceboxPickup",requestPickup)
hook.Add("GravGunPickupAllowed","SourceboxPickup",requestPickup)
return P
