// common type for all kinds of weapons. we need a common type to check what's
// allowed on the hotbar, etc.
using System.Text;
using UnityEngine;

public abstract class WeaponItem : UsableItem
{
    [Header("Weapon")]
    public float attackRange = 20; // attack range
    public int damage = 10;
    public string upperBodyAnimationParameter;

    // usage: disable inventory usage for weapons. only from hotbar.
    // (right clicking a rifle in the inventory to shoot it would be odd)
    public override Usability CanUse(PlayerInventory inventory, int inventoryIndex) { return Usability.Never; }
    public override void Use(PlayerInventory inventory, int hotbarIndex) {}

    // tooltip
    public override string ToolTip()
    {
        StringBuilder tip = new StringBuilder(base.ToolTip());
        tip.Replace("{ATTACKRANGE}", attackRange.ToString());
        tip.Replace("{DAMAGE}", damage.ToString());
        return tip.ToString();
    }
}
