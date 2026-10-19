local definitions=util.JSONToTable(file.Read("sourcebox/scene.json","DATA") or "{}") or {}
local allowed,npcs={},{} for _,m in ipairs(definitions.models or {}) do allowed[m]=true end for _,m in ipairs(definitions.npcModels or {}) do npcs[m]=true end
for _,m in ipairs((definitions.addon or {}).npcModels or {}) do npcs[m]=true end
local S={nextId=0,ids=setmetatable({},{__mode="k"}),entities={},warned={},ack={},seen={}}
local function arr(v) return {v.x,v.y,v.z} end
local candidates={}
local function track(e)
 if not IsValid(e) then return end
 local c=e:GetClass()
 if c=="prop_physics" or c=="prop_physics_multiplayer" or e:IsNPC() then candidates[e]=true end
end
for _,e in ipairs(ents.GetAll()) do track(e) end
hook.Add("OnEntityCreated","SourceboxSceneTrack",function(e) timer.Simple(0,function() track(e) end) end)
hook.Add("EntityRemoved","SourceboxSceneUntrack",function(e) candidates[e]=nil end)
function S:Read()
 local objects={} self.entities={}
 for e in pairs(candidates) do
  if not IsValid(e) then candidates[e]=nil continue end
  local class=e:GetClass()
  local npc=e:IsNPC()
  if (class=="prop_physics" or class=="prop_physics_multiplayer" or npc) and not e.isClone and not e.AntiPaint and not e:CreatedByMap() and not string.StartWith(e:GetName(),"sourcebox_") then
   local model=string.lower(e:GetModel() or "")
   if (npc and npcs[model]) or (not npc and allowed[model]) then
    if #objects<(definitions.maxEntities or 32) then
     if not self.ids[e] then self.nextId=self.nextId+1 self.ids[e]=self.nextId end
     local id=self.ids[e] self.entities[id]=e
     local angle=e:GetAngles() local ph=e:GetPhysicsObject()
     local sequence=e:GetSequenceName(e:GetSequence()) or ""
     if not string.match(sequence,"^[%w_ .%-]*$") or #sequence>96 then sequence="" end
     objects[#objects+1]={id=id,kind=npc and "npc" or "prop",model=model,sequence=sequence,health=math.Clamp(e:Health(),0,1000000),position=arr(e:GetPos()),rotation={angle.p,angle.y,angle.r},velocity=arr(IsValid(ph) and ph:GetVelocity() or e:GetVelocity()),angularVelocity=arr(IsValid(ph) and ph:GetAngleVelocity() or Vector())}
    elseif not self.warned.limit then self.warned.limit=true for _,p in ipairs(player.GetHumans()) do p:ChatPrint("Sourcebox: current shared-entity limit is 32.") end end
   elseif not self.warned[model] then self.warned[model]=true for _,p in ipairs(player.GetHumans()) do p:ChatPrint("Sourcebox: this model is not on the verified shared-asset list: "..model) end end
  end
 end
 return {objects=objects}
end
function S:Receive(data,session)
 for _,m in ipairs(data.sceneActions or {}) do
  if not self.seen[m.event] then
   self.seen[m.event]=true self.ack[#self.ack+1]=m.event
   if #self.ack>256 then local old=table.remove(self.ack,1) self.seen[old]=nil end
   local e=self.entities[m.id]
   if m.generation==session and IsValid(e) then
    local source=Vector(unpack(m.position))
    local direction=e:WorldSpaceCenter()-(source+Vector(0,0,36)) direction:Normalize()
    local ph=e:GetPhysicsObject()
    if m.action=="hit" then
     local damage=DamageInfo() damage:SetDamage(definitions.interactions.damage) damage:SetDamageType(DMG_BULLET)
     damage:SetAttacker(game.GetWorld()) damage:SetInflictor(game.GetWorld()) damage:SetDamagePosition(e:WorldSpaceCenter()) damage:SetDamageForce(direction*500)
     e:TakeDamageInfo(damage)
    end
    if IsValid(ph) then ph:Wake() ph:ApplyForceCenter(direction*ph:GetMass()*definitions.interactions.pushSpeed) end
   end
  end
 end
end
return S

