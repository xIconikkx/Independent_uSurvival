// crafting recipe for the type of crafting that the player can do all the time
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName="New Recipe", menuName="uSurvival Recipe/Crafting", order=999)]
public class CraftingRecipe : ScriptableRecipe
{
    // fixed ingredient size for all recipes
    public static int recipeSize = 6;

    // ingredients
    public List<ScriptableItemAndAmount> ingredients = new List<ScriptableItemAndAmount>(6);

    // probability of success
    [Range(0, 1)] public float probability = 1;

    // helper function to check if an item slot list has at least one valid item
    bool IngredientsNotEmpty()
    {
        // avoid Linq for performance / GC
        foreach (ScriptableItemAndAmount slot in ingredients)
            if (slot.amount > 0 && slot.item != null)
                return true;
        return false;
    }

    // check if the list of items works for this recipe. the list shouldn't
    // contain 'null'.
    public virtual bool CanCraftWith(List<ItemSlot> items)
    {
        // add all non-empty slots to the list of items that need to be checked
        List<ItemSlot> checkItems = new List<ItemSlot>();
        foreach (ItemSlot slot in items)
            if (slot.amount > 0)
                checkItems.Add(slot);

        // make sure that we have at least one ingredient
        if (IngredientsNotEmpty())
        {
            // each ingredient in the list, with amount?
            foreach (ScriptableItemAndAmount ingredient in ingredients)
            {
                if (ingredient.amount > 0 && ingredient.item != null)
                {
                    // is there a stack with at least that amount and that item?
                    int index = checkItems.FindIndex(slot => slot.amount >= ingredient.amount && slot.item.data == ingredient.item);
                    if (index != -1)
                        checkItems.RemoveAt(index);
                    else
                        return false;
                }
            }

            // and nothing else in the list?
            return checkItems.Count == 0;
        }
        else return false;
    }

    // caching /////////////////////////////////////////////////////////////////
    // we can only use Resources.Load in the main thread. we can't use it when
    // declaring static variables. so we have to use it as soon as 'dict' is
    // accessed for the first time from the main thread.
    //
    // (ScriptableRecipe already has a dict, but we add a separate one only for
    //  CraftingRecipes so we don't have to search through others, e.g.
    //  FurnaceRecipes)
    static Dictionary<string, CraftingRecipe> cacheCrafting = null;
    public static Dictionary<string, CraftingRecipe> dictCrafting
    {
        get
        {
            // not loaded yet?
            if (cacheCrafting == null)
            {
                // get all ScriptableRecipes in resources
                CraftingRecipe[] recipes = Resources.LoadAll<CraftingRecipe>("");

                // check for duplicates, then add to cache
                List<string> duplicates = recipes.ToList().FindDuplicates(recipe => recipe.name);
                if (duplicates.Count == 0)
                {
                    cacheCrafting = recipes.ToDictionary(recipe => recipe.name, recipe => recipe);
                }
                else
                {
                    foreach (string duplicate in duplicates)
                        Debug.LogError("Resources folder contains multiple CraftingRecipes with the name " + duplicate + ". If you are using subfolders like 'Warrior/Ring' and 'Archer/Ring', then rename them to 'Warrior/(Warrior)Ring' and 'Archer/(Archer)Ring' instead.");
                }
            }
            return cacheCrafting;
        }
    }

    // find a recipe based on item slots
    public static CraftingRecipe Find(List<ItemSlot> items)
    {
        // avoid Linq for performance
        foreach (CraftingRecipe recipe in dictCrafting.Values)
            if (recipe.CanCraftWith(items))
                return recipe;
        return null;
    }

    // validation //////////////////////////////////////////////////////////////
    void OnValidate()
    {
        // force list size
        // -> add if too few
        for (int i = ingredients.Count; i < recipeSize; ++i)
            ingredients.Add(new ScriptableItemAndAmount());

        // -> remove if too many
        for (int i = recipeSize; i < ingredients.Count; ++i)
            ingredients.RemoveAt(i);

        // set amount to at least '1' in each occupied slot. otherwise it's
        // too easy to assign an item and forget to set amount from 0 to 1!
        for (int i = 0; i < ingredients.Count; ++i)
        {
            ScriptableItemAndAmount ingredient = ingredients[i];
            if (ingredient.item != null)
            {
                ingredient.amount = Mathf.Clamp(ingredient.amount, 1, ingredient.item.maxStack);
                ingredients[i] = ingredient;
            }
        }
    }
}
