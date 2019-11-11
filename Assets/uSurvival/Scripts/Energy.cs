// For Health, Mana, etc.
using UnityEngine;
using UnityEngine.Events;
using Mirror;

public abstract class Energy : NetworkBehaviour
{
    // current value
    // set & get: keep between min and max
    [SyncVar] int _current = 0;
    public int current
    {
        get { return Mathf.Min(_current, max); }
        set
        {
            bool emptyBefore = _current == 0;
            _current = Mathf.Clamp(value, 0, max);
            if (_current == 0 && !emptyBefore) onEmpty.Invoke();
        }
    }

    // maximum value (may depend on buffs, items, etc.)
    public abstract int max { get; }

    // recovery tick rate
    // (1/s for health, but might need to be less for something like temperature
    //  where we don't want to reduce it by -1C per second, etc.)
    public int recoveryTickRate = 1;

    // recovery per tick (may depend on buffs, items etc.)
    // -> 'recoveryRate' sounds like 1/s, but 'PerTick' makes it 100% clear
    public abstract int recoveryPerTick { get; }

    // don't recover while dead. all energy scripts need to check Health.
    public Health health;

    // recovery can swap over, for example:
    //   once nutrition is full it increases health
    //   once nutrition is empty it decreases health
    public Energy overflowInto;
    public Energy underflowInto;

    // spawn with full energy? important for monsters, etc.
    public bool spawnFull = true;

    [Header("Events")]
    public UnityEvent onEmpty;

    public override void OnStartServer()
    {
        // set full energy on start if needed
        if (spawnFull) current = max;

        // recovery every 'TickRate'
        InvokeRepeating(nameof(Recover), recoveryTickRate, recoveryTickRate);
    }

    // get percentage
    public float Percent() =>
        (current != 0 && max != 0) ? (float)current / (float)max : 0;

    // recover once a second
    [Server]
    public void Recover()
    {
        if (enabled && health.current > 0)
        {
            // calculate over/underflowing value (might be <0 or >max)
            int next = current + recoveryPerTick;

            // assign current in range [0,max]
            current = next;

            // apply underflow (if any) by '-n'
            // (if next=-3 then underflow+=(-3))
            if (next < 0 && underflowInto != null)
                underflowInto.current += next;
            // apply overflow (if any)
            // (if next is bigger max then diff to max is 'next-max')
            else if (next > max && overflowInto)
                overflowInto.current += (next - max);
        }
    }
}
