using UnityEngine;
using Mirror;

// inventory, attributes etc. can influence max
public interface IHydrationBonus
{
    int GetHydrationBonus(int baseHydration);
    int GetHydrationRecoveryBonus();
}

[DisallowMultipleComponent]
public class Hydration : Energy
{
    public int baseRecoveryPerTick = -1;
    public int baseHydration = 100;

    // cache components that give a bonus (attributes, inventory, etc.)
    IHydrationBonus[] bonusComponents;
    void Awake()
    {
        bonusComponents = GetComponentsInChildren<IHydrationBonus>();
    }

    public override int max
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (IHydrationBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetHydrationBonus(baseHydration);
            return baseHydration + bonus;
        }
    }

    public override int recoveryPerTick
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (IHydrationBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetHydrationRecoveryBonus();
            return baseRecoveryPerTick + bonus;
        }
    }
}