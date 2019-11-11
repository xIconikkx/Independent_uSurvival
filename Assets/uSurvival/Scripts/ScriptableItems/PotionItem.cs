using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName="uSurvival Item/Potion", order=999)]
public class PotionItem : UsableItem
{
    [Header("Potion")]
    public int usageHealth;
    public int usageHydration;
    public int usageNutrition;

    // note: no need to overwrite CanUse functions. simply check cooldowns in base.

    void ApplyEffects(GameObject player)
    {
        player.GetComponent<Health>().current += usageHealth;
        player.GetComponent<Hydration>().current += usageHydration;
        player.GetComponent<Nutrition>().current += usageNutrition;
    }

    public override void Use(PlayerInventory inventory, int inventoryIndex)
    {
        // call base function to start cooldown
        base.Use(inventory, inventoryIndex);

        ApplyEffects(inventory.gameObject);

        // decrease amount
        ItemSlot slot = inventory.slots[inventoryIndex];
        slot.DecreaseAmount(1);
        inventory.slots[inventoryIndex] = slot;
    }
    public override void Use(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // call base function to start cooldown
        base.Use(hotbar, hotbarIndex, lookAt);

        ApplyEffects(hotbar.gameObject);

        // decrease amount
        ItemSlot slot = hotbar.slots[hotbarIndex];
        slot.DecreaseAmount(1);
        hotbar.slots[hotbarIndex] = slot;
    }

    // tooltip
    public override string ToolTip()
    {
        StringBuilder tip = new StringBuilder(base.ToolTip());
        tip.Replace("{USAGEHEALTH}", usageHealth.ToString());
        tip.Replace("{USAGEHYDRATION}", usageHydration.ToString());
        tip.Replace("{USAGENUTRITION}", usageNutrition.ToString());
        return tip.ToString();
    }
}
