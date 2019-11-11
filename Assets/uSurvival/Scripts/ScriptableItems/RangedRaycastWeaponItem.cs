// Ranged weapons that raycast instead of using projectiles. E.g. pistols, guns.
using UnityEngine;

[CreateAssetMenu(menuName="uSurvival Item/Weapon(Ranged Raycast)", order=999)]
public class RangedRaycastWeaponItem : RangedWeaponItem
{
    public override void Use(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        Combat combat = hotbar.GetComponent<Combat>();

        // raycast to find out what we hit
        if (RaycastToLookAt(hotbar.gameObject, lookAt, out RaycastHit hit))
        {
            // hit anything living? then deal damage
            Health enemyHealth = hit.transform.GetComponent<Health>();
            if (enemyHealth)
            {
                combat.DealDamageAt(enemyHealth.gameObject, damage, hit.point, hit.normal, hit.collider);
            }
        }

        // base logic (decrease ammo and durability)
        base.Use(hotbar, hotbarIndex, lookAt);
    }
}
