using UnityEngine;
using Mirror;

// inventory, attributes etc. can influence max
public interface INutritionBonus
{
    int GetNutritionBonus(int baseNutrition);
    int GetNutritionRecoveryBonus();
}

[DisallowMultipleComponent]
public class Nutrition : Energy
{
    public int baseRecoveryPerTick = -1;
    public int baseNutrition = 100;

    // cache components that give a bonus (attributes, inventory, etc.)
    INutritionBonus[] bonusComponents;
    void Awake()
    {
        bonusComponents = GetComponentsInChildren<INutritionBonus>();
    }

    public override int max
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (INutritionBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetNutritionBonus(baseNutrition);
            return baseNutrition + bonus;
        }
    }

    public override int recoveryPerTick
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (INutritionBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetNutritionRecoveryBonus();
            return baseRecoveryPerTick + bonus;
        }
    }
}