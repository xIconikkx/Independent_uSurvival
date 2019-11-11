using System;
using UnityEngine;
using UnityEngine.Events;
using Mirror;
using UnityEngine.Serialization;

// inventory, attributes etc. can influence max health
public interface ICombatBonus
{
    int GetDamageBonus();
    int GetDefenseBonus();
}

[Serializable] public class UnityEventGameObjectInt : UnityEvent<GameObject, int> {}

public class Combat : NetworkBehaviour
{
    // invincibility is useful for GMs etc.
    public int baseDamage;
    public int baseDefense;
    public GameObject onDamageEffect;

    // it's useful to know an entity's last combat time (did/was attacked)
    // e.g. to prevent logging out for x seconds after combat
    [SyncVar] public double lastCombatTime;

    // events
    [FormerlySerializedAs("onReceivedDamage")]
    public UnityEventGameObjectInt onServerReceivedDamage;
    public UnityEventGameObjectInt onClientReceivedDamage;

    // cache components that give a bonus (attributes, inventory, etc.)
    ICombatBonus[] bonusComponents;
    void Awake()
    {
        bonusComponents = GetComponentsInChildren<ICombatBonus>();
    }

    // calculate damage
    public int damage
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetDamageBonus();
            return baseDamage + bonus;
        }
    }

    // calculate defense
    public int defense
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetDefenseBonus();
            return baseDefense + bonus;
        }
    }

    // deal damage while acknowledging the target's defense etc.
    public void DealDamageAt(GameObject other, int amount, Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
    {
        if (other != null)
        {
            Health otherHealth = other.GetComponent<Health>();
            Combat otherCombat = other.GetComponent<Combat>();
            if (otherHealth != null && otherCombat != null)
            {
                // not dead yet?
                if (otherHealth.current > 0)
                {
                    // extra damage on that collider? (e.g. on head)
                    DamageArea damageArea = hitCollider.GetComponent<DamageArea>();
                    float multiplier = damageArea != null ? damageArea.multiplier : 1;
                    int amountMultiplied = Mathf.RoundToInt(amount * multiplier);

                    // subtract defense (but leave at least 1 damage, otherwise
                    // it may be frustrating for weaker players)
                    int damageDealt = Mathf.Max(amountMultiplied - otherCombat.defense, 1);

                    // deal the damage
                    otherHealth.current -= damageDealt;

                    // call OnServerReceivedDamage event on the target
                    // -> can be used for monsters to pull aggro
                    // -> can be used by equipment to decrease durability etc.
                    otherCombat.onServerReceivedDamage.Invoke(gameObject, damageDealt);

                    // show effects on clients
                    otherCombat.RpcOnReceivedDamage(damageDealt, hitPoint, hitNormal);

                    // reset last combat time for both
                    lastCombatTime = NetworkTime.time;
                    otherCombat.lastCombatTime = NetworkTime.time;
                }
            }
        }
    }

    [ClientRpc]
    public void RpcOnReceivedDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        // show damage effect (if any)
        if (onDamageEffect)
            Instantiate(onDamageEffect, hitPoint, Quaternion.LookRotation(-hitNormal));

        // call OnServerReceivedDamage event on the target
        onClientReceivedDamage.Invoke(gameObject, amount);
    }
}