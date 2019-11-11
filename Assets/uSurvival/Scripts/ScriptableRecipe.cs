// Saves the crafting recipe info in a ScriptableObject that can be used ingame
// by referencing it from a MonoBehaviour. It only stores static data.
//
// We also add each one to a dictionary automatically, so that all of them can
// be found by name without having to put them all in a database. Note that we
// have to put them all into the Resources folder and use Resources.LoadAll to
// load them. This is important because some recipes may not be referenced by
// any entity ingame. But all recipes should still be loadable from the
// database, even if they are not referenced by anyone anymore. So we have to
// use Resources.Load. (before we added them to the dict in OnEnable, but that's
// only called for those that are referenced in the game. All others will be
// ignored be Unity.)
//
// A Recipe can be created by right clicking the Resources folder and selecting
// Create -> uSurvival Recipe. Existing recipes can be found in the Resources
// folder.
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public abstract class ScriptableRecipe : ScriptableObject
{
    // every recipe has a result
    public ScriptableItem result;

    // caching /////////////////////////////////////////////////////////////////
    // we can only use Resources.Load in the main thread. we can't use it when
    // declaring static variables. so we have to use it as soon as 'dict' is
    // accessed for the first time from the main thread.
    static Dictionary<string, ScriptableRecipe> cache = null;
    public static Dictionary<string, ScriptableRecipe> dict
    {
        get
        {
            // not loaded yet?
            if (cache == null)
            {
                // get all ScriptableRecipes in resources
                ScriptableRecipe[] recipes = Resources.LoadAll<ScriptableRecipe>("");

                // check for duplicates, then add to cache
                List<string> duplicates = recipes.ToList().FindDuplicates(recipe => recipe.name);
                if (duplicates.Count == 0)
                {
                    cache = recipes.ToDictionary(recipe => recipe.name, recipe => recipe);
                }
                else
                {
                    foreach (string duplicate in duplicates)
                        Debug.LogError("Resources folder contains multiple ScriptableRecipes with the name " + duplicate + ". If you are using subfolders like 'Warrior/Ring' and 'Archer/Ring', then rename them to 'Warrior/(Warrior)Ring' and 'Archer/(Archer)Ring' instead.");
                }
            }
            return cache;
        }
    }
}
