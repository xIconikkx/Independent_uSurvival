using UnityEngine;
using Mirror;

// inventory, attributes etc. can influence max
public interface ITemperatureBonus
{
    int GetTemperatureRecoveryBonus();
}

[DisallowMultipleComponent]
public class Temperature : Energy
{
    public int baseRecoveryPerTick = -1;

    // degree celsius * 100 so we can lose 0.01°C every second instead of 1°C
    // every second, which should drain it in 36s then
    // (increasing tick rate causes too slow feedback when warming up on a fire)
    // => 3650 equals 36.50°C, which is the normal body temperature
    [SerializeField] int _max = 3650;
    public override int max { get { return _max; } }

    // current heat source: no [SyncVar] because OnTriggerEnter/Exit is called
    // on client and server anyway
    HeatSource currentHeatSource;

    // cache components that give a bonus (attributes, inventory, etc.)
    ITemperatureBonus[] bonusComponents;
    void Awake()
    {
        bonusComponents = GetComponentsInChildren<ITemperatureBonus>();
    }

    public override int recoveryPerTick
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (ITemperatureBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetTemperatureRecoveryBonus();
            return baseRecoveryPerTick + bonus + (currentHeatSource ? currentHeatSource.recoveryBonus : 0);
        }
    }

    // [Client] & [Server] so we don't need a SyncVar
    void OnTriggerEnter(Collider co)
    {
        // heat source?
        HeatSource heatSource = co.GetComponent<HeatSource>();
        if (heatSource != null)
        {
            // none yet?
            if (currentHeatSource == null)
            {
                currentHeatSource = heatSource;
            }
            // otherwise keep closest one
            else if (currentHeatSource != heatSource) // different one? otherwise don't bother with calculations
            {
                float oldDistance = Vector3.Distance(transform.position, currentHeatSource.transform.position);
                float newDistance = Vector3.Distance(transform.position, heatSource.transform.position);
                if (newDistance < oldDistance)
                    currentHeatSource = heatSource;
            }
        }
    }

    // [Client] & [Server] so we don't need a SyncVar
    void OnTriggerExit(Collider co)
    {
        // clear current heat source (if any)
        if (currentHeatSource != null && currentHeatSource.transform == co.transform)
            currentHeatSource = null;
    }
}