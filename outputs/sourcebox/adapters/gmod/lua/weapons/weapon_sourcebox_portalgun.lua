if SERVER then AddCSLuaFile() end
SWEP.Base="weapon_portalgun"
SWEP.PrintName="Sourcebox Portal Gun"
SWEP.Category="Sourcebox"
SWEP.Spawnable=true
SWEP.Slot=5
SWEP.SlotPos=2
SWEP.DrawAmmo=false
SWEP.DrawCrosshair=true
SWEP.Primary={ClipSize=-1,DefaultClip=-1,Automatic=false,Ammo="none"}
SWEP.Secondary={ClipSize=-1,DefaultClip=-1,Automatic=false,Ammo="none"}
function SWEP:PortalShot(endpoint)
 if CLIENT then return end
 local owner=self:GetOwner()
 if not IsValid(owner) or not owner:Alive() or owner:GetActiveWeapon()~=self then return end
 if SourceboxPortalRequest then SourceboxPortalRequest(owner,{type="portal_fire",endpoint=endpoint}) end
 self:SendWeaponAnim(ACT_VM_PRIMARYATTACK)
 self:SetNextPrimaryFire(CurTime()+0.3) self:SetNextSecondaryFire(CurTime()+0.3)
end
function SWEP:PrimaryAttack()
 if self:GetNWString("MapDestination","")~="" then
  if SourceboxMapTravelPlace or CLIENT then
   if SERVER then SourceboxMapTravelPlace(self:GetOwner()) end
   self:SetNextPrimaryFire(CurTime()+0.5) return
  end
  -- A disabled experiment must not leave an equipped weapon unable to fire.
  self:SetNWString("MapDestination","") self:SetNWString("MapLabel","")
 end
 self:PortalShot(0)
end
function SWEP:SecondaryAttack()
 if self:GetNWString("MapDestination","")~="" then self:PrimaryAttack() return end
 self:PortalShot(1)
end
function SWEP:Reload()
 if self:GetOwner():KeyDown(IN_USE) or game.GetMap()~="sourcebox_room_001" or (SourceboxWorlds and SourceboxWorlds.playerWorld~="sourcebox_room_001") then
  if SERVER and (self.SourceboxMenuAt or 0)<CurTime() then
   self.SourceboxMenuAt=CurTime()+0.7
   if SourceboxMapTravelMenu then SourceboxMapTravelMenu(self:GetOwner())
   else self:GetOwner():ChatPrint("Map destinations are disabled while world loading is being repaired. Left/right fire still place shared-room portals.") end
  end
  return
 end
 if self:GetNWString("MapDestination","")~="" then
  if SERVER then self:SetNWString("MapDestination","") self:SetNWString("MapLabel","") end return
 end
 if SERVER and (self.SourceboxClearAt or 0)<CurTime() then
  self.SourceboxClearAt=CurTime()+0.5
  if SourceboxPortalRequest then SourceboxPortalRequest(self:GetOwner(),{type="portal_clear"}) end
 end
end
