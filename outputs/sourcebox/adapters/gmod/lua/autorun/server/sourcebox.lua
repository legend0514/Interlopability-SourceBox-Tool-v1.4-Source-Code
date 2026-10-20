-- Enabled only in Sourcebox's explicitly launched local test room.
if game.GetMap() ~= "sourcebox_room_001" then return end
if not game.SinglePlayer() then return end
file.CreateDir("sourcebox")
local config = util.JSONToTable(file.Read("sourcebox/assets.json", "DATA") or "{}") or {}
for _,ent in ipairs(ents.FindByName("sourcebox_remote_*")) do ent:Remove() end
local physics=include("sourcebox/physics.lua")
local scene=include("sourcebox/scene.lua")
local portals=include("sourcebox/portals.lua")
local ragdolls=include("sourcebox/ragdolls.lua")
-- Grant after Sandbox's loadout finishes, independently of core connectivity.
-- Keep the ordinary weapons; use the normal inventory and selection controls.
local function portalStarter(ply,select)
 if not IsValid(ply) or not ply:Alive() or not weapons.GetStored("weapon_portalgun") then return end
 if not ply:HasWeapon("weapon_sourcebox_portalgun") then ply:Give("weapon_sourcebox_portalgun") end
 if select then ply:SelectWeapon("weapon_sourcebox_portalgun") end
end
hook.Add("PlayerSpawn","SourceboxPortalStarter",function(ply)
 timer.Simple(0.2,function() portalStarter(ply,true) end)
end)
timer.Simple(1,function() for _,ply in ipairs(player.GetHumans()) do portalStarter(ply,false) end end)
SourceboxPortalRequest=function(ply,message)
 if not IsValid(ply) or ply~=player.GetHumans()[1] or not ply:Alive() then return end
 local weapon=ply:GetActiveWeapon()
 if not IsValid(weapon) or weapon:GetClass()~="weapon_sourcebox_portalgun" then return end
 if not portals.session then physics:Notice("Portal service is not connected") return end
 if message.type=="portal_fire" then
  local eye=ply:EyePos() local trace=util.TraceLine({start=eye,endpos=eye+ply:GetAimVector()*2048,filter=ply,mask=MASK_SHOT})
  if not trace.Hit or trace.StartSolid then return end
  message.eye={eye.x,eye.y,eye.z} message.hit={trace.HitPos.x,trace.HitPos.y,trace.HitPos.z}
 end
 message.session=portals.session physics:Command(message)
end
local hl1mp=util.JSONToTable(file.Read("sourcebox/hl1mp.json","DATA") or "{}") or {}
local cs2=util.JSONToTable(file.Read("sourcebox/cs2.json","DATA") or "{}") or {}
local proxies, targets = {}, {}
local counter, revision, lastRead = 0, 0, 0
local hitCount,lastHitTick=0,{}
local damageAck,damageSeen,protectedUntil={},{},0
local session = tostring(os.time()) .. ":" .. tostring(SysTime())
local function vec(v) return Vector(v[1],v[2],v[3]) end
local function remove(id) if IsValid(proxies[id]) then proxies[id]:Remove() end proxies[id]=nil targets[id]=nil end
local function update(m)
 local s,id=m.state,m.game
 if id~="hl2" and id~="tf2" and id~="hl1mp" and id~="cs2" and id~="portal2" then return end
 if not s or not s.position or not s.rotation then return end
 local old=targets[id]
 if old and old.session==m.session and old.seq>=s.seq then return end
 local asset=id=="hl1mp" and hl1mp.models[s.model] or id=="tf2" and (config.tf2Classes[s.classId] or config.tf2Classes[tostring(s.classId)]) or config.proxies.tf2
 if id=="cs2" and cs2.source1Teams then
  local team=cs2.source1Teams[tostring(s.team)] or cs2.source1Teams["3"]
  team=team or {model="models/player/gasmask.mdl"}
  asset={model=team.model,sequences={}}
  for key,name in pairs(cs2.source1Sequences or {idle="idle_all_01",run="run_all_01",air="jump_pistol"}) do asset.sequences[key]={name=name} end
 end
 if id=="portal2" then asset={model="models/sourcebox/portal2/chell.mdl",sequences={idle={name="idle"},walk={name="run"},run={name="run"},air={name="air"}}} end
 if not asset then return end
 local model=asset.model
 if not util.IsValidModel(model) then model="models/player/kleiner.mdl" end
 local ent=proxies[id]
 if IsValid(ent) and ent:GetModel()~=model then remove(id) ent=nil end
 if not IsValid(ent) then
  ent=ents.Create("prop_dynamic")
  ent:SetModel(model) ent:SetPos(vec(s.position)) ent:Spawn()
  ent:SetMoveType(MOVETYPE_NONE) ent:SetSolid(SOLID_BBOX)
  ent:SetCollisionBounds(Vector(-16,-16,0),Vector(16,16,72))
  ent:SetNotSolid(false) ent:SetCollisionGroup(COLLISION_GROUP_NONE) ent:Fire("EnableCollision")
  ent:SetSaveValue("m_takedamage",1)

  ent:SetName("sourcebox_remote_"..id) proxies[id]=ent
  print("[Sourcebox] Created "..id.." proxy: "..model)
 end
 ent:SetSkin(id=="tf2" and s.team==3 and 1 or 0)
 local velocity=vec(s.velocity)
 local kind=not s.grounded and "air" or (velocity:Length2D()>10 and "run" or "idle")
 local sequence=(asset.sequences[kind] or asset.sequences.idle or {name="idle"}).name
 local index=ent:LookupSequence(sequence)
 if index>=0 and ent:GetSequence()~=index then ent:ResetSequence(index) end
 ent:SetPlaybackRate(1)
 local yaw=Angle(0,s.rotation[2],0)
 ent:SetPoseParameter("move_x",math.Clamp(velocity:Dot(yaw:Forward())/300,-1,1))
 ent:SetPoseParameter("move_y",math.Clamp(velocity:Dot(yaw:Right())/300,-1,1))
 local position=vec(s.position)
 if old and old.session==m.session and old.position==position and old.angle==yaw then
  old.seq=s.seq
 else
  targets[id]={session=m.session,seq=s.seq,position=position,angle=yaw,from=ent:GetPos(),fromAngle=ent:GetAngles(),time=SysTime()}
 end
end
hook.Add("EntityTakeDamage","SourceboxRemotePlayerDamage",function(ent,damage)
 local target=nil for id,proxy in pairs(proxies) do if ent==proxy then target=id break end end
 if not target then return end
 local attacker=damage:GetAttacker()
 if IsValid(attacker) and attacker:IsPlayer() and damage:GetDamage()>0 then
  local tick=engine.TickCount()
  if lastHitTick[target]~=tick then
   lastHitTick[target]=tick hitCount=hitCount+1
   physics:Command({type="player_hit",target=target})
  end
 end
 -- Keep the proxy intact; the actual remote player receives the damage.
 return true
end)
hook.Add("Think","SourceboxInterpolate",function()
 physics:Tick()
 for id,t in pairs(targets) do
  local ent=proxies[id]
  if IsValid(ent) and not t.complete then
   local a=math.Clamp((SysTime()-t.time)/0.1,0,1)
   local pos,ang=LerpVector(a,t.from,t.position),LerpAngle(a,t.fromAngle,t.angle)
   if ent:GetPos()~=pos then ent:SetPos(pos) end
   if ent:GetAngles()~=ang then ent:SetAngles(ang) end
   t.complete=a>=1
  end
 end
end)
local portalErrorAt=0
local function receivePortals(data)
 local ok,err=xpcall(function() portals:Receive(data,physics) end,debug.traceback)
 if not ok and SysTime()>=portalErrorAt then
  portalErrorAt=SysTime()+5
  ErrorNoHalt("[Sourcebox] Portal update failed; crossplay continues and portal snapshot will retry: "..tostring(err).."\n")
 end
end
timer.Create("SourceboxExchange",0.05,0,function()
 local player=player.GetHumans()[1]
 local state=nil
 if IsValid(player) and player:Alive() then
  local p,a,v=player:GetPos(),player:EyeAngles(),player:GetVelocity()
  local held=player:GetActiveWeapon()
  local weapon=IsValid(held) and held:GetClass() or ""
  if weapon=="weapon_physgun" then weapon="weapon_physcannon" end
  if not (config.hl2Weapons or {})[weapon] then weapon="" end
  state={health=player:Health(),weapon=weapon,model=string.lower(player:GetModel() or ""),sequence=player:GetSequenceName(player:GetSequence()) or "",position={p.x,p.y,p.z},rotation={a.p,a.y,a.r},velocity={v.x,v.y,v.z},grounded=player:OnGround()}
 end
 counter=counter+1
 local diagnostics={}
 for id,ent in pairs(proxies) do if IsValid(ent) then diagnostics[id]={model=ent:GetModel(),sequence=ent:GetSequence(),entity=ent:EntIndex(),solid=ent:GetSolid()} end end
 file.Write("sourcebox/outgoing.json",util.TableToJSON({session=session,counter=counter,state=state,proxies=diagnostics,hits=hitCount,scene=scene:Read(),sceneAck=scene.ack,damageAck=damageAck,portalAck=portals.ack,commands=physics.commands,connection=physics.connection,cube=physics.owner=="gmod" and not physics.freeze and {epoch=physics.epoch,state=physics:Read()} or nil}))
 local data=util.JSONToTable(file.Read("sourcebox/incoming.json","DATA") or "{}")
 if data and data.revision and data.revision~=revision then
  revision=data.revision lastRead=SysTime()
  for _,hit in ipairs(data.playerDamage or {}) do
   if hit.session==session and not damageSeen[hit.event] then
    damageSeen[hit.event]=true table.insert(damageAck,hit.event)
    if #damageAck>128 then damageSeen[table.remove(damageAck,1)]=nil end
    if IsValid(player) and player:Alive() and SysTime()>=protectedUntil then
     local health=player:Health()-math.Clamp(tonumber(hit.amount) or 0,0,100)
     if health<=0 then physics:Command({type="player_death"}) player:SetHealth(player:GetMaxHealth()) player:SetPos(Vector(-128,128,8)) player:SetLocalVelocity(Vector(0,0,0)) protectedUntil=SysTime()+1 else player:SetHealth(health) end
    end
   end
  end
  ragdolls:Receive(data,proxies,player)
  physics:Receive(data,session)
  receivePortals(data)
  scene:Receive(data,session)
  local seen={}
  for _,m in ipairs(data.peers or {}) do
   seen[m.game]=true
   local ok,err=pcall(update,m)
   if not ok then ErrorNoHalt("[Sourcebox] Remote player update failed: "..tostring(err).."\n") end
  end
  for id in pairs(proxies) do if not seen[id] then remove(id) end end
 end
 if SysTime()-lastRead>2 then receivePortals({connected=false}) physics:Clear() for id in pairs(proxies) do remove(id) end end
end)
print("[Sourcebox] GMod adapter active: local player export and HL2/TF2 proxy import")
-- Explicit developer inventory fixture; never grants an unfinished gun normally.
if file.Exists("sourcebox/portal_inventory_probe.txt","DATA") then
 file.Delete("sourcebox/portal_inventory_probe.txt")
 timer.Simple(4,function()
  local ply=player.GetHumans()[1]
  if not IsValid(ply) then return end
  local gun=ply:Give("weapon_sourcebox_portalgun")
  if IsValid(gun) then ply:SelectWeapon("weapon_sourcebox_portalgun") ply:ChatPrint("Sourcebox inventory test: Portal Gun equipped; shared portals are not enabled yet.") end
 end)
end

-- DeGroot is the user's Goblin identity. The workshop registers it under
-- Goblin_ByGus; apply only to this session without changing the saved preference.
local function defaultGoblin(ply)
 if not IsValid(ply) or ply.SourceboxGoblinDefaultApplied then return end
 local model="models/goblin/playerm/goblin.mdl"
 if not util.IsValidModel(model) then return end
 ply.SourceboxGoblinDefaultApplied=true
 ply:SetModel(model)
 print("[Sourcebox] Default player model: DeGroot (Goblin)")
end
hook.Add("PlayerInitialSpawn","SourceboxDefaultGoblin",function(ply)
 timer.Simple(1,function() defaultGoblin(ply) end)
end)
timer.Simple(1,function() for _,ply in ipairs(player.GetAll()) do defaultGoblin(ply) end end)

-- Stock NPC asset coverage updated: G-Man and installed citizen variants.
-- Explicit local acceptance fixture; never created unless the developer flag exists.
if file.Exists("sourcebox/npc_probe.txt","DATA") then
 file.Delete("sourcebox/npc_probe.txt")
 timer.Simple(1,function()
  local probe=ents.Create("npc_gman")
  if IsValid(probe) then probe:SetPos(Vector(0,256,8)) probe:Spawn() print("[Sourcebox test] Spawned native G-Man for NPC replication check") end
 end)
end

if file.Exists("sourcebox/goblin_probe.txt","DATA") then
 file.Delete("sourcebox/goblin_probe.txt")
 timer.Simple(4,function()
  local probe=ents.Create("npc_citizen")
  if IsValid(probe) then probe:SetModel("models/npcgoblin/goblin/male_01.mdl") probe:SetPos(Vector(0,256,8)) probe:SetKeyValue("citizentype","4") probe:Spawn() print("[Sourcebox test] Spawned friendly Goblin NPC") end
 end)
end

include("sourcebox/portal-acceptance.lua")

-- Opt-in native damage fixture: exercises EntityTakeDamage rather than sending
-- synthetic relay packets. Health telemetry at the receiver is the assertion.
timer.Create("SourceboxDamageAcceptance",1,0,function()
 if not file.Exists("sourcebox/damage_probe.txt","DATA") then return end
 local ply=player.GetHumans()[1] if not IsValid(ply) or not ply:Alive() then return end
 local target=string.Trim(file.Read("sourcebox/damage_probe.txt","DATA") or "")
 file.Delete("sourcebox/damage_probe.txt")
 local results={}
 for id,ent in pairs(proxies) do
  if IsValid(ent) and (target=="" or target=="all" or target==id) then
   local hit=DamageInfo() hit:SetAttacker(ply) hit:SetInflictor(ply) hit:SetDamage(10) hit:SetDamageType(DMG_BULLET)
   local before=hitCount ent:TakeDamageInfo(hit)
   results[#results+1]={target=id,detected=hitCount>before,entity=ent:EntIndex()}
  end
 end
 file.Write("sourcebox/damage_probe_result.json",util.TableToJSON(results,true))
end)
