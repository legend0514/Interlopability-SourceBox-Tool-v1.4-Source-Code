if game.GetMap()~="sourcebox_room_001" then return end
local settings=(util.JSONToTable(file.Read("sourcebox/gmod.json","DATA") or "{}") or {}).performance or {}
local previewCalls,previewSeconds=0,0
local function apply()
 if not GetConVar("portal_rendertexture_scale") then return false end
 local stored=scripted_ents.GetStored("prop_portal")
 if not stored or not stored.t.RenderPortal then return false end
 -- The dependency globally disables PVS and forces maximum-detail models.
 -- That draws every imported map in this host, even with no portal visible.
 RunConsoleCommand("r_novis","0")
 RunConsoleCommand("r_lod","-1")
 do
  local original=stored.t.SourceboxOriginalRender or stored.t.RenderPortal
  stored.t.SourceboxOriginalRender=original
  stored.t.SourceboxRenderBudget=true
  stored.t.RenderPortal=function(portal,origin,angles)
   if RENDERING_PORTAL then return end
   -- Render only apertures intersecting the current camera frustum. Keep the
   -- native mask and traversal code, including views of remote players.
   local delta=portal:GetPos()-origin
   local radius=128
   local forward=delta:Dot(angles:Forward())
   if forward < -radius then return end
   local view=render.GetViewSetup(true)
   local half=math.tan(math.rad((view.fov or 90)*0.5))
   local aspect=(view.width or ScrW())/math.max(1,view.height or ScrH())
   if math.abs(delta:Dot(angles:Right()))>math.max(0,forward)*half+radius then return end
   if math.abs(delta:Dot(angles:Up()))>math.max(0,forward)*half/aspect+radius then return end
   local now=SysTime()
   if now<(portal.SourceboxNextPreview or 0) then return end
   portal.SourceboxNextPreview=now+1/math.Clamp(settings.portalPreviewHz or 30,10,60)
   local started=SysTime()
   original(portal,origin,angles)
   previewCalls=previewCalls+1 previewSeconds=previewSeconds+SysTime()-started
  end
 end
 -- These addon convars are archived. Leave resolution and lighting under the
 -- player's control: changing them here also changes normal GMod sessions.
 -- Keep destination images, player rendering, and all portal pairs enabled.
 print("[Sourcebox] Portal preview resolution scale "..GetConVar("portal_rendertexture_scale"):GetString())
 return true
end
timer.Create("SourceboxPortalPerformanceSetup",1,30,function()
 if apply() then timer.Remove("SourceboxPortalPerformanceSetup") end
end)
local samples,total,worst=0,0,0
local previous=SysTime()
hook.Add("Think","SourceboxFrameTiming",function()
 local current=SysTime() local dt=current-previous previous=current
 samples=samples+1 total=total+dt worst=math.max(worst,dt)
end)
timer.Create("SourceboxFrameTimingReport",10,0,function()
 if samples>0 then
  local classes={} local entities=ents.GetAll()
  for _,entity in ipairs(entities) do local class=entity:GetClass() classes[class]=(classes[class] or 0)+1 end
  file.Write("sourcebox/entity-timing.json",util.TableToJSON({count=#entities,classes=classes,luaKB=collectgarbage("count"),time=SysTime()}))
  local cvars={}
  for _,name in ipairs({"r_novis","r_lod","engine_no_focus_sleep","host_sleep","host_timescale","host_framerate","mat_picmip","mat_vsync","mat_queue_mode","gmod_mcore_test","mat_fullbright","mat_hdr_level","mat_autoexposure_min","mat_autoexposure_max","mat_force_tonemap_scale","mat_bloomscale","r_dynamic"}) do local c=GetConVar(name) cvars[name]=c and c:GetString() or "unavailable" end
  file.Write("sourcebox/performance-cvars.json",util.TableToJSON(cvars))
  local fps=GetConVar("fps_max") local background=GetConVar("fps_max_nofocus")
  file.Write("sourcebox/frame-timing.json",util.TableToJSON({samples=samples,averageMs=total/samples*1000,worstMs=worst*1000,previewCalls=previewCalls,previewMs=previewSeconds*1000,measuredAt=SysTime(),focus=system.HasFocus(),fpsMax=fps and fps:GetString() or "unavailable",backgroundFps=background and background:GetString() or "unavailable"}))
 end
 samples,total,worst=0,0,0
 previewCalls,previewSeconds=0,0
end)
