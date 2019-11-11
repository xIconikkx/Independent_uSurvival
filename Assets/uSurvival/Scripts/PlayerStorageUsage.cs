// to interact with the storage
using UnityEngine;
using Mirror;

public class PlayerStorageUsage : NetworkBehaviour
{
    // components to be assigned in inspector
    public Health health;
    public PlayerLook look;
    public PlayerInteraction interaction;
    public PlayerInventory inventory;
    public KeyCode[] splitKeys = {KeyCode.LeftShift, KeyCode.RightShift};

    // commands ////////////////////////////////////////////////////////////////
    [Command]
    public void CmdSwapStorageStorage(GameObject storageGameObject, int fromIndex, int toIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (storageGameObject != null)
        {
            Storage storage = storageGameObject.GetComponent<Storage>();
            if (storage != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, storage.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= fromIndex && fromIndex < storage.slots.Count &&
                0 <= toIndex && toIndex < storage.slots.Count &&
                fromIndex != toIndex)
            {
                // swap them
                ItemSlot temp = storage.slots[fromIndex];
                storage.slots[fromIndex] = storage.slots[toIndex];
                storage.slots[toIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdStorageSplit(GameObject storageGameObject, int fromIndex, int toIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (storageGameObject != null)
        {
            Storage storage = storageGameObject.GetComponent<Storage>();
            if (storage != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, storage.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= fromIndex && fromIndex < storage.slots.Count &&
                0 <= toIndex && toIndex < storage.slots.Count &&
                fromIndex != toIndex)
            {
                // slotFrom needs at least two to split, slotTo has to be empty
                ItemSlot slotFrom = storage.slots[fromIndex];
                ItemSlot slotTo = storage.slots[toIndex];
                if (slotFrom.amount >= 2 && slotTo.amount == 0) {
                    // split them serversided (has to work for even and odd)
                    slotTo = slotFrom; // copy the value

                    slotTo.amount = slotFrom.amount / 2;
                    slotFrom.amount -= slotTo.amount; // works for odd too

                    // put back into the list
                    storage.slots[fromIndex] = slotFrom;
                    storage.slots[toIndex] = slotTo;
                }
            }
        }
    }

    [Command]
    public void CmdStorageMerge(GameObject storageGameObject, int fromIndex, int toIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (storageGameObject != null)
        {
            Storage storage = storageGameObject.GetComponent<Storage>();
            if (storage != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, storage.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= fromIndex && fromIndex < storage.slots.Count &&
                0 <= toIndex && toIndex < storage.slots.Count &&
                fromIndex != toIndex)
            {
                // both items have to be valid
                ItemSlot slotFrom = storage.slots[fromIndex];
                ItemSlot slotTo = storage.slots[toIndex];
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
                        storage.slots[fromIndex] = slotFrom;
                        storage.slots[toIndex] = slotTo;
                    }
                }
            }
        }
    }

    [Command]
    public void CmdSwapInventoryStorage(GameObject storageGameObject, int inventoryIndex, int storageIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (storageGameObject != null)
        {
            Storage storage = storageGameObject.GetComponent<Storage>();
            if (storage != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, storage.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
                0 <= storageIndex && storageIndex < storage.slots.Count)
            {
                // swap them
                ItemSlot temp = storage.slots[storageIndex];
                storage.slots[storageIndex] = inventory.slots[inventoryIndex];
                inventory.slots[inventoryIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdMergeInventoryStorage(GameObject storageGameObject, int inventoryIndex, int storageIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (storageGameObject != null)
        {
            Storage storage = storageGameObject.GetComponent<Storage>();
            if (storage != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, storage.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
                0 <= storageIndex && storageIndex < storage.slots.Count)
            {
                // both items have to be valid
                ItemSlot slotFrom = inventory.slots[inventoryIndex];
                ItemSlot slotTo = storage.slots[storageIndex];
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
                        storage.slots[storageIndex] = slotTo;
                    }
                }
            }
        }
    }

    [Command]
    public void CmdMergeStorageInventory(GameObject storageGameObject, int storageIndex, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (storageGameObject != null)
        {
            Storage storage = storageGameObject.GetComponent<Storage>();
            if (storage != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, storage.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
                0 <= storageIndex && storageIndex < storage.slots.Count)
            {
                // both items have to be valid
                ItemSlot slotFrom = storage.slots[storageIndex];
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
                        storage.slots[storageIndex] = slotFrom;
                        inventory.slots[inventoryIndex] = slotTo;
                    }
                }
            }
        }
    }

    // drag & drop /////////////////////////////////////////////////////////////
    void OnDragAndDrop_StorageSlot_StorageSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (interaction.current != null)
        {
            Storage storage = ((NetworkBehaviour)interaction.current).GetComponent<Storage>();
            if (storage != null)
            {
                // merge? (just check equality, rest is done server sided)
                if (storage.slots[slotIndices[0]].amount > 0 && storage.slots[slotIndices[1]].amount > 0 &&
                    storage.slots[slotIndices[0]].item.Equals(storage.slots[slotIndices[1]].item))
                {
                    CmdStorageMerge(storage.gameObject, slotIndices[0], slotIndices[1]);
                }
                // split?
                else if (Utils.AnyKeyPressed(splitKeys))
                {
                    CmdStorageSplit(storage.gameObject, slotIndices[0], slotIndices[1]);
                }
                // swap?
                else
                {
                    CmdSwapStorageStorage(storage.gameObject, slotIndices[0], slotIndices[1]);
                }
            }
        }
    }

    void OnDragAndDrop_InventorySlot_StorageSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        

        if (interaction.current != null)
        {
            Storage storage = ((NetworkBehaviour)interaction.current).GetComponent<Storage>();
            if (storage != null)
            {
                // merge? (just check equality, rest is done server sided)
                if (inventory.slots[slotIndices[0]].amount > 0 && storage.slots[slotIndices[1]].amount > 0 &&
                    inventory.slots[slotIndices[0]].item.Equals(storage.slots[slotIndices[1]].item))
                {
                    CmdMergeInventoryStorage(storage.gameObject, slotIndices[0], slotIndices[1]);
                }
                // swap?
                else
                {
                    CmdSwapInventoryStorage(storage.gameObject, slotIndices[0], slotIndices[1]);
                }
            }
        }
        Player.localPlayer.GetComponent<Money>().UpdateMoney();
    }

    void OnDragAndDrop_StorageSlot_InventorySlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (interaction.current != null)
        {
            Storage storage = ((NetworkBehaviour)interaction.current).GetComponent<Storage>();
            if (storage != null)
            {
                // merge? (just check equality, rest is done server sided)
                if (storage.slots[slotIndices[0]].amount > 0 && inventory.slots[slotIndices[1]].amount > 0 &&
                    storage.slots[slotIndices[0]].item.Equals(inventory.slots[slotIndices[1]].item))
                {
                    CmdMergeStorageInventory(storage.gameObject, slotIndices[0], slotIndices[1]);
                }
                // swap?
                else
                {
                    CmdSwapInventoryStorage(storage.gameObject, slotIndices[1], slotIndices[0]);
                }
            }
        }
        Player.localPlayer.GetComponent<Money>().UpdateMoney();
    }
}
