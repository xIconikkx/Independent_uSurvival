// The Item struct only contains the dynamic item properties so that the static
// properties can be read from the scriptable object and don't have to be synced
// over the network.
//
// Items have to be structs in order to work with SyncLists.
//
// Use .Equals to compare two items. Comparing the name is NOT enough for cases
// where dynamic stats differ. E.g. two pets with different levels shouldn't be
// merged.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mirror;

[Serializable]
public struct Item
{
    // hashcode used to reference the real ScriptableItem (can't link to data
    // directly because synclist only supports simple types). and syncing a
    // string's hashcode instead of the string takes WAY less bandwidth.
    public int hash;

    // ammo for weapon items
    public int ammo;

    // current durability
    public int durability;

    // constructors
    public Item(ScriptableItem data)
    {
        hash = data.name.GetStableHashCode();
        ammo = 0;
        durability = data.maxDurability;
    }

    // database item property access
    public ScriptableItem data
    {
        get
        {
            // show a useful error message if the key can't be found
            // note: ScriptableItem.OnValidate 'is in resource folder' check
            //       causes Unity SendMessage warnings and false positives.
            //       this solution is a lot better.
            if (!ScriptableItem.dict.ContainsKey(hash))
                throw new KeyNotFoundException("There is no ScriptableItem with hash=" + hash + ". Make sure that all ScriptableItems are in the Resources folder so they are loaded properly.");
            return ScriptableItem.dict[hash];
        }
    }
    public string name => data.name;
    public int maxStack => data.maxStack;
    public int maxDurability => data.maxDurability;
    public float DurabilityPercent()
    {
        return (durability != 0 && maxDurability != 0) ? (float)durability / (float)maxDurability : 0;
    }
    public bool destroyable => data.destroyable;
    public Sprite image => data.image;

    // tooltip
    public string ToolTip()
    {
        // we use a StringBuilder so that addons can modify tooltips later too
        // ('string' itself can't be passed as a mutable object)
        StringBuilder tip = new StringBuilder(data.ToolTip());
        tip.Replace("{AMMO}", ammo.ToString());
        // show durability only if item has durability
        if (maxDurability > 0)
            tip.Replace("{DURABILITY}", (DurabilityPercent() * 100).ToString("F0"));
        return tip.ToString();
    }
}