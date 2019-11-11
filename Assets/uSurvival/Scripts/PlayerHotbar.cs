// Note: could inherit from Inventory class, but there seems to be a UNET bug
// where having two Inventory components causes the second one not to be synced
// to the client anymore.
using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[Serializable]
public struct HotbarModelLocation
{
    public string requiredCategory;
    public Transform location;
}

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerEquipment))]
public class PlayerHotbar : NetworkBehaviour, ICombatBonus
{
    // Used components. Assign in Inspector. Easier than GetComponent caching.
    public Player player;
    public AudioSource audioSource;
    public Health health;
    public PlayerInventory inventory;
    public PlayerEquipment equipment;
    public PlayerMovement movement;
    public PlayerReloading reloading;
    public PlayerLook look;

    public int size = 10;
    public SyncListItemSlot slots = new SyncListItemSlot();
    public ScriptableItem[] defaultItems;
    public KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };
    [SyncVar(hook=nameof(OnSelectionChanged))] public int selection = 0; // always between 0 and slots.count, no checks needed

    // punching: reusing 'melee weapon' makes sense because it's the same code anyway
    public MeleeWeaponItem hands;

    // hotbar items will automatically be displayed on the body if a free
    // EquipmentInfo is found
    public HotbarModelLocation[] modelLocations;

    // combat boni for currently equipped tool (weapon/shield/etc.) ////////////
    public int GetDamageBonus()
    {
        ItemSlot slot = slots[selection];
        if (slot.amount > 0 && slot.item.data is WeaponItem)
            return ((WeaponItem)slot.item.data).damage;
        return 0;
    }
    public int GetDefenseBonus() { return 0; }

    // helpers /////////////////////////////////////////////////////////////////
    // returns tool or hands
    public UsableItem GetUsableItemOrHands(int index)
    {
        ItemSlot slot = slots[index];
        return slot.amount > 0 ? (UsableItem)slot.item.data : hands;
    }

    // returns current tool or hands
    public UsableItem GetCurrentUsableItemOrHands()
    {
        return GetUsableItemOrHands(selection);
    }

    // check slot's durability (ignore hands and ignore maxDurability=0)
    bool CheckDurability(int slotIndex)
    {
       return slots[slotIndex].amount == 0 ||
              slots[slotIndex].item.maxDurability == 0 ||
              slots[slotIndex].item.durability > 0;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // setup synclist callbacks on client. no need to update and show and
        // animate equipment on server
        slots.Callback += OnHotbarChanged;

        // refresh all locations once (on synclist changed won't be called for
        // initial lists)
        // -> needs to happen before ProximityChecker's initial SetVis call,
        //    otherwise we get a hidden character with visible equipment
        //    (hence OnStartClient and not Start)
        RefreshLocations();
    }

    void RefreshLocations()
    {
        // run a sweep to assign / replace / delete locations
        // note: destroying all of them and then instantiating all of them is
        //       way easier, but also way too expensive. instead we scan through
        //       them and only delete/add prefabs as needed.

        // keep track of the locations that were assigned
        HashSet<int> assignedLocations = new HashSet<int>();

        // try to place each prefab into a free location with the matching category
        for (int hotbarIndex = 0; hotbarIndex < slots.Count; ++hotbarIndex)
        {
            ItemSlot slot = slots[hotbarIndex];
            if (slot.amount > 0 && slot.item.data.modelPrefab != null)
            {
                UsableItem itemData = (UsableItem)(slot.item.data);

                // find a location for this one
                for (int locationIndex = 0; locationIndex < modelLocations.Length; ++locationIndex)
                {
                    HotbarModelLocation modelLocation = modelLocations[locationIndex];

                    // matching category and not assigned yet?
                    if (itemData.category.StartsWith(modelLocation.requiredCategory) &&
                        !assignedLocations.Contains(locationIndex))
                    {
                        GameObject model;

                        // if the location has no model yet, simply add one
                        if (modelLocation.location.childCount == 0)
                        {
                            // instantiate and parent
                            model = Instantiate(itemData.modelPrefab);
                            model.name = itemData.modelPrefab.name; // avoid "(Clone)"
                            model.transform.SetParent(modelLocation.location, false);
                        }
                        // it has a model. is it the right one?
                        else if (modelLocation.location.GetChild(0).name == itemData.modelPrefab.name)
                        {
                            model = modelLocation.location.GetChild(0).gameObject;
                        }
                        // otherwise replace it
                        else
                        {
                            // destroy and unparent so childCount is 0 in next
                            // check (destroy doesn't destroy immediately)
                            GameObject oldModel = modelLocation.location.GetChild(0).gameObject;
                            oldModel.transform.parent = null;
                            Destroy(oldModel);

                            // instantiate
                            model = Instantiate(itemData.modelPrefab);
                            model.name = itemData.modelPrefab.name; // avoid "(Clone)"
                            model.transform.SetParent(modelLocation.location, false);
                        }

                        // hide if selected, show if not selected anymore
                        model.SetActive(hotbarIndex != selection);

                        // remember that we assigned this one
                        assignedLocations.Add(locationIndex);

                        // done searching a location for this model
                        break;
                    }
                }
            }
        }

        // now clear all locations that were not assigned. those might still
        // have old models
        for (int locationIndex = 0; locationIndex < modelLocations.Length; ++locationIndex)
        {
            // not assigned? then destroy the old model (if any)
            HotbarModelLocation modelLocation = modelLocations[locationIndex];
            if (!assignedLocations.Contains(locationIndex) &&
                modelLocation.location.childCount > 0)
            {
                // destroy and unparent so childCount is 0 in next
                // check (destroy doesn't destroy immediately)
                GameObject oldModel = modelLocation.location.GetChild(0).gameObject;
                oldModel.transform.parent = null;
                Destroy(oldModel);
            }
        }
    }

    void OnHotbarChanged(SyncListItemSlot.Operation op, int index, ItemSlot changedSlot)
    {
        // update all locations whenever one location changed. simple and stupid
        RefreshLocations();
    }

    void OnSelectionChanged(int value)
    {
        // set selection
        selection = value;

        // refresh locations to hide the selected model
        RefreshLocations();
    }

    // update //////////////////////////////////////////////////////////////////
    [Client]
    void TryUseItem(UsableItem itemData)
    {
        // note: no .amount > 0 check because it's either an item or hands

        // repeated or one time use while holding mouse down?
        if (itemData.keepUsingWhileButtonDown || Input.GetMouseButtonDown(0))
        {
            // check durability
            if (CheckDurability(selection))
            {
                // get the exact look position on whatever object we aim at
                Vector3 lookAt = look.lookPositionRaycasted;

                // use it
                Usability usability = itemData.CanUse(this, selection, lookAt);
                if (usability == Usability.Usable)
                {
                    // attack by using the weapon item
                    //Debug.DrawLine(Camera.main.transform.position, lookAt, Color.gray, 1);
                    CmdUseItem(selection, lookAt);

                    // simulate OnUsed locally without waiting for the Rpc to avoid
                    // latency effects:
                    // - usedEndTime would be synced too slowly, hence fire interval
                    //   would be too slow on clients
                    // - TryUseItem would be called immediately again afterwards
                    //   because useEndTime wouldn't be reset yet due to latency
                    // - decals/muzzle flash would be delayed by latency and feel
                    //   bad
                    OnUsedItem(itemData, lookAt);
                }
                else if (usability == Usability.Empty)
                {
                    // play empty sound locally (if any)
                    // -> feels best to only play it when clicking the mouse button once, not while holding
                    if (Input.GetMouseButtonDown(0))
                    {
                        if (itemData.emptySound)
                            audioSource.PlayOneShot(itemData.emptySound);
                    }
                }
                // do nothing if on cooldown (just wait) or if not usable at all
            }
        }
    }

    void Update()
    {
        // current selection model needs to be refreshed on the client AND on
        // the server, because the server needs the muzzle location when firing.
        // -> slots.Callback is only called on clients as it seems, so we need
        //    to do it in Update. RefreshLocation only refresh if necessary
        //    anyway.
        equipment.RefreshLocation(equipment.weaponMount, slots[selection]);

        // localplayer selected item usage
        if (isLocalPlayer)
        {
            // mouse down and can we use items right now?
            if (Input.GetMouseButton(0) &&
                Cursor.lockState == CursorLockMode.Locked &&
                health.current > 0 &&
                movement.state != MoveState.CLIMBING &&
                reloading.ReloadTimeRemaining() == 0 &&
                !look.IsFreeLooking() &&
                !Utils.IsCursorOverUserInterface() &&
                Input.touchCount <= 1)
            {
                // use current item or hands
                TryUseItem(GetCurrentUsableItemOrHands());
            }
        }
    }

    ////////////////////////////////////////////////////////////////////////////
    [Command]
    public void CmdSelect(int index)
    {
        // validate: valid index and not reloading? (switching while reloading
        // is very unrealistic, feels weird and UI reload bar uses selected
        // weapon's reloadTime)
        if (0 <= index && index < slots.Count &&
            reloading.ReloadTimeRemaining() == 0)
            selection = index;
    }

    [Command]
    public void CmdSwapHotbarHotbar(int fromIndex, int toIndex)
    {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (health.current > 0 &&
            0 <= fromIndex && fromIndex < slots.Count &&
            0 <= toIndex && toIndex < slots.Count &&
            fromIndex != toIndex)
        {
            // swap them
            ItemSlot temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;
        }
    }

    [Command]
    public void CmdHotbarSplit(int fromIndex, int toIndex)
    {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (health.current > 0 &&
            0 <= fromIndex && fromIndex < slots.Count &&
            0 <= toIndex && toIndex < slots.Count &&
            fromIndex != toIndex)
        {
            // slotFrom needs at least two to split, slotTo has to be empty
            ItemSlot slotFrom = slots[fromIndex];
            ItemSlot slotTo = slots[toIndex];
            if (slotFrom.amount >= 2 && slotTo.amount == 0)
            {
                // split them serversided (has to work for even and odd)
                slotTo = slotFrom; // copy the value

                slotTo.amount = slotFrom.amount / 2;
                slotFrom.amount -= slotTo.amount; // works for odd too

                // put back into the list
                slots[fromIndex] = slotFrom;
                slots[toIndex] = slotTo;
            }
        }
    }

    [Command]
    public void CmdHotbarMerge(int fromIndex, int toIndex)
    {
        if (health.current > 0 &&
            0 <= fromIndex && fromIndex < slots.Count &&
            0 <= toIndex && toIndex < slots.Count &&
            fromIndex != toIndex)
        {
            // both items have to be valid
            ItemSlot slotFrom = slots[fromIndex];
            ItemSlot slotTo = slots[toIndex];
            if (slotFrom.amount > 0 && slotTo.amount > 0)
            {
                // make sure that items are the same type
                // note: .Equals because name AND dynamic variables matter (petLevel etc.)
                if (slotFrom.item.Equals(slotTo.item))
                {
                    // merge from -> to
                    // put as many as possible into 'To' slot
                    int put = slotTo.IncreaseAmount(slotFrom.amount);
                    slotFrom.DecreaseAmount(put);

                    // put back into the list
                    slots[fromIndex] = slotFrom;
                    slots[toIndex] = slotTo;
                }
            }
        }
    }

    // only allow items on the hotbar. ammo has no business being on there, etc.
    public bool IsAllowedOnHotbar(Item item)
    {
        return item.data is UsableItem;
    }

    public bool IsAllowedOnHotbar(ItemSlot slot)
    {
        return slot.amount == 0 || IsAllowedOnHotbar(slot.item);
    }

    [Command]
    public void CmdSwapInventoryHotbar(int inventoryIndex, int hotbarIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the hotbar. also check if it's allowed on there.
        if (health.current > 0 &&
            0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
            0 <= hotbarIndex && hotbarIndex < slots.Count &&
            IsAllowedOnHotbar(inventory.slots[inventoryIndex]))
        {
            // swap them
            ItemSlot temp = slots[hotbarIndex];
            slots[hotbarIndex] = inventory.slots[inventoryIndex];
            inventory.slots[inventoryIndex] = temp;
        }
    }

    [Command]
    public void CmdMergeInventoryHotbar(int inventoryIndex, int hotbarIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the equipment
        if (health.current > 0 &&
            0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
            0 <= hotbarIndex && hotbarIndex < slots.Count)
        {
            // both items have to be valid
            ItemSlot slotFrom = inventory.slots[inventoryIndex];
            ItemSlot slotTo = slots[hotbarIndex];
            if (slotFrom.amount > 0 && slotTo.amount > 0)
            {
                // make sure that items are the same type
                // note: .Equals because name AND dynamic variables matter (petLevel etc.)
                if (slotFrom.item.Equals(slotTo.item))
                {
                    // merge from -> to
                    // put as many as possible into 'To' slot
                    int put = slotTo.IncreaseAmount(slotFrom.amount);
                    slotFrom.DecreaseAmount(put);

                    // put back into the lists
                    inventory.slots[inventoryIndex] = slotFrom;
                    slots[hotbarIndex] = slotTo;
                }
            }
        }
    }

    [Command]
    public void CmdMergeHotbarInventory(int hotbarIndex, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the equipment
        if (health.current > 0 &&
            0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
            0 <= hotbarIndex && hotbarIndex < slots.Count)
        {
            // both items have to be valid
            ItemSlot slotFrom = slots[hotbarIndex];
            ItemSlot slotTo = inventory.slots[inventoryIndex];
            if (slotFrom.amount > 0 && slotTo.amount > 0)
            {
                // make sure that items are the same type
                // note: .Equals because name AND dynamic variables matter (petLevel etc.)
                if (slotFrom.item.Equals(slotTo.item))
                {
                    // merge from -> to
                    // put as many as possible into 'To' slot
                    int put = slotTo.IncreaseAmount(slotFrom.amount);
                    slotFrom.DecreaseAmount(put);

                    // put back into the lists
                    slots[hotbarIndex] = slotFrom;
                    inventory.slots[inventoryIndex] = slotTo;
                }
            }
        }
    }

    // used by local simulation and Rpc, so we might as well put it in a function
    void OnUsedItem(UsableItem itemData, Vector3 lookAt)
    {
        // reset cooldown for local player to avoid waiting for sync result
        if (isLocalPlayer)
            player.SetItemCooldown(itemData.name, itemData.cooldown);

        // call OnUsed
        itemData.OnUsed(this, lookAt);

        // trigger upperbody usage animation in all animators, so it works for
        // skinned meshes too.
        // (trigger works best for usage, especially for repeated usage to)
        // (only for weapons, not for potions until we can hold potions in hand
        //  later on)
        if (itemData is WeaponItem)
            foreach (Animator animator in GetComponentsInChildren<Animator>())
                animator.SetTrigger("UPPERBODY_USED");
    }

    [ClientRpc]
    public void RpcUsedItem(int itemNameHash, Vector3 lookAt)
    {
        // local player simulates OnUsed immediately after using, so only do it
        // for other players
        if (!isLocalPlayer)
        {
            // use Item finds ScriptableItem based on hash, no need to do it manually
            Item item = new Item{hash=itemNameHash};

            // OnUsed logic
            OnUsedItem((UsableItem)item.data, lookAt);
        }
    }

    // note: lookAt is available in PlayerLook, but we still pass the exact
    // uncompressed Vector3 here, because it needs to be PRECISE when shooting,
    // building structures, etc.
    [Command]
    public void CmdUseItem(int index, Vector3 lookAt)
    {
        // validate
        if (0 <= index && index < slots.Count &&
            health.current > 0 &&
            CheckDurability(index))
        {
            // use item at index, or hands
            // note: we don't decrease amount / destroy in all cases because
            // some items may swap to other slots in .Use()
            UsableItem itemData = GetUsableItemOrHands(index);
            if (itemData.CanUse(this, index, lookAt) == Usability.Usable)
            {
                // use it
                itemData.Use(this, index, lookAt);

                // RpcUsedItem needs itemData, but we can't send that as Rpc
                // -> we could send the Item at slots[index], but .data is null
                //    for hands because hands actually live in '.hands' variable
                // -> we could create a new Item(itemData) and send, but it's
                //    kinda odd that it's different from slot
                // => only sending hash saves A LOT of bandwidth over time since
                //    this rpc is called very frequently (each weapon shot etc.)
                //    (we reuse Item's hash generation for simplicity)
                RpcUsedItem(new Item(itemData).hash, lookAt);
            }
            else
            {
                // CanUse is checked locally before calling this Cmd, so if we
                // get here then either our prediction is off (in which case we
                // really should show a message for easier debugging), or someone
                // tried to cheat, or there's some networking issue, etc.
                Debug.Log("CmdUseItem rejected for: " + name + " item=" + itemData.name + "@" + NetworkTime.time);
            }
        }
    }

    // drop items //////////////////////////////////////////////////////////////
    [Server]
    public void DropItemAndClearSlot(int index)
    {
        // drop and remove from inventory
        ItemSlot slot = slots[index];
        inventory.DropItem(slot.item, slot.amount);
        slot.amount = 0;
        slots[index] = slot;
    }

    [Command]
    public void CmdDropItem(int index)
    {
        // validate
        if (health.current > 0 &&
            0 <= index && index < slots.Count && slots[index].amount > 0)
        {
            DropItemAndClearSlot(index);
        }
    }

    // durability //////////////////////////////////////////////////////////////
    public void OnReceivedDamage(GameObject attacker, int damage)
    {
        // reduce durability in each item
        for (int i = 0; i < slots.Count; ++i)
        {
            if (slots[i].amount > 0)
            {
                ItemSlot slot = slots[i];
                slot.item.durability = Mathf.Clamp(slot.item.durability - damage, 0, slot.item.maxDurability);
                slots[i] = slot;
            }
        }
    }

    // death & respawn /////////////////////////////////////////////////////////
    // drop all items on death, so others can loot us
    [Server]
    public void OnDeath()
    {
        for (int i = 0; i < slots.Count; ++i)
            if (slots[i].amount > 0)
                DropItemAndClearSlot(i);
    }

    // drag & drop /////////////////////////////////////////////////////////////
    void OnDragAndDrop_HotbarSlot_HotbarSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo

        // merge? (just check equality, rest is done server sided)
        if (slots[slotIndices[0]].amount > 0 && slots[slotIndices[1]].amount > 0 &&
            slots[slotIndices[0]].item.Equals(slots[slotIndices[1]].item))
        {
            CmdHotbarMerge(slotIndices[0], slotIndices[1]);
        }
        // split?
        else if (Utils.AnyKeyPressed(inventory.splitKeys))
        {
            CmdHotbarSplit(slotIndices[0], slotIndices[1]);
        }
        // swap?
        else
        {
            CmdSwapHotbarHotbar(slotIndices[0], slotIndices[1]);
        }
    }

    void OnDragAndDrop_InventorySlot_HotbarSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo

        // merge? (just check equality, rest is done server sided)
        if (inventory.slots[slotIndices[0]].amount > 0 && slots[slotIndices[1]].amount > 0 &&
            inventory.slots[slotIndices[0]].item.Equals(slots[slotIndices[1]].item))
        {
            CmdMergeInventoryHotbar(slotIndices[0], slotIndices[1]);
        }
        // dropped ammo onto weapon to load it?
        else if (inventory.slots[slotIndices[0]].amount > 0 && slots[slotIndices[1]].amount > 0 &&
                 reloading.CanLoadAmmoIntoWeapon(inventory.slots[slotIndices[0]], slots[slotIndices[1]].item))
        {
            reloading.CmdReloadWeaponOnHotbar(slotIndices[0], slotIndices[1]);
        }
        // swap? (only if allowed inventory item)
        else if (IsAllowedOnHotbar(inventory.slots[slotIndices[0]]))
        {
            CmdSwapInventoryHotbar(slotIndices[0], slotIndices[1]);
        }
    }

    void OnDragAndDrop_HotbarSlot_InventorySlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo

        // merge? (just check equality, rest is done server sided)
        if (slots[slotIndices[0]].amount > 0 && inventory.slots[slotIndices[1]].amount > 0 &&
            slots[slotIndices[0]].item.Equals(inventory.slots[slotIndices[1]].item))
        {
            CmdMergeHotbarInventory(slotIndices[0], slotIndices[1]);
        }
        // swap? (only if allowed inventory item)
        else if (IsAllowedOnHotbar(inventory.slots[slotIndices[1]]))
        {
            CmdSwapInventoryHotbar(slotIndices[1], slotIndices[0]);
        }
    }

    void OnDragAndClear_HotbarSlot(int slotIndex)
    {
        CmdDropItem(slotIndex);
    }
}