local M={seen={},order={},bodies={}}
function M:Receive(data,proxies,ply)
 for _,m in ipairs(data.deathBodies or {}) do
  if not self.seen[m.event] then
   self.seen[m.event]=true table.insert(self.order,m.event)
   if #self.order>128 then self.seen[table.remove(self.order,1)]=nil end
   local source=m.game=="gmod" and ply or proxies[m.game]
   if IsValid(source) and m.state then
    local model=source:GetModel()
    local prepared=m.game=="hl2" and "models/sourcebox/ragdolls/hl2.mdl" or m.game=="portal2" and "models/sourcebox/ragdolls/chell.mdl"
    if prepared and util.IsValidRagdoll(prepared) then model=prepared end
    -- Imported models without a physics skeleton must not become fake rigid corpses.
    if util.IsValidRagdoll(model) then
     local body=ents.Create("prop_ragdoll")
     if IsValid(body) then
      local s=m.state
      body:SetModel(model) body:SetSkin(source:GetSkin())
      body:SetPos(Vector(unpack(s.position))) body:SetAngles(Angle(0,s.rotation[2],0))
      body:Spawn() body:SetCollisionGroup(COLLISION_GROUP_DEBRIS)
      body.SourceboxCosmeticRagdoll=true
      timer.Simple(0.2,function()
       file.Write("sourcebox/ragdoll-result.json",util.TableToJSON({event=m.event,game=m.game,model=model,valid=IsValid(body),physicsBones=IsValid(body) and body:GetPhysicsObjectCount() or 0,time=os.time()},true))
      end)
      local velocity=Vector(unpack(s.velocity))
      for i=0,body:GetPhysicsObjectCount()-1 do local p=body:GetPhysicsObjectNum(i) if IsValid(p) then p:SetVelocity(velocity) end end
      table.insert(self.bodies,body)
      while #self.bodies>m.maxBodies do local old=table.remove(self.bodies,1) if IsValid(old) then old:Remove() end end
      timer.Simple(m.lifetimeMs/1000,function() if IsValid(body) then body:Remove() end end)
     end
    else print("[Sourcebox] Ragdoll physics unavailable for "..tostring(model)) end
   end
  end
 end
end
return M
