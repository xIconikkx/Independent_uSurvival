using UnityEngine;
using Mirror;

public class PlayerReloading : NetworkBehaviour
{
    public Health health;
    public PlayerHotbar hotbar;
    public Inventory inventory;
    public PlayerMovement movement;
    public AudioSource audioSource;

    public KeyCode reloadKey = KeyCode.R;
    [SyncVar] double reloadTimeEnd; // server time, synced to client for UI. double for long term precision.

    void Update()
    {
        if (!isLocalPlayer) return;

        // reload key pressed?
        // and cursor locked? (not in UI), alive, not climbing, not typing in an input?
        if (Input.GetKeyDown(reloadKey) &&
            Cursor.lockState == CursorLockMode.Locked &&
            health.current > 0 && movement.state != MoveState.CLIMBING &&
            ReloadTimeRemaining() == 0 &&
            !UIUtils.AnyInputActive())
        {
            // usable item in selected hotbar slot?
            ItemSlot slot = hotbar.slots[hotbar.selection];
            if (slot.amount > 0 && slot.item.data is RangedWeaponItem)
            {
                // requires ammo and not fully loaded yet?
                RangedWeaponItem itemData = (RangedWeaponItem)slot.item.data;
                if (itemData.requiredAmmo != null && slot.item.ammo < itemData.magazineSize)
                {
                    // ammo type in inventory?
                    int inventoryIndex = inventory.GetItemIndexByName(itemData.requiredAmmo.name);
                    if (inventoryIndex != -1)
                    {
                        // ask server to reload
                        CmdReloadWeaponOnHotbar(inventoryIndex, hotbar.selection);

                        // play audio locally to avoid server delay and to save bandwidth
                        if (itemData.reloadSound) audioSource.PlayOneShot(itemData.reloadSound);
                    }
                }
            }
        }
    }

    public float ReloadTimeRemaining()
    {
        // how much time remaining until the casttime ends? (using server time)
        return NetworkTime.time >= reloadTimeEnd ? 0 : (float)(reloadTimeEnd - NetworkTime.time);
    }

    public bool CanLoadAmmoIntoWeapon(ItemSlot ammoSlot, Item weapon)
    {
        // valid slots?
        if (ammoSlot.amount > 0 &&
            ammoSlot.item.data is AmmoItem &&
            weapon.data is RangedWeaponItem)
        {
            // correct ammo type?
            AmmoItem ammoData = (AmmoItem)ammoSlot.item.data;
            RangedWeaponItem weaponData = (RangedWeaponItem)weapon.data;
            if (weaponData.requiredAmmo == ammoData)
            {
                // weapon not fully loaded yet?
                return weapon.ammo < weaponData.magazineSize;
            }
        }
        return false;
    }

    [Command]
    public void CmdReloadWeaponOnHotbar(int inventoryAmmoIndex, int hotbarWeaponIndex)
    {
        // validate
        if (health.current > 0 &&
            0 <= inventoryAmmoIndex && inventoryAmmoIndex < inventory.slots.Count &&
            0 <= hotbarWeaponIndex && hotbarWeaponIndex < hotbar.slots.Count &&
            inventory.slots[inventoryAmmoIndex].amount > 0 &&
            hotbar.slots[hotbarWeaponIndex].amount > 0)
        {
            ItemSlot ammoSlot = inventory.slots[inventoryAmmoIndex];
            ItemSlot weaponSlot = hotbar.slots[hotbarWeaponIndex];
            if (CanLoadAmmoIntoWeapon(ammoSlot, weaponSlot.item))
            {
                RangedWeaponItem weaponData = (RangedWeaponItem)weaponSlot.item.data;

                // add as many as possible
                int limit = Mathf.Clamp(ammoSlot.amount, 0, weaponData.magazineSize - weaponSlot.item.ammo);
                ammoSlot.amount -= limit;
                weaponSlot.item.ammo += limit;

                // put back into the lists
                inventory.slots[inventoryAmmoIndex] = ammoSlot;
                hotbar.slots[hotbarWeaponIndex] = weaponSlot;

                // start reload timer
                reloadTimeEnd = NetworkTime.time + weaponData.reloadTime;
            }
        }
    }

    [Command]
    public void CmdReloadWeaponInInventory(int ammoIndex, int weaponIndex)
    {
        // validate
        if (health.current > 0 &&
            0 <= ammoIndex && ammoIndex < inventory.slots.Count &&
            0 <= weaponIndex && weaponIndex < inventory.slots.Count &&
            inventory.slots[ammoIndex].amount > 0 &&
            inventory.slots[weaponIndex].amount > 0)
        {
            ItemSlot ammoSlot = inventory.slots[ammoIndex];
            ItemSlot weaponSlot = inventory.slots[weaponIndex];
            if (CanLoadAmmoIntoWeapon(ammoSlot, weaponSlot.item))
            {
                RangedWeaponItem weaponData = (RangedWeaponItem)weaponSlot.item.data;

                // add as many as possible
                int limit = Mathf.Clamp(ammoSlot.amount, 0, weaponData.magazineSize - weaponSlot.item.ammo);
                ammoSlot.amount -= limit;
                weaponSlot.item.ammo += limit;

                // put back into the lists
                inventory.slots[ammoIndex] = ammoSlot;
                inventory.slots[weaponIndex] = weaponSlot;

                // start reload timer
                reloadTimeEnd = NetworkTime.time + weaponData.reloadTime;
            }
        }
    }
}
