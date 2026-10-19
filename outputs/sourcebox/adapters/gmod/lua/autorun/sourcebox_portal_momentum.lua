if SERVER then AddCSLuaFile() end
if game.GetMap()~="sourcebox_room_001" then return end
-- Preserve the successor's collision handling and input acceleration. Its Move
-- hook applies extra horizontal friction even during an airborne wall exit.
-- Correct only that exact loss, not collision-induced velocity changes.
timer.Create("SourceboxPortalMomentumHook",1,30,function()
 local original=(hook.GetTable().Move or {}).hpdMoveHook
 if not original then return end
 timer.Remove("SourceboxPortalMomentumHook")
 hook.Add("Move","hpdMoveHook",function(ply,mv)
  local portal=ply.InPortal
  local normal=IsValid(portal) and portal:GetNWBool("SourceboxPortal",false) and portal:GetForward() or nil
  local before=normal and mv:GetVelocity():Dot(normal) or 0
  local factor=0.98-FrameTime()*5
  local result=original(ply,mv)
  if normal and math.abs(normal.z)<0.01 and before>100 and factor>0 then
   local velocity=mv:GetVelocity()
   local after=velocity:Dot(normal)
   if math.abs(after-before*factor)<0.5 then
    mv:SetVelocity(velocity+normal*(before-after))
   end
  end
  return result
 end)
end)
