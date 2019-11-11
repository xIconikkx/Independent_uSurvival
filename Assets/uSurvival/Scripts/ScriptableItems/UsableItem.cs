// only usable items need usage functions
// we need bindings for inventory and hotbar, so that inheriting templates
// can decide if they want to be usable from inventory, or only from hotbar,
// etc.
// => drinks might be usable everywhere, but weapons only from hotbar etc.
// => some hotbar items might need current lookat point too, e.g. to fire a
//    weapon or build something where the player is looking, etc.
using System.Text;
using UnityEngine;

// a bool is not enough for CanUse result, since each usable item can either be:
// - useable
// - cooldown (fire rate, potion cooldown, equipment )
// - empty (no ammo, empty water bottle, etc.)
// - never usable by this person or from this slot or whatever
public enum Usability : byte { Usable, Cooldown, Empty, Never }

public abstract class UsableItem : ScriptableItem
{
    // equipment, weapons, tools etc. need a category to decide which slots it
    // can fit into
    [Header("Category")]
    public string category;

    [Header("Usage")]
    public bool keepUsingWhileButtonDown; // guns should keep shooting, buildings shouldn't be kept building while holding, etc.
    public AudioClip successfulUseSound; // swinging axe at enemy etc.
    public AudioClip failedUseSound; // swinging axe but into the air etc.
    public AudioClip emptySound; // weapon 'clicking' if magazine is empty, etc.

    [Header("Cooldown")]
    public float cooldown; // weapon fire rate, potion usage interval, etc.
    [Tooltip("Cooldown category can be used if different potion items should share the same cooldown. Cooldown applies only to this item name if empty.")]
#pragma warning disable CS0649 // Field never assigned to
    [SerializeField] string _cooldownCategory; // leave empty for itemname based cooldown. fill in for category.
#pragma warning restore CS0649 // Field never assigned to
    public string cooldownCategory =>
        // defaults to per-item-name cooldown if empty. otherwise category.
        string.IsNullOrWhiteSpace(_cooldownCategory) ? name : _cooldownCategory;

    public bool shoulderLookAtWhileHolding;

    // if we select it on the hotbar by pressing the hotkey, should we select
    // the slot (and hold it in hands), or use it immediately (like a potion)?
    public bool useDirectly;

    // [Server] and [Client] CanUse checks for UI, Commands, etc.
    public virtual Usability CanUse(PlayerInventory inventory, int inventoryIndex)
    {
        // base cooldown check
        return inventory.GetComponent<Player>().GetItemCooldown(cooldownCategory) > 0
               ? Usability.Cooldown
               : Usability.Usable;
    }
    public virtual Usability CanUse(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // base cooldown check
        return hotbar.GetComponent<Player>().GetItemCooldown(cooldownCategory) > 0
               ? Usability.Cooldown
               : Usability.Usable;
    }

    // [Server] Use logic
    public virtual void Use(PlayerInventory inventory, int hotbarIndex)
    {
        // start cooldown (if any)
        // -> no need to set sync dict dirty if we have no cooldown
        if (cooldown > 0)
            inventory.GetComponent<Player>().SetItemCooldown(cooldownCategory, cooldown);
    }
    public virtual void Use(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // start cooldown (if any)
        // -> no need to set sync dict dirty if we have no cooldown
        if (cooldown > 0)
            hotbar.GetComponent<Player>().SetItemCooldown(cooldownCategory, cooldown);
    }

    // [Client] OnUse Rpc callback for effects, sounds, etc.
    // -> can't pass Inventory+slotIndex because .Use might clear it before getting here already
    // -> should always simulate a Use() again to decide which sounds to play etc.,
    //    so that we can simulate it for local player to avoid latency effects.
    //    (passing a 'result' bool wouldn't allow us to call OnUsed without Use() theN)
    public virtual void OnUsed(PlayerInventory inventory) {}
    public virtual void OnUsed(PlayerHotbar hotbar, Vector3 lookAt) {}

    // tooltip
    public override string ToolTip()
    {
        StringBuilder tip = new StringBuilder(base.ToolTip());
        tip.Replace("{COOLDOWN}", cooldown.ToString());
        return tip.ToString();
    }
}
