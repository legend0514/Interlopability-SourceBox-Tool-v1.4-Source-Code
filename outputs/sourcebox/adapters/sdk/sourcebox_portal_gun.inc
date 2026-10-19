#ifdef HL2_DLL
// A separate inventory item using the SDK's existing networked weapon base.
class CSourceboxPortalGun : public CWeaponCubemap
{
public:
 DECLARE_CLASS(CSourceboxPortalGun,CWeaponCubemap);
 void PrimaryAttack() OVERRIDE {SendWeaponAnim(ACT_VM_PRIMARYATTACK);m_flNextPrimaryAttack=gpGlobals->curtime+0.3f;}
 void SecondaryAttack() OVERRIDE {SendWeaponAnim(ACT_VM_PRIMARYATTACK);m_flNextSecondaryAttack=gpGlobals->curtime+0.3f;}
 bool Reload() OVERRIDE {return false;}
};
LINK_ENTITY_TO_CLASS(weapon_sourcebox_portalgun,CSourceboxPortalGun);
#endif
