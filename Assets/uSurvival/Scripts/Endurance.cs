using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// inventory, attributes etc. can influence max
// (no recovery bonus because it makes no sense (physically/biologically)
public interface IEnduranceBonus
{
    int GetEnduranceBonus(int baseEndurance);
}

[Serializable]
public class DrainState
{
    public MoveState state;
    public int drain;
}

[DisallowMultipleComponent]
public class Endurance : Energy
{
    [Header("Components")]
    public PlayerMovement movement;

    [Header("Configuration")]
    public int _recoveryPerTick = 1;
    public int baseEndurance = 10;

    public List<DrainState> drainStates = new List<DrainState>{
        new DrainState{state = MoveState.RUNNING, drain = -1},
        new DrainState{state = MoveState.AIRBORNE, drain = -1}
    };

    // cache components that give a bonus (attributes, inventory, etc.)
    IEnduranceBonus[] bonusComponents;
    void Awake()
    {
        bonusComponents = GetComponentsInChildren<IEnduranceBonus>();
    }

    public override int max
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (IEnduranceBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetEnduranceBonus(baseEndurance);
            return baseEndurance + bonus;
        }
    }

    public bool IsInDrainState(out DrainState drainState)
    {
        // search manually. Linq.Find is HEAVY(!) on GC and performance
        foreach (DrainState drain in drainStates)
        {
            if (drain.state == movement.state)
            {
                drainState = drain;
                return true;
            }
        }
        drainState = null;
        return false;
    }

    public override int recoveryPerTick
    {
        get
        {
            // in a state that drains it? otherwise recover
            return IsInDrainState(out DrainState drainState)
                   ? drainState.drain
                   : _recoveryPerTick;
        }
    }
}