-- Explicit local gameplay acceptance fixture. Executes the real SWEP and engine movement.
-- Run only when the developer creates data/sourcebox/portal_acceptance_probe.txt.
local started=false
local nextCheck=0
-- Reproduce native cleanup without changing the core's portal revision.
timer.Create("SourceboxPortalRepairProbe",1,0,function()
 if not file.Exists("sourcebox/portal_repair_probe.txt","DATA") then return end
 local candidate
 for _,ent in ipairs(ents.FindByClass("prop_portal")) do
  if ent:GetName()=="sourcebox_portal_gmod_1" and ent:IsLinked() then candidate=ent break end
 end
 if not IsValid(candidate) then return end
 file.Delete("sourcebox/portal_repair_probe.txt")
 local name=candidate:GetName()
 local before=util.JSONToTable(file.Read("sourcebox/incoming.json","DATA") or "{}")
 candidate:Remove()
 timer.Simple(3,function()
  local matches=ents.FindByName(name) local replacement=matches[1]
  local after=util.JSONToTable(file.Read("sourcebox/incoming.json","DATA") or "{}")
  file.Write("sourcebox/portal_repair_result.json",util.TableToJSON({count=#matches,restored=IsValid(replacement),linked=IsValid(replacement) and replacement:IsLinked(),beforeRevision=before.portals and before.portals.revision,afterRevision=after.portals and after.portals.revision}))
 end)
end)
hook.Add("Think","SourceboxPortalAcceptance",function()
 if SysTime()<nextCheck then return end
 nextCheck=SysTime()+1
 if started or not file.Exists("sourcebox/portal_acceptance_probe.txt","DATA") then return end
 local ply=player.GetHumans()[1]
 if not IsValid(ply) or not ply:Alive() or not ply:HasWeapon("weapon_sourcebox_portalgun") then return end
 started=true file.Delete("sourcebox/portal_acceptance_probe.txt")
 local original=ply:GetPos() local angles=ply:EyeAngles()
 local function record(stage)
  if not IsValid(ply) then return end
  local portals={}
  for _,e in ipairs(ents.FindByClass("prop_portal")) do portals[#portals+1]={name=e:GetName(),position=tostring(e:GetPos()),angles=tostring(e:GetAngles()),linked=e:IsLinked(),active=e:GetNWBool("Potal:Activated"),other=IsValid(e:GetOther()) and e:GetOther():GetName() or "none",within=e:PlayerWithinBounds(ply),solid=e:GetSolid(),canPort=e:CanPort(ply)} end
  file.Append("sourcebox/portal_acceptance.txt",util.TableToJSON({stage=stage,time=SysTime(),position={ply:GetPos().x,ply:GetPos().y,ply:GetPos().z},inPortal=IsValid(ply.InPortal) and ply.InPortal:GetName() or "none",portals=portals,width=GetConVar("portal_width"):GetInt(),height=GetConVar("portal_height"):GetInt(),renderOnly=GetConVar("portal_rendering_mode"):GetBool()}).."\n")
 end
 ply:SetPos(Vector(0,-320,0.03125)) ply:SetEyeAngles(Angle(0,0,0)) ply:SelectWeapon("weapon_sourcebox_portalgun")
 timer.Simple(1,function() if IsValid(ply) then local w=ply:GetActiveWeapon() if IsValid(w) and w:GetClass()=="weapon_sourcebox_portalgun" then w:PrimaryAttack() record("native_primary") end end end)
 timer.Simple(1.5,function() if IsValid(ply) then ply:SetEyeAngles(Angle(0,180,0)) end end)
 timer.Simple(2,function() if IsValid(ply) then local w=ply:GetActiveWeapon() if IsValid(w) and w:GetClass()=="weapon_sourcebox_portalgun" then w:SecondaryAttack() record("native_secondary") end end end)
 timer.Simple(3,function()
  if not IsValid(ply) then return end
  ply:SetPos(Vector(450,-320,0.03125)) ply:SetEyeAngles(Angle(0,0,0)) record("walk_begin")
  local untilTime=CurTime()+2
  hook.Add("StartCommand","SourceboxPortalAcceptanceWalk",function(p,cmd)
   if p~=ply then return end
   if CurTime()>untilTime or p:GetPos().x<0 then cmd:SetForwardMove(0) hook.Remove("StartCommand","SourceboxPortalAcceptanceWalk") record("walk_end") return end
   cmd:SetViewAngles(Angle(0,0,0)) cmd:SetForwardMove(250) cmd:SetSideMove(0)
  end)
 end)
 timer.Simple(6,function() record("result") if IsValid(ply) then ply:SetPos(original) ply:SetEyeAngles(angles) end hook.Remove("StartCommand","SourceboxPortalAcceptanceWalk") end)
end)


-- Opt-in real-engine gravity/portal test; no synthetic pass result.
timer.Create("SourceboxPropPortalProbe",1,0,function()
 if not file.Exists("sourcebox/prop_portal_probe.txt","DATA") then return end
 local ply=player.GetHumans()[1]
 if not IsValid(ply) or not ply:Alive() or not ply:HasWeapon("weapon_sourcebox_portalgun") then return end
 file.Delete("sourcebox/prop_portal_probe.txt")
 for _,old in ipairs(ents.FindByName("acceptance_sourcebox_crate")) do old:Remove() end
 local original,angles,move=ply:GetPos(),ply:EyeAngles(),ply:GetMoveType() ply:SetMoveType(MOVETYPE_NOCLIP)
 ply:SelectWeapon("weapon_sourcebox_portalgun")
 ply:SetPos(Vector(0,-128,1)) ply:SetEyeAngles(Angle(90,0,0))
 timer.Simple(1,function() if IsValid(ply) then SourceboxPortalRequest(ply,{type="portal_fire",endpoint=0}) end end)
 timer.Simple(2,function() if IsValid(ply) then ply:SetPos(Vector(0,-128,128)) ply:SetEyeAngles(Angle(0,0,0)) end end)
 timer.Simple(3,function() if IsValid(ply) then SourceboxPortalRequest(ply,{type="portal_fire",endpoint=1}) end end)
 timer.Simple(5,function()
  if IsValid(ply) then ply:SetPos(original) ply:SetEyeAngles(angles) ply:SetMoveType(move) end
  local ent=ents.Create("prop_physics")
  if not IsValid(ent) then return end
  ent:SetName("acceptance_sourcebox_crate") ent:SetModel("models/props_junk/wood_crate001a.mdl") ent:SetPos(Vector(0,-128,170)) ent:Spawn()
  local ph=ent:GetPhysicsObject() if IsValid(ph) then ph:Wake() end
  local samples={} local crossed=false local count=0
  timer.Create("SourceboxPropPortalSamples",0.05,100,function()
   count=count+1
   if IsValid(ent) then
    local p=ent:GetPos() local vel=ent:GetVelocity()
    if p.x > 440 and p.z > 100 then crossed=true end
    samples[#samples+1]={position={p.x,p.y,p.z},velocity={vel.x,vel.y,vel.z}}
   end
   if count==100 then
    file.Write("sourcebox/prop_portal_result.json",util.TableToJSON({crossed=crossed,samples=samples}))
    print("[Sourcebox test] Native gravity prop portal crossed="..tostring(crossed))
   end
  end)
 end)
end)


-- Exercise foreign-owner floor portals with native GMod props as well.
timer.Create("SourceboxForeignPropProbe",1,0,function()
 if not file.Exists("sourcebox/foreign_prop_probe.txt","DATA") then return end
 local entries={}
 for _,e in ipairs(ents.FindByClass("prop_portal")) do
  if string.StartWith(e:GetName(),"sourcebox_portal_") and e:IsLinked() and e:GetForward().z>0.9 then entries[#entries+1]=e end
 end
 if #entries==0 then return end
 file.Delete("sourcebox/foreign_prop_probe.txt")
 local results={}
 for _,entry in ipairs(entries) do
  local prop=ents.Create("prop_physics")
  if not IsValid(prop) then continue end
  prop:SetModel("models/props_junk/wood_crate001a.mdl") prop:SetPos(entry:GetPos()+Vector(0,0,170)) prop:Spawn()
  local ph=prop:GetPhysicsObject() if IsValid(ph) then ph:Wake() end
  local result={portal=entry:GetName(),allowed=entry:CanPort(prop),crossed=false,samples={}}
  results[#results+1]=result
  local last=prop:GetPos() local count=0
  timer.Create("SourceboxForeignPropSample"..prop:EntIndex(),0.05,100,function()
   count=count+1
   if IsValid(prop) then
    local p=prop:GetPos() local v=prop:GetVelocity()
    if p:Distance(last)>128 then result.crossed=true end
    result.samples[#result.samples+1]={position={p.x,p.y,p.z},velocity={v.x,v.y,v.z}}
    last=p
   end
   if count==100 then file.Write("sourcebox/foreign_prop_result.json",util.TableToJSON(results)) end
  end)
 end
end)

-- Opt-in momentum measurement through a real synchronized native portal pair.
timer.Create("SourceboxPlayerMomentumProbe",1,0,function()
 if not file.Exists("sourcebox/player_momentum_probe.txt","DATA") then return end
 local ply=player.GetHumans()[1] if not IsValid(ply) or not ply:Alive() then return end
 local entry
 for _,e in ipairs(ents.FindByClass("prop_portal")) do
  if string.StartWith(e:GetName(),"sourcebox_portal_") and e:IsLinked() and e:GetForward().z>0.9 and IsValid(e:GetOther()) and math.abs(e:GetOther():GetForward().z)<0.1 then entry=e break end
 end
 if not IsValid(entry) then return end
 file.Delete("sourcebox/player_momentum_probe.txt")
 local original,angles=ply:GetPos(),ply:EyeAngles()
 local exit=entry:GetOther() local normal=exit:GetForward()
 local result={entry=entry:GetName(),exit=exit:GetName(),samples={}}
 ply:SetPos(entry:GetPos()+Vector(0,0,150)) ply:SetLocalVelocity(Vector())
 local count=0
 timer.Create("SourceboxPlayerMomentumSamples",0.02,150,function()
  count=count+1
  if IsValid(ply) then
   local p=ply:GetPos() local v=ply:GetVelocity()
   result.samples[#result.samples+1]={position={p.x,p.y,p.z},velocity={v.x,v.y,v.z},outwardSpeed=v:Dot(normal),grounded=ply:OnGround()}
  end
  if count==150 then
   file.Write("sourcebox/player_momentum_result.json",util.TableToJSON(result))
   if IsValid(ply) then ply:SetPos(original) ply:SetEyeAngles(angles) ply:SetLocalVelocity(Vector()) end
  end
 end)
end)





-- Verify that opening the native menu does not pause crossplay simulation.
timer.Create("SourceboxMenuPauseProbe",1,0,function()
 if not file.Exists("sourcebox/menu_pause_probe.txt","DATA") then return end
 local p=player.GetHumans()[1] if not IsValid(p) then return end
 file.Delete("sourcebox/menu_pause_probe.txt")
 local started=SysTime() local ticks=0
 hook.Add("Think","SourceboxMenuPauseTicks",function() ticks=ticks+1 end)
 p:ConCommand("gameui_activate")
 timer.Simple(5,function()
  hook.Remove("Think","SourceboxMenuPauseTicks")
  file.Write("sourcebox/menu_pause_result.json",util.TableToJSON({elapsed=SysTime()-started,ticks=ticks,passed=ticks>20}))
  if IsValid(p) then p:ConCommand("gameui_hide") end
 end)
end)
