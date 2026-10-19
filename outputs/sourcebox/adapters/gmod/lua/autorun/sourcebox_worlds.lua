-- Real BSP surfaces rendered and collided inside one native GMod map.
if SERVER then AddCSLuaFile() end
if game.GetMap()~="sourcebox_room_001" then return end
local cfg=util.JSONToTable(file.Read("sourcebox/worlds.json","DATA") or "{}") or {}
if cfg.enabled~=true then
 if SERVER then file.Write("sourcebox/world.json",util.TableToJSON({map=game.GetMap(),nativeMap=game.GetMap(),transitioning=false,time=os.time()})) end
 return
end
SourceboxWorlds={data={},ready={},clientReady={},playerWorld="sourcebox_room_001",config=cfg}
local W=SourceboxWorlds
local function vec(a) return Vector(a[1],a[2],a[3]) end
local function log(e) e.time=os.time() file.Append("sourcebox/world-events-"..(SERVER and "server" or "client")..".txt",util.TableToJSON(e).."\n") end
local collisionClass="sourcebox_world_collision"
local ENT={Type="anim",Base="base_anim",Spawnable=false}
function ENT:SetupDataTables() self:NetworkVar("String",0,"WorldId") self:NetworkVar("Int",0,"ChunkId") end
function ENT:UpdateTransmitState() return TRANSMIT_ALWAYS end
function ENT:Initialize()
 if SERVER then self:SetModel("models/hunter/blocks/cube025x025x025.mdl") self:SetMoveType(MOVETYPE_NONE) end
 self.NextBuild=0
end
function ENT:BuildCollision()
 local world=W.data[self:GetWorldId()] local chunk=world and world.collision[self:GetChunkId()]
 if not chunk then return false end
 local points={} local origin=self:GetPos()
 for _,v in ipairs(chunk.vertices) do points[#points+1]=Vector(v[1],v[2],v[3])-origin end
 if not self:PhysicsFromMesh(points) then log({event="collision_failed",world=self:GetWorldId(),chunk=self:GetChunkId()}) self.Failed=true return false end
 self:EnableCustomCollisions(true) self:SetSolid(SOLID_VPHYSICS) self:SetMoveType(MOVETYPE_NONE)
 local ph=self:GetPhysicsObject() if IsValid(ph) then ph:SetPos(self:GetPos()) ph:SetAngles(self:GetAngles()) ph:EnableMotion(false) end
 self.Built=true return true
end
function ENT:Think()
 if not self.Built and not self.Failed then self:BuildCollision() end
 -- These meshes never move. Updating all their physics transforms each frame
 -- rebuilds broadphase state even while the player is in a different world.
 if CLIENT then self:SetNextClientThink((self.Built or self.Failed) and -1 or CurTime()+0.5)
 else self:NextThink(CurTime()+((self.Built or self.Failed) and 60 or 0.5)) end
 return true
end
if CLIENT then function ENT:Draw() end end
scripted_ents.Register(ENT,collisionClass)
if SERVER then
 util.AddNetworkString("SourceboxWorldReady")
 net.Receive("SourceboxWorldReady",function(_,ply) local id=net.ReadString() if ply==player.GetHumans()[1] and W.data[id] then W.clientReady[id]=true end end)
end
local queue={}
for _,d in ipairs(cfg.worlds or {}) do
 local text=file.Read("sourcebox/worlds/"..d.id..(cfg.nativeGeometry and ".metadata.json" or ".json"),"DATA")
 local world=text and util.JSONToTable(text,true)
 if world and world.id==d.id and istable(world.chunks) then
  W.data[d.id]=world world.label=d.label world.meshes={} world.total=SERVER and #world.collision or #world.chunks world.built=0
  if not cfg.nativeGeometry then
   for n,chunk in ipairs(SERVER and world.collision or world.chunks) do queue[#queue+1]={id=d.id,n=n} end
  end
 else log({event="world_parse_failed",world=d.id}) end
end
local materials={}
local function build(item)
 local world=W.data[item.id] local chunk=(SERVER and world.collision or world.chunks)[item.n]
 if SERVER then
  local center=Vector() for _,v in ipairs(chunk.vertices) do center=center+Vector(v[1],v[2],v[3]) end center=center/#chunk.vertices
  local e=ents.Create(collisionClass) if not IsValid(e) then return false end
  e:SetName("sourcebox_world_"..item.id.."_"..item.n) e:SetPos(center) e:SetWorldId(item.id) e:SetChunkId(item.n) e:Spawn()
  return e:BuildCollision()
 else
  local mat=materials[chunk.material]
  if not mat then
   local original=Material(chunk.material) local texture=original:GetTexture("$basetexture")
   mat=CreateMaterial("sourcebox_world_"..util.CRC(chunk.material),"UnlitGeneric",{["$basetexture"]=texture and texture:GetName() or "debug/debugempty",["$nocull"]="1",["$vertexcolor"]="1"}) materials[chunk.material]=mat
   if original:IsError() then log({event="missing_material",material=chunk.material}) end
  end
  local vertices={}
  for _,v in ipairs(chunk.vertices) do vertices[#vertices+1]={pos=Vector(v[1],v[2],v[3]),normal=Vector(v[4],v[5],v[6]),u=v[7],v=v[8],color=Color(255,255,255)} end
  local mesh=Mesh(mat) mesh:BuildFromTriangles(vertices) world.meshes[#world.meshes+1]={mesh=mesh,material=mat}
  return true
 end
end
local nextItem=1
hook.Add("InitPostEntity","SourceboxWorldStart",function() timer.Simple(1,function()
 if cfg.nativeGeometry then
  if SERVER and not IsValid(ents.FindByName("sourcebox_worlds_baked")[1]) then log({event="native_world_marker_missing"}) return end
  for id in pairs(W.data) do
   W.ready[id]=true
   if CLIENT then net.Start("SourceboxWorldReady") net.WriteString(id) net.SendToServer() end
   log({event="world_ready",world=id,backend="native_bsp"})
  end
 end
 W.started=true
end) end)
hook.Add("Think","SourceboxWorldBuild",function()
 if not W.started then return end
 -- Spread native physics and GPU uploads over frames instead of freezing all clients.
 local deadline=SysTime()+0.004
 while queue[nextItem] and SysTime()<deadline do
  local item=queue[nextItem] nextItem=nextItem+1
  local ok,result=xpcall(function() return build(item) end,debug.traceback)
  local world=W.data[item.id]
  if item.n%20==0 then log({event="world_build_progress",world=item.id,chunk=item.n,total=world.total}) end
  if ok and result then world.built=world.built+1 else world.failed=true log({event="world_build_failed",world=item.id,error=tostring(result)}) end
  if world.built==world.total then
   W.ready[item.id]=true log({event="world_ready",world=item.id,chunks=world.total,triangles=world.stats.triangles})
   if CLIENT then net.Start("SourceboxWorldReady") net.WriteString(item.id) net.SendToServer() end
  end
 end
 if not queue[nextItem] then hook.Remove("Think","SourceboxWorldBuild") end
end)
local function contains(world,p,below,above)
 local lo,hi=world.bounds[1],world.bounds[2]
 return p.x>=lo[1]-300 and p.x<=hi[1]+300 and p.y>=lo[2]-300 and p.y<=hi[2]+300 and p.z>=lo[3]-below and p.z<=hi[3]+above
end
if CLIENT then
 local nextDiagnostic=0
 hook.Add("PostDrawTranslucentRenderables","SourceboxWorldDraw",function(depth,sky)
  if sky then return end
  local view=render.GetViewSetup(true) local pos=EyePos()
  if IsValid(RENDERING_PORTAL) and IsValid(RENDERING_PORTAL:GetOther()) then pos=RENDERING_PORTAL:GetOther():GetPos() end
  -- Worlds can share a height band (Construct, 2Fort and Frenzy).
  -- Select with all three coordinates, including nested portal cameras.
  local drawn=0
  for id,world in pairs(W.data) do
   if contains(world,pos,300,1000) then
    for _,part in ipairs(world.meshes) do render.SetMaterial(part.material) part.mesh:Draw() drawn=drawn+1 end
   end
  end
  if SysTime()>nextDiagnostic then
   nextDiagnostic=SysTime()+2
   file.Write("sourcebox/world-render.json",util.TableToJSON({eye=tostring(EyePos()),selected=tostring(pos),view=tostring(view and view.origin),drawn=drawn,portal=IsValid(RENDERING_PORTAL),time=SysTime()}))
  end
 end)
 hook.Add("ShutDown","SourceboxWorldDestroy",function() for _,world in pairs(W.data) do for _,part in ipairs(world.meshes) do part.mesh:Destroy() end end end)
else
 local function publish()
  local ply=player.GetHumans()[1]
  if IsValid(ply) then
   local p=ply:GetPos()
   if math.abs(p.x)<600 and math.abs(p.y)<600 and p.z>=-100 and p.z<512 then W.playerWorld="sourcebox_room_001"
   else
    for id,world in pairs(W.data) do
     if contains(world,p,250,1000) then W.playerWorld=id break end
    end
   end
  end
  local ready={} for id in pairs(W.ready) do if W.clientReady[id] then ready[#ready+1]=id end end
  file.Write("sourcebox/world.json",util.TableToJSON({map=W.playerWorld,nativeMap=game.GetMap(),ready=ready,transitioning=false,time=os.time()}))
 end
 publish() timer.Create("SourceboxWorldIdentity",0.2,0,publish)
 timer.Create("SourceboxWorldCollisionDiagnostic",2,0,function()
  local result={}
  for id,world in pairs(W.data) do
   local s=vec(world.spawn)
   local tr=util.TraceLine({start=s+Vector(0,0,64),endpos=s-Vector(0,0,128),mask=MASK_PLAYERSOLID})
   result[id]={hit=tr.Hit,position=tostring(tr.HitPos),normal=tostring(tr.HitNormal),entity=IsValid(tr.Entity) and tr.Entity:GetClass() or "world"}
  end
  file.Write("sourcebox/world-collision.json",util.TableToJSON(result))
 end)
 hook.Add("PlayerSpawn","SourceboxWorldSpawn",function(ply) W.playerWorld="sourcebox_room_001" publish() end)
 timer.Create("SourceboxWorldFallRecovery",1,0,function()
  local world=W.data[W.playerWorld] local ply=player.GetHumans()[1]
  if not IsValid(ply) or not ply:Alive() or ply:GetMoveType()==MOVETYPE_NOCLIP then return end
  local pos=ply:GetPos()
  local inside=math.abs(pos.x)<600 and math.abs(pos.y)<600 and pos.z>=-100 and pos.z<512
  for _,candidate in pairs(W.data) do if contains(candidate,pos,250,1000) then inside=true break end end
  if inside then ply.SourceboxOutsideSince=nil return end
  ply.SourceboxOutsideSince=ply.SourceboxOutsideSince or SysTime()
  if SysTime()-ply.SourceboxOutsideSince<2 then return end
  log({event="out_of_world_recovery",world=W.playerWorld,position=tostring(pos)})
  -- A destination spawn can itself be occupied by several portal exits.
  -- Recover to the known clear hub spawn rather than trigger another transfer.
  ply:SetPos(Vector(-128,128,8)) W.playerWorld="sourcebox_room_001"
  ply:SetLocalVelocity(Vector()) ply:SetGroundEntity(NULL)
  ply.SourceboxPortalCooldown=SysTime()+1 ply.SourceboxOutsideSince=nil
 end)
end

