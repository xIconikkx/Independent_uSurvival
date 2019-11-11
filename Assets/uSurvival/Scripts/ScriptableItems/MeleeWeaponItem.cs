// Axes, baseball bats, etc.
using System.Text;
using UnityEngine;
using Mirror;

[CreateAssetMenu(menuName="uSurvival Item/Weapon(Melee)", order=999)]
public class MeleeWeaponItem : WeaponItem
{
    public float sphereCastRadius = 0.5f; // don't make it too big or it will hit the floor first!

    // usage
    public override Usability CanUse(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // check base usability first (cooldown etc.)
        Usability baseUsable = base.CanUse(hotbar, hotbarIndex, lookAt);
        if (baseUsable != Usability.Usable)
            return baseUsable;

        // not reloading?
        return hotbar.GetComponent<PlayerReloading>().ReloadTimeRemaining() > 0
               ? Usability.Cooldown
               : Usability.Usable;
    }

    Health SphereCastToLookAt(GameObject player, Collider collider, Vector3 lookAt, out RaycastHit hit)
    {
        // get PlayerLook component to access the physics raycast layers for the
        // player (e.g. don't Raycast against AggroArea etc.)
        PlayerLook look = player.GetComponent<PlayerLook>();

        // spherecast to find out what we hit
        // -> ignore self just to be sure
        // -> based on collider.gameObject in case collider is on pelvis etc.
        // -> raycast is too small, e.g. if we aim above a monster's shoulder
        //    then we miss, which feels awful. hence spherecast. we need a
        //    'thick' raycast, which is what spherecast is meant for
        // -> bounds.center works best as origin, since that's where the axe is.
        //    using the head as origin makes it difficult to hit smaller enemies
        // -> spherecast ignores colliders that it starts in, it only uses the
        //    ones that it collides with after moving forward. so if we are
        //    very close to an enemy, spherecast wouldn't work. the solution is
        //    to start sphere casting slightly behind us.
        //    (depending on sphereCastRadius, this might be a quite a bit behind
        //     but it does work perfectly and does not detect enemies behind us)
        //    (literally behind us. not behind look at direction, as this might
        //     be our feet if we look upwards, then the sphere would start in
        //     and ignore the wall in front of us, allowing us to hit enemies
        //     behind walls)
        // -> we use range + sphereCastRadius because we start a bit behind
        // -> we do NOT use a layer mask, because we should cast against everything.
        //    if there's a door between us and the monster, we shouldn't hit it.
        Vector3 origin = collider.bounds.center;
        Vector3 behindOrigin = origin - player.transform.forward * sphereCastRadius;
        Vector3 direction = (lookAt - origin).normalized;
        Debug.DrawLine(behindOrigin, lookAt, Color.red, 1);
        if (Utils.SphereCastWithout(behindOrigin, sphereCastRadius, direction, out hit, attackRange + sphereCastRadius, player, look.raycastLayers))
        {
            // show ray for debugging
            Debug.DrawLine(behindOrigin, hit.point, Color.cyan, 1);
            Debug.DrawLine(hit.point, hit.point + hit.normal, Color.blue, 1);

            // hit anything living?
            return hit.transform.GetComponent<Health>();
        }
        return null;
    }

    public override void Use(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // call base function to start cooldown
        base.Use(hotbar, hotbarIndex, lookAt);

        // can we hit anything living?
        Combat combat = hotbar.GetComponent<Combat>();
        RaycastHit hit;
        Health enemyHealth = SphereCastToLookAt(hotbar.gameObject, hotbar.GetComponent<CapsuleCollider>(), lookAt, out hit);
        if (enemyHealth != null)
        {
            // deal damage
            combat.DealDamageAt(enemyHealth.gameObject, combat.damage + damage, hit.point, hit.normal, hit.collider);

            // reduce durability only if we hit something
            // (an axe doesn't lose durability if we swing it in the air)
            // (slot might be invalid in case of hands)
            ItemSlot slot = hotbar.slots[hotbarIndex];
            if (slot.amount > 0)
            {
                slot.item.durability = Mathf.Max(slot.item.durability - 1, 0);
                hotbar.slots[hotbarIndex] = slot;
            }
            else Debug.Log("ignore slot: " + hotbarIndex);
        }
    }

    public override void OnUsed(PlayerHotbar hotbar, Vector3 lookAt)
    {
        // find out what we hit by simulating it again to decide which sound to play
        RaycastHit hit;
        Health enemyHealth = SphereCastToLookAt(hotbar.gameObject, hotbar.GetComponent<CapsuleCollider>(), lookAt, out hit);
        if (enemyHealth != null)
        {
            if (successfulUseSound) hotbar.audioSource.PlayOneShot(successfulUseSound);
        }
        else
        {
            if (failedUseSound) hotbar.audioSource.PlayOneShot(failedUseSound);
        }
    }
}
