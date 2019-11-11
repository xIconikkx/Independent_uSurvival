// furnace recipe for the type of crafting that the player can do only at a
// furnace
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName="New Recipe", menuName="uSurvival Recipe/Furnace", order=999)]
public class FurnaceRecipe : ScriptableRecipe
{
    // furnace recipes only have 1 ingredient item
    // (a furnace is not a blender. it only heats up one thing)
    public ScriptableItem ingredient;
    public ScriptableItem fuel;

    [Tooltip("The baking time in seconds.")]
    public float bakingTime = 5;

    // check if the list of items works for this recipe. the list shouldn't
    // contain 'null'.
    public bool CanCraftWith(ItemSlot ingredientSlot, ItemSlot fuelSlot)
    {
        // check ingredients and check if correct fuel is supplied
        return ingredientSlot.amount > 0 &&
               ingredientSlot.item.data == ingredient &&
               fuelSlot.amount > 0 &&
               fuelSlot.item.data == fuel;
    }

    // caching /////////////////////////////////////////////////////////////////
    // we can only use Resources.Load in the main thread. we can't use it when
    // declaring static variables. so we have to use it as soon as 'dict' is
    // accessed for the first time from the main thread.
    //
    // (ScriptableRecipe already has a dict, but we add a separate one only for
    //  CraftingRecipes so we don't have to search through others, e.g.
    //  FurnaceRecipes)
    static Dictionary<string, FurnaceRecipe> cacheFurnace = null;
    public static Dictionary<string, FurnaceRecipe> dictFurnace
    {
        get
        {
            // not loaded yet?
            if (cacheFurnace == null)
            {
                // get all ScriptableRecipes in resources
                FurnaceRecipe[] recipes = Resources.LoadAll<FurnaceRecipe>("");

                // check for duplicates, then add to cache
                List<string> duplicates = recipes.ToList().FindDuplicates(recipe => recipe.name);
                if (duplicates.Count == 0)
                {
                    cacheFurnace = recipes.ToDictionary(recipe => recipe.name, recipe => recipe);
                }
                else
                {
                    foreach (string duplicate in duplicates)
                        Debug.LogError("Resources folder contains multiple FurnaceRecipes with the name " + duplicate + ". If you are using subfolders like 'Warrior/Ring' and 'Archer/Ring', then rename them to 'Warrior/(Warrior)Ring' and 'Archer/(Archer)Ring' instead.");
                }
            }
            return cacheFurnace;
        }
    }

    // find a recipe based on item slots
    public static FurnaceRecipe Find(ItemSlot ingredientSlot, ItemSlot fuelSlot)
    {
        // avoid Linq for performance
        foreach (FurnaceRecipe recipe in dictFurnace.Values)
            if (recipe.CanCraftWith(ingredientSlot, fuelSlot))
                return recipe;
        return null;
    }
}
