// Finite State Machine
// -> we should react to every state and to every event for correctness
// -> we keep it functional for simplicity
using UnityEngine;
using Mirror;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NetworkProximityGridChecker))]
public abstract class FiniteStateMachine : NetworkBehaviour
{
    // Used components. Assign in Inspector. Easier than GetComponent caching.
    public Health health;
    public NetworkProximityGridChecker proximityChecker;

    // state
    [SyncVar] public string state = "IDLE";

    // [SyncVar] NetworkIdentity: errors when null
    // [SyncVar] Entity: SyncVar only works for simple types
    // [SyncVar] GameObject is the only solution where we don't need a custom
    //           synchronization script (needs NetworkIdentity component!)
    // -> we still wrap it with a property for easier access, so we don't have
    //    to use target.GetComponent<Entity>() everywhere
    [SyncVar, HideInInspector] public GameObject target;

    public override void OnStartServer()
    {
        // change to dead if we spawned with 0 health
        if (health.current == 0) state = "DEAD";
    }

    // visibility //////////////////////////////////////////////////////////////
    // is the entity currently hidden?
    // note: usually the server is the only one who uses forceHidden, the
    //       client usually doesn't know about it and simply doesn't see the
    //       GameObject.
    public bool IsHidden() => proximityChecker.forceHidden;

    // monsters, npcs etc. don't have to be updated if no player is around
    // checking observers is enough, because lonely players have at least
    // themselves as observers, so players will always be updated
    // and dead monsters will respawn immediately in the first update call
    // even if we didn't update them in a long time (because of the 'end'
    // times)
    // -> update only if:
    //    - observers are null (they are null in clients)
    //    - if they are not null, then only if at least one (on server)
    //    - if the entity is hidden, otherwise it would never be updated again
    //      because it would never get new observers
    public bool IsWorthUpdating() =>
        netIdentity.observers == null ||
        netIdentity.observers.Count > 0 ||
        IsHidden();

    // entity logic will be implemented with a finite state machine
    // -> we should react to every state and to every event for correctness
    // -> we keep it functional for simplicity
    // note: can still use LateUpdate for Updates that should happen in any case
    void Update()
    {
        // only update if it's worth updating (see IsWorthUpdating comments)
        // -> we also clear the target if it's hidden, so that players don't
        //    keep hidden (respawning) monsters as target, hence don't show them
        //    as target again when they are shown again
        if (IsWorthUpdating())
        {
            if (isClient) UpdateClient();
            if (isServer)
            {
                if (target != null && target.GetComponent<NetworkProximityGridChecker>().forceHidden) target = null;
                state = UpdateServer();
            }
        }
    }

    // update for server. should return the new state.
    protected abstract string UpdateServer();

    // update for client.
    protected abstract void UpdateClient();

    // this function is called by the AggroArea (if any) on clients and server
    public virtual void OnAggro(GameObject go) {}
}