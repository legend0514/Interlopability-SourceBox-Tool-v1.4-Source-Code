local P={seen={},ack={},revision=-1,markers={}}
local successor=include("sourcebox/portal-successor.lua")
local function vec(a) return Vector(a[1],a[2],a[3]) end
function P:Receive(data,physics)
 local nextSession=data.connected and data.coreSession or nil
 if self.session~=nextSession then
  successor:Clear()
  self.session=nextSession self.seen={} self.ack={} self.revision=-1
  for _,ent in pairs(self.markers) do if IsValid(ent) then ent:Remove() end end self.markers={}
 end
 if not self.session then return end
 if data.portalsEnabled then local ply=player.GetHumans()[1] if IsValid(ply) and ply:Alive() and not ply:HasWeapon("weapon_sourcebox_portalgun") then ply:Give("weapon_sourcebox_portalgun") end end
 local snapshot=data.portals
 local repair=false
 if snapshot and SysTime()>=(self.nextRepairCheck or 0) then
  self.nextRepairCheck=SysTime()+1
  repair=successor:NeedsRepair(snapshot)
 end
 if snapshot and (snapshot.revision>self.revision or repair) then
  if successor:Receive(snapshot,physics) then self.revision=snapshot.revision end
 end
 for _,message in ipairs(data.portalEvents or {}) do
  if not self.seen[message.event] then
   self.seen[message.event]=true self.ack[#self.ack+1]=message.event
   if #self.ack>128 then self.seen[table.remove(self.ack,1)]=nil end
   if message.type=="portal_notice" then physics:Notice(message.reason)
   elseif message.type=="portal_traverse" and message.session==self.session then
    -- The successor owns native GMod traversal. Reject stale core requests.
    physics:Command({type="portal_ack",session=self.session,event=message.event,applied=false})
   end
  end
 end
end
return P
