-- Destination mode links two live native portals inside the same host map.
if SERVER then AddCSLuaFile() end
if game.GetMap()~="sourcebox_room_001" then return end
local worldConfig=util.JSONToTable(file.Read("sourcebox/worlds.json","DATA") or "{}") or {}
if worldConfig.enabled~=true then return end
if CLIENT then
 net.Receive("SourceboxMapMenu",function()
  local list=net.ReadTable()
  local frame=vgui.Create("DFrame") frame:SetSize(430,330) frame:Center() frame:SetTitle("Portal Gun — destination world") frame:MakePopup()
  local label=vgui.Create("DLabel",frame) label:Dock(TOP) label:SetTall(50) label:SetWrap(true) label:SetText("Choose a world, then fire at a wall. The portal looks into that world; walk through to enter it.")
  local scroll=vgui.Create("DScrollPanel",frame) scroll:Dock(FILL)
  for _,d in ipairs(list) do
   local b=scroll:Add("DButton") b:Dock(TOP) b:DockMargin(0,0,0,7) b:SetTall(38) b:SetEnabled(d.ready) b:SetText(d.label..(d.ready and "" or " — preparing"))
   b.DoClick=function() net.Start("SourceboxMapSelect") net.WriteString(d.id) net.SendToServer() frame:Close() end
  end
 end)
 hook.Add("HUDPaint","SourceboxWorldGunHint",function()
  local ply=LocalPlayer() if not IsValid(ply) then return end
  local gun=ply:GetActiveWeapon() if not IsValid(gun) or gun:GetClass()~="weapon_sourcebox_portalgun" then return end
  local label=gun:GetNWString("MapLabel","")
  draw.SimpleText(label~="" and ("Destination: "..label.." | Fire: place portal | E + R: worlds") or "E + R: destination worlds", "DermaDefaultBold",ScrW()/2,ScrH()-90,Color(180,235,255),TEXT_ALIGN_CENTER)
 end)
 return
end
if not game.SinglePlayer() then return end
util.AddNetworkString("SourceboxMapMenu") util.AddNetworkString("SourceboxMapSelect")
local pair={}
timer.Create("SourceboxPerformanceRefreshProbe",1,0,function()
 if not file.Exists("sourcebox/refresh-performance.txt","DATA") then return end
 local ply=player.GetHumans()[1] if not IsValid(ply) then return end
 file.Delete("sourcebox/refresh-performance.txt")
 ply:SendLua('include("autorun/client/sourcebox_performance.lua")')
end)
for n=1,2 do pair[n]=ents.FindByName("sourcebox_world_portal_"..n)[1] end
local function ready(id) local w=SourceboxWorlds return w and (id=="sourcebox_room_001" or (w.ready[id] and w.clientReady[id])) end
local function availableWorlds()
 local w=SourceboxWorlds local ids={}
 if not w then return ids end
 for _,d in ipairs(w.config.worlds) do if d.id~=w.playerWorld and ready(d.id) then ids[#ids+1]=d.id end end
 return ids
end
local function gunFor(ply)
 if not IsValid(ply) or not ply:Alive() or ply~=player.GetHumans()[1] then return end
 local gun=ply:GetActiveWeapon() if IsValid(gun) and gun:GetClass()=="weapon_sourcebox_portalgun" then return gun end
end
SourceboxMapTravelMenu=function(ply)
 if not gunFor(ply) or not SourceboxWorlds then return end
 local list={{id="",label="Ordinary portals",ready=true},{id="sourcebox_room_001",label="Shared Room",ready=true}}
 list[#list+1]={id="__random",label="Random destination",ready=#availableWorlds()>0}
 for _,d in ipairs(SourceboxWorlds.config.worlds) do list[#list+1]={id=d.id,label=d.label,ready=ready(d.id)==true} end
 net.Start("SourceboxMapMenu") net.WriteTable(list) net.Send(ply)
end
local function selectDestination(ply,id)
 local gun=gunFor(ply) local w=SourceboxWorlds if not gun or not w then return false end
 if id=="" then gun:SetNWString("MapDestination","") gun:SetNWString("MapLabel","") return true end
 if id=="__random" then
  if #availableWorlds()==0 then ply:ChatPrint("No other world is ready yet.") return false end
  gun:SetNWString("MapDestination",id) gun:SetNWString("MapLabel","Random destination") return true
 end
 if not ready(id) then ply:ChatPrint("That world's geometry is still preparing.") return false end
 if id==w.playerWorld then ply:ChatPrint("You're already in that world.") return false end
 local label=id=="sourcebox_room_001" and "Shared Room" or w.data[id].label
 gun:SetNWString("MapDestination",id) gun:SetNWString("MapLabel",label) ply:ChatPrint("Portal destination: "..label) return true
end
net.Receive("SourceboxMapSelect",function(_,ply)
 if (ply.SourceboxMapSelectAt or 0)>CurTime() then return end ply.SourceboxMapSelectAt=CurTime()+0.3 selectDestination(ply,net.ReadString())
end)
-- Destination pairs are owned by the shared service, just like ordinary portals.
local function canonicalPair()
 pair={ents.FindByName("sourcebox_portal_gmod_1")[1],ents.FindByName("sourcebox_portal_gmod_2")[1]}
 return pair
end
timer.Create("SourceboxWorldCanonicalPair",0.1,0,canonicalPair)
SourceboxMapTravelPlace=function(ply)
 local gun=gunFor(ply) local w=SourceboxWorlds if not gun or not w then return end
 local id=gun:GetNWString("MapDestination","")
 if id=="__random" then
  local choices=availableWorlds() if #choices==0 then return end
  id=choices[math.random(#choices)]
 end
 if not ready(id) or id==w.playerWorld then return end
 if not SourceboxPortalRequest then return end
 SourceboxPortalRequest(ply,{type="portal_fire",endpoint=0,destination=id})
 gun:SetNWString("ResolvedWorld",id)
 if gun:GetNWString("MapDestination","")=="__random" then gun:SetNWString("MapLabel","Random: "..w.data[id].label) end
 file.Append("sourcebox/world-events-server.txt",util.TableToJSON({event="world_portal_requested",destination=id,nativeMap=game.GetMap(),time=os.time()}).."\n")
end
-- Opt-in native test: real SWEP placement and movement through the real portal.
timer.Create("SourceboxWorldReturnAcceptance",1,0,function()
 if not file.Exists("sourcebox/world-return-probe.txt","DATA") then return end
 local ply=player.GetHumans()[1] local back=pair[2]
 if not IsValid(ply) or not IsValid(back) then return end
 file.Delete("sourcebox/world-return-probe.txt")
 ply:SetPos(back:GetPos()+back:GetForward()*100-Vector(0,0,64))
 ply:SetEyeAngles((-back:GetForward()):Angle())
 ply:ConCommand("jpeg sourcebox_world_return_preview")
 local untilTime=CurTime()+3
 hook.Add("StartCommand","SourceboxWorldReturnWalk",function(p,cmd)
  if p~=ply then return end
  if CurTime()>untilTime or SourceboxWorlds.playerWorld=="sourcebox_room_001" then cmd:SetForwardMove(0) hook.Remove("StartCommand","SourceboxWorldReturnWalk") return end
  cmd:SetViewAngles((-back:GetForward()):Angle()) cmd:SetForwardMove(250) cmd:SetSideMove(0)
 end)
 timer.Simple(4,function() if IsValid(ply) then ply:ConCommand("jpeg sourcebox_world_return_arrival") end end)
end)
timer.Create("SourceboxWorldAcceptance",1,0,function()
 if not file.Exists("sourcebox/world-probe.txt","DATA") then return end
 local id=string.Trim(file.Read("sourcebox/world-probe.txt","DATA") or "") local ply=player.GetHumans()[1]
 if (id~="__random" and not ready(id)) or not IsValid(ply) or not ply:Alive() then return end
 if SourceboxWorlds.playerWorld~="sourcebox_room_001" then return end
 file.Delete("sourcebox/world-probe.txt") ply:SelectWeapon("weapon_sourcebox_portalgun")
 timer.Simple(1,function()
  if not selectDestination(ply,id) then return end
  if SourceboxWorlds.playerWorld~="sourcebox_room_001" then return end
  ply:SetPos(Vector(200,-320,1)) ply:SetEyeAngles(Angle(0,0,0))
  timer.Simple(0.5,function()
   if not IsValid(ply) then return end
   ply:GetActiveWeapon():PrimaryAttack()
   local resolved=ply:GetActiveWeapon():GetNWString("ResolvedWorld",id)
   local entry=pair[1] if not IsValid(entry) then return end
   ply:SetPos(entry:GetPos()+entry:GetForward()*100-Vector(0,0,64)) ply:SetEyeAngles((-entry:GetForward()):Angle())
   timer.Simple(1,function()
    if not IsValid(ply) or not IsValid(entry) then return end
    ply:ConCommand("jpeg sourcebox_world_preview")
    local untilTime=CurTime()+2
    hook.Add("StartCommand","SourceboxWorldWalk",function(p,cmd)
     if p~=ply then return end
     if CurTime()>untilTime or SourceboxWorlds.playerWorld==resolved then cmd:SetForwardMove(0) hook.Remove("StartCommand","SourceboxWorldWalk") return end
     cmd:SetViewAngles((-entry:GetForward()):Angle()) cmd:SetForwardMove(250) cmd:SetSideMove(0)
    end)
   end)
   timer.Simple(4,function() if IsValid(ply) then ply:ConCommand("jpeg sourcebox_world_arrival") end end)
  end)
 end)
end)
