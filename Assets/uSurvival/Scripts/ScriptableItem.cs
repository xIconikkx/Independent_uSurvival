// Saves the item info in a ScriptableObject that can be used ingame by
// referencing it from a MonoBehaviour. It only stores an item's static data.
//
// We also add each one to a dictionary automatically, so that all of them can
// be found by name without having to put them all in a database. Note that we
// have to put them all into the Resources folder and use Resources.LoadAll to
// load them. This is important because some items may not be referenced by any
// entity ingame (e.g. when a special event item isn't dropped anymore after the
// event). But all items should still be loadable from the database, even if
// they are not referenced by anyone anymore. So we have to use Resources.Load.
// (before we added them to the dict in OnEnable, but that's only called for
//  those that are referenced in the game. All others will be ignored be Unity.)
//
// An Item can be created by right clicking the Resources folder and selecting
// Create->uSurvival Item. Existing items can be found in the Resources folder.
//
// Note: this class is not abstract so we can create 'useless' items like recipe
// ingredients, etc.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName="uSurvival Item/General", order=999)]
public class ScriptableItem : ScriptableObject
{
    [Header("Base Stats")]
    public int maxStack = 1; // set default already
    [Tooltip("Durability is only allowed for non-stackable items (if MaxStack is 1))")]
    public int maxDurability = 1000; // all items should have durability
    public bool destroyable;
    [TextArea(1, 30)] public string toolTip;
    public Sprite image;

    [Header("3D Representation")]
    public ItemDrop drop; // spawned when players die
    public GameObject modelPrefab; // shown when equipped/in hands/etc.

    [Header("Default Item Price")]
    public int itemPrice = 25;

    // tooltip /////////////////////////////////////////////////////////////////
    // fill in all variables into the tooltip
    // this saves us lots of ugly string concatenation code. we can't do it in
    // ScriptableItem because some variables can only be replaced here, hence we
    // would end up with some variables not replaced in the string when calling
    // Tooltip() from the template.
    // -> note: each tooltip can have any variables, or none if needed
    // -> example usage:
    /*
    <b>{NAME}</b>
    Description here...

    Destroyable: {DESTROYABLE}
    Amount: {AMOUNT}
    */
    public virtual string ToolTip()
    {
        // we use a StringBuilder so that addons can modify tooltips later too
        // ('string' itself can't be passed as a mutable object)
        StringBuilder tip = new StringBuilder(toolTip);
        tip.Replace("{NAME}", name);
        tip.Replace("{DESTROYABLE}", (destroyable ? "Yes" : "No"));
        return tip.ToString();
    }

    // validation //////////////////////////////////////////////////////////////
    // -> virtual so inheriting items can use it too
    protected virtual void OnValidate()
    {
        // with durability, there is an odd situation where items with maxstack
        // bigger than 1 would not be stackable if they have different
        // durability. this causes a lot of gameplay frustration and can feel
        // like a bug.
        // -> let's limit durability to items that aren't stackable. everything
        //    else is just way too confusing.
        if (maxStack > 1 && maxDurability != 0)
        {
            maxDurability = 0;
            Debug.LogWarning(name + " maxDurability was reset to 0 because it's not stackable. Set maxStack to 1 if you want to use durability.");
        }
    }

    // caching /////////////////////////////////////////////////////////////////
    // we can only use Resources.Load in the main thread. we can't use it when
    // declaring static variables. so we have to use it as soon as 'dict' is
    // accessed for the first time from the main thread.
    // -> we save the hash so the dynamic item part doesn't have to contain and
    //    sync the whole name over the network
    static Dictionary<int, ScriptableItem> cache;
    public static Dictionary<int, ScriptableItem> dict
    {
        get
        {
            // not loaded yet?
            if (cache == null)
            {
                // get all ScriptableItems in resources
                ScriptableItem[] items = Resources.LoadAll<ScriptableItem>("");

                // check for duplicates, then add to cache
                List<string> duplicates = items.ToList().FindDuplicates(item => item.name);
                if (duplicates.Count == 0)
                {
                    cache = items.ToDictionary(item => item.name.GetStableHashCode(), item => item);
                }
                else
                {
                    foreach (string duplicate in duplicates)
                        Debug.LogError("Resources folder contains multiple ScriptableItems with the name " + duplicate + ". If you are using subfolders like 'Warrior/Ring' and 'Archer/Ring', then rename them to 'Warrior/(Warrior)Ring' and 'Archer/(Archer)Ring' instead.");
                }
            }
            return cache;
        }
    }
}

// ScriptableItem + Amount is useful for default items (e.g. spawn with 10 potions)
[Serializable]
public struct ScriptableItemAndAmount
{
    public ScriptableItem item;
    public int amount;
}
