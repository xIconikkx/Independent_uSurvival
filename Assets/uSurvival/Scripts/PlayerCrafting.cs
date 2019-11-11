// the crafting system is designed to work with all kinds of commonly known
// crafting options:
// - item combinations: wood + stone = axe
// - weapon upgrading: axe + gem = strong axe
// - recipe items: axerecipe(item) + wood(item) + stone(item) = axe(item)
//
// players can craft at all times, not just at npcs, because that's the most
// realistic option
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public enum CraftingState : byte {None, InProgress, Success, Failed}

[DisallowMultipleComponent]
[RequireComponent(typeof(Inventory))]
public class PlayerCrafting : NetworkBehaviour
{
    // Used components. Assign in Inspector. Easier than GetComponent caching.
    public Health health;
    public Inventory inventory;

    public List<int> indices = Enumerable.Repeat(-1, CraftingRecipe.recipeSize).ToList();
    [HideInInspector] public CraftingState craftingState = CraftingState.None; // // client sided

    // craft the current combination of items and put result into inventory
    [Command]
    public void CmdCraft(string recipeName, int[] indices)
    {
        // validate: between 1 and 6, all valid, no duplicates?
        if (health.current > 0 &&
            0 < indices.Length && indices.Length <= CraftingRecipe.recipeSize &&
            indices.All(index => 0 <= index && index < inventory.slots.Count && inventory.slots[index].amount > 0) &&
            !indices.ToList().HasDuplicates())
        {
            // find recipe
            if (CraftingRecipe.dictCrafting.TryGetValue(recipeName, out CraftingRecipe recipe) &&
                recipe.result != null)
            {
                // enough space?
                Item result = new Item(recipe.result);
                if (inventory.CanAdd(result, 1))
                {
                    // remove the ingredients from inventory in any case
                    foreach (ScriptableItemAndAmount ingredient in recipe.ingredients)
                        if (ingredient.amount > 0 && ingredient.item != null)
                            inventory.Remove(new Item(ingredient.item), ingredient.amount);

                    // roll the dice to decide if we add the result or not
                    // IMPORTANT: we use rand() < probability to decide.
                    // => UnityEngine.Random.value is [0,1] inclusive:
                    //    for 0% probability it's fine because it's never '< 0'
                    //    for 100% probability it's not because it's not always '< 1', it might be == 1
                    //    and if we use '<=' instead then it won't work for 0%
                    // => C#'s Random value is [0,1) exclusive like most random
                    //    functions. this works fine.
                    if (new System.Random().NextDouble() < recipe.probability)
                    {
                        // add result item to inventory
                        inventory.Add(result, 1);
                        TargetCraftingSuccess();
                    }
                    else
                    {
                        TargetCraftingFailed();
                    }
                }
            }
        }
    }

    // two rpcs for results to save 1 byte for the actual result
    [TargetRpc] // only send to one client
    public void TargetCraftingSuccess()
    {
        craftingState = CraftingState.Success;
    }

    [TargetRpc] // only send to one client
    public void TargetCraftingFailed()
    {
        craftingState = CraftingState.Failed;
    }

    // drag & drop /////////////////////////////////////////////////////////////
    void OnDragAndDrop_InventorySlot_CraftingIngredientSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        // only if not crafting right now
        if (craftingState != CraftingState.InProgress)
        {
            if (!indices.Contains(slotIndices[0]))
            {
                indices[slotIndices[1]] = slotIndices[0];
                craftingState = CraftingState.None; // reset state
            }
        }
    }

    void OnDragAndDrop_CraftingIngredientSlot_CraftingIngredientSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        // only if not crafting right now
        if (craftingState != CraftingState.InProgress)
        {
            // just swap them clientsided
            int temp = indices[slotIndices[0]];
            indices[slotIndices[0]] = indices[slotIndices[1]];
            indices[slotIndices[1]] = temp;
            craftingState = CraftingState.None; // reset state
        }
    }

    void OnDragAndClear_CraftingIngredientSlot(int slotIndex)
    {
        // only if not crafting right now
        if (craftingState != CraftingState.InProgress)
        {
            indices[slotIndex] = -1;
            craftingState = CraftingState.None; // reset state
        }
    }
}