// Guns, bows, etc.
using System.Text;
using UnityEngine;
using Mirror;

public abstract class RangedWeaponItem : WeaponItem
{
    public AmmoItem requiredAmmo;
    public int magazineSize = 20;
    public float reloadTime = 1;
    public AudioClip reloadSound;
    public float zoom = 20;
    public GameObject decalPrefab;
    public float decalOffset = 0.01f;

    [Header("Recoil")]
    [Range(0, 30)] public float recoilHorizontal;
    [Range(0, 30)] public float recoilVertical;

    // usage
    public override Usability CanUse(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // check base usability first (cooldown etc.)
        Usability baseUsable = base.CanUse(hotbar, hotbarIndex, lookAt);
        if (baseUsable != Usability.Usable)
            return baseUsable;

        // reloading?
        if (hotbar.GetComponent<PlayerReloading>().ReloadTimeRemaining() > 0)
            return Usability.Cooldown;

        // not enough ammo?
        if (requiredAmmo != null && hotbar.slots[hotbarIndex].item.ammo == 0)
            return Usability.Empty;

        // otherwise we can use it
        return Usability.Usable;
    }

    // helper functions ////////////////////////////////////////////////////////
    protected WeaponDetails GetWeaponDetails(PlayerEquipment equipment)
    {
        if (equipment.weaponMount != null && equipment.weaponMount.childCount > 0)
            return equipment.weaponMount.GetChild(0).GetComponentInChildren<WeaponDetails>();
        return null;
    }

    protected void ShowMuzzleFlash(PlayerEquipment equipment)
    {
        // find the weapon details
        WeaponDetails details = GetWeaponDetails(equipment);
        if (details != null)
        {
            if (details.muzzleFlash != null) details.muzzleFlash.Fire();
        }
        else Debug.LogWarning("weapon details not found for player: " + equipment.name);
    }

    protected bool RaycastToLookAt(GameObject player, Vector3 lookAt, out RaycastHit hit)
    {
        // get PlayerLook component to access the physics raycast layers for the
        // player (e.g. don't Raycast against AggroArea etc.)
        PlayerLook look = player.GetComponent<PlayerLook>();

        // start raycast at head bone, not at muzzle location
        // (starting at muzzle would be physically correct, but if we lookat
        //  something between head and muzzle, we'd shoot backwards from
        //  muzzle towards head, which would cause walls being shot from the
        //  other side etc.)
        // (also makes sure that we can never shoot through walls by sticking a
        //  weapon through a wall)
        Transform head = player.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);

        // ignore self just to be sure
        Vector3 direction = lookAt - head.position;
        Debug.DrawLine(head.position, direction, Color.yellow, 1);
        if (Utils.RaycastWithout(head.position, direction, out hit, attackRange, player, look.raycastLayers))
        {
            // don't hit anything between
            // show ray for debugging
            Debug.DrawLine(head.position, hit.point, Color.red, 1);
            Debug.DrawLine(hit.point, hit.point + hit.normal, Color.blue, 1);
            return true;
        }

        hit = new RaycastHit();
        return false;
    }

    public override void Use(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // call base function to start cooldown
        base.Use(hotbar, hotbarIndex, lookAt);

        ItemSlot slot = hotbar.slots[hotbarIndex];

        // decrease ammo (if any is required)
        if (requiredAmmo != null)
        {
            --slot.item.ammo;
            hotbar.slots[hotbarIndex] = slot;
        }

        // reduce durability in any case. rifles always get worse each shot.
        slot.item.durability = Mathf.Max(slot.item.durability - 1, 0);
        hotbar.slots[hotbarIndex] = slot;
    }

    public override void OnUsed(PlayerHotbar hotbar, Vector3 lookAt)
    {
        // play shot sound in any case
        if (successfulUseSound) hotbar.audioSource.PlayOneShot(successfulUseSound);

        // show muzzle flash in any case
        ShowMuzzleFlash(hotbar.GetComponent<PlayerEquipment>());

        // show decal if we didn't hit anything living
        if (decalPrefab != null &&
            RaycastToLookAt(hotbar.gameObject, lookAt, out RaycastHit hit) &&
            !hit.transform.GetComponent<Health>())
        {
            // instantiate
            GameObject go = Instantiate(decalPrefab, hit.point + hit.normal * decalOffset, Quaternion.LookRotation(-hit.normal));

            // parent to hit collider so that decals don't hang in air if we
            // hit a moving object like a door.
            // (.collider.transform instead of
            // -> parent to .collider.transform instead of .transform because
            //    for our doors, .transform would be the door parent, while
            //    .collider is the part that actually moves. so this is safer.
            go.transform.parent = hit.collider.transform;
        }

        // recoil (only for local player)
        if (hotbar.isLocalPlayer)
        {
            // horizontal from - to +
            // vertical from 0 to + (recoil never goes downwards)
            float horizontal = Random.Range(-recoilHorizontal / 2, recoilHorizontal / 2);
            float vertical = Random.Range(0, recoilVertical);

            // rotate player horizontally, rotate camera vertically
            hotbar.transform.Rotate(new Vector3(0, horizontal, 0));
            Camera.main.transform.Rotate(new Vector3(-vertical, 0, 0));
        }
    }

    // tooltip
    public override string ToolTip()
    {
        StringBuilder tip = new StringBuilder(base.ToolTip());
        tip.Replace("{REQUIREDAMMO}", requiredAmmo != null ? requiredAmmo.name : "");
        tip.Replace("{MAGAZINESIZE}", magazineSize.ToString());
        return tip.ToString();
    }
}
