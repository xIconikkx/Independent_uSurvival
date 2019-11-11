// to interact with the storage
using UnityEngine;
using Mirror;

public class PlayerFurnaceUsage : NetworkBehaviour
{
    // components to be assigned in inspector
    public Health health;
    public PlayerLook look;
    public PlayerInteraction interaction;
    public PlayerInventory inventory;
    public KeyCode[] splitKeys = {KeyCode.LeftShift, KeyCode.RightShift};

    // commands ////////////////////////////////////////////////////////////////
    [Command]
    public void CmdSwapInventoryFurnaceIngredient(GameObject furnaceGameObject, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (furnaceGameObject != null)
        {
            Furnace furnace = furnaceGameObject.GetComponent<Furnace>();
            if (furnace != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, furnace.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count)
            {
                // modifying slots cancels the bake. this way the user has a way
                // to stop a bake, add more ingredient stacks, etc.
                furnace.CancelBaking();

                // swap them
                ItemSlot temp = furnace.ingredientSlot;
                furnace.ingredientSlot = inventory.slots[inventoryIndex];
                inventory.slots[inventoryIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdSwapInventoryFurnaceFuel(GameObject furnaceGameObject, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (furnaceGameObject != null)
        {
            Furnace furnace = furnaceGameObject.GetComponent<Furnace>();
            if (furnace != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, furnace.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count)
            {
                // modifying slots cancels the bake. this way the user has a way
                // to stop a bake, add more ingredient stacks, etc.
                furnace.CancelBaking();

                // swap them
                ItemSlot temp = furnace.fuelSlot;
                furnace.fuelSlot = inventory.slots[inventoryIndex];
                inventory.slots[inventoryIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdMoveFurnaceResultInventory(GameObject furnaceGameObject, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (furnaceGameObject != null)
        {
            Furnace furnace = furnaceGameObject.GetComponent<Furnace>();
            if (furnace != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, furnace.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
                inventory.slots[inventoryIndex].amount == 0 &&
                furnace.resultSlot.amount > 0)
            {
                // modifying result slot DOES NOT cancel baking because it's
                // totally fine to take finished results out of it.

                // move it
                inventory.slots[inventoryIndex] = furnace.resultSlot;
                furnace.resultSlot = new ItemSlot();
            }
        }
    }

    [Command]
    public void CmdMergeInventoryFurnaceIngredient(GameObject furnaceGameObject, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (furnaceGameObject != null)
        {
            Furnace furnace = furnaceGameObject.GetComponent<Furnace>();
            if (furnace != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, furnace.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count)
            {
                // modifying slots cancels the bake. this way the user has a way
                // to stop a bake, add more ingredient stacks, etc.
                furnace.CancelBaking();

                // both items have to be valid
                ItemSlot slotFrom = inventory.slots[inventoryIndex];
                ItemSlot slotTo = furnace.ingredientSlot;
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
                        furnace.ingredientSlot = slotTo;
                    }
                }
            }
        }
    }

    [Command]
    public void CmdMergeInventoryFurnaceFuel(GameObject furnaceGameObject, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (furnaceGameObject != null)
        {
            Furnace furnace = furnaceGameObject.GetComponent<Furnace>();
            if (furnace != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, furnace.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count)
            {
                // modifying slots cancels the bake. this way the user has a way
                // to stop a bake, add more ingredient stacks, etc.
                furnace.CancelBaking();

                // both items have to be valid
                ItemSlot slotFrom = inventory.slots[inventoryIndex];
                ItemSlot slotTo = furnace.fuelSlot;
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
                        furnace.fuelSlot = slotTo;
                    }
                }
            }
        }
    }

    [Command]
    public void CmdMergeFurnaceIngredientInventory(GameObject furnaceGameObject, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (furnaceGameObject != null)
        {
            Furnace furnace = furnaceGameObject.GetComponent<Furnace>();
            if (furnace != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, furnace.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count)
            {
                // modifying slots cancels the bake. this way the user has a way
                // to stop a bake, add more ingredient stacks, etc.
                furnace.CancelBaking();

                // both items have to be valid
                ItemSlot slotFrom = furnace.ingredientSlot;
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
                        furnace.ingredientSlot = slotFrom;
                        inventory.slots[inventoryIndex] = slotTo;
                    }
                }
            }
        }
    }

    [Command]
    public void CmdMergeFurnaceFuelInventory(GameObject furnaceGameObject, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (furnaceGameObject != null)
        {
            Furnace furnace = furnaceGameObject.GetComponent<Furnace>();
            if (furnace != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, furnace.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count)
            {
                // modifying slots cancels the bake. this way the user has a way
                // to stop a bake, add more ingredient stacks, etc.
                furnace.CancelBaking();

                // both items have to be valid
                ItemSlot slotFrom = furnace.fuelSlot;
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
                        furnace.fuelSlot = slotFrom;
                        inventory.slots[inventoryIndex] = slotTo;
                    }
                }
            }
        }
    }

    [Command]
    public void CmdMergeFurnaceResultInventory(GameObject furnaceGameObject, int inventoryIndex)
    {
        // validate: make sure that the slots actually exist in the inventory
        // and in the storage
        if (furnaceGameObject != null)
        {
            Furnace furnace = furnaceGameObject.GetComponent<Furnace>();
            if (furnace != null &&
                // use head pos for range check because interactable raycast does too.
                // (transform.position isn't good when interacting above us!)
                Vector3.Distance(look.headPosition, furnace.transform.position) <= interaction.range &&
                health.current > 0 &&
                0 <= inventoryIndex && inventoryIndex < inventory.slots.Count)
            {
                // modifying result slot DOES NOT cancel baking because it's
                // totally fine to take finished results out of it.

                // both items have to be valid
                ItemSlot slotFrom = furnace.resultSlot;
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
                        furnace.resultSlot = slotFrom;
                        inventory.slots[inventoryIndex] = slotTo;
                    }
                }
            }
        }
    }

    // drag & drop: inventory<->furnace ingredient /////////////////////////////
    void OnDragAndDrop_InventorySlot_FurnaceIngredientSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (interaction.current != null)
        {
            Furnace furnace = ((NetworkBehaviour)interaction.current).GetComponent<Furnace>();
            if (furnace != null)
            {
                // merge? (just check equality, rest is done server sided)
                if (inventory.slots[slotIndices[0]].amount > 0 && furnace.ingredientSlot.amount > 0 &&
                    inventory.slots[slotIndices[0]].item.Equals(furnace.ingredientSlot.item))
                {
                    CmdMergeInventoryFurnaceIngredient(furnace.gameObject, slotIndices[0]);
                }
                // swap?
                else
                {
                    CmdSwapInventoryFurnaceIngredient(furnace.gameObject, slotIndices[0]);
                }
            }
        }
    }

    void OnDragAndDrop_FurnaceIngredientSlot_InventorySlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (interaction.current != null)
        {
            Furnace furnace = ((NetworkBehaviour)interaction.current).GetComponent<Furnace>();
            if (furnace != null)
            {
                // merge? (just check equality, rest is done server sided)
                if (furnace.ingredientSlot.amount > 0 && inventory.slots[slotIndices[1]].amount > 0 &&
                    furnace.ingredientSlot.item.Equals(inventory.slots[slotIndices[1]].item))
                {
                    CmdMergeFurnaceIngredientInventory(furnace.gameObject, slotIndices[1]);
                }
                // swap?
                else
                {
                    CmdSwapInventoryFurnaceIngredient(furnace.gameObject, slotIndices[1]);
                }
            }
        }
    }

    // drag & drop: inventory<->furnace fuel ///////////////////////////////////
    void OnDragAndDrop_InventorySlot_FurnaceFuelSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (interaction.current != null)
        {
            Furnace furnace = ((NetworkBehaviour)interaction.current).GetComponent<Furnace>();
            if (furnace != null)
            {
                // merge? (just check equality, rest is done server sided)
                if (inventory.slots[slotIndices[0]].amount > 0 && furnace.fuelSlot.amount > 0 &&
                    inventory.slots[slotIndices[0]].item.Equals(furnace.fuelSlot.item))
                {
                    CmdMergeInventoryFurnaceFuel(furnace.gameObject, slotIndices[0]);
                }
                // swap?
                else
                {
                    CmdSwapInventoryFurnaceFuel(furnace.gameObject, slotIndices[0]);
                }
            }
        }
    }

    void OnDragAndDrop_FurnaceFuelSlot_InventorySlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (interaction.current != null)
        {
            Furnace furnace = ((NetworkBehaviour)interaction.current).GetComponent<Furnace>();
            if (furnace != null)
            {
                // merge? (just check equality, rest is done server sided)
                if (furnace.fuelSlot.amount > 0 && inventory.slots[slotIndices[1]].amount > 0 &&
                    furnace.fuelSlot.item.Equals(inventory.slots[slotIndices[1]].item))
                {
                    CmdMergeFurnaceFuelInventory(furnace.gameObject, slotIndices[1]);
                }
                // swap?
                else
                {
                    CmdSwapInventoryFurnaceFuel(furnace.gameObject, slotIndices[1]);
                }
            }
        }
    }

    // drag & drop: inventory<->furnace result /////////////////////////////////
    void OnDragAndDrop_FurnaceResultSlot_InventorySlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (interaction.current != null)
        {
            Furnace furnace = ((NetworkBehaviour)interaction.current).GetComponent<Furnace>();
            if (furnace != null)
            {
                // merge? (just check equality, rest is done server sided)
                if (furnace.resultSlot.amount > 0 && inventory.slots[slotIndices[1]].amount > 0 &&
                    furnace.resultSlot.item.Equals(inventory.slots[slotIndices[1]].item))
                {
                    CmdMergeFurnaceResultInventory(furnace.gameObject, slotIndices[1]);
                }
                // move? (= swap to empty slot)
                else if (furnace.resultSlot.amount > 0 && inventory.slots[slotIndices[1]].amount == 0)
                {
                    CmdMoveFurnaceResultInventory(furnace.gameObject, slotIndices[1]);
                }
            }
        }
    }
}
