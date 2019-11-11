using System.Collections.Generic;
using UnityEngine;
using Mirror;

// make sure that drops aren't sent to all players on the server, only to those
// that are close enough
[RequireComponent(typeof(NetworkProximityGridChecker))] // don't send drops to all players, just the close ones
[RequireComponent(typeof(Collider))] // needed for looting raycasting
public class ItemDrop : NetworkBehaviour, Interactable
{
    [Header("Components")]
    public NetworkProximityGridChecker proximityChecker;

    [Header("Item")]
    // default itemData, can be assigned in Inspector
#pragma warning disable CS0649 // Field is never assigned to
    [SerializeField] ScriptableItem itemData; // not public, so that people use .item & .amount
#pragma warning restore CS0649 // Field is never assigned to

    // drops need a real Item + amount so that we can set dynamic stats like ammo
    // note: we don't use 'ItemSlot' so that 'amount' can be assigned in Inspector for default spawns
    [SyncVar] public int amount = 1; // sometimes set on server, needs to sync
    [SyncVar, HideInInspector] public Item item;

    [Header("Item Spawning")]
    public bool respawn; // set to true for scene objects to make them respawn
    public float respawnInterval;

    // colliders
    Collider[] colliders;

    void Awake()
    {
        // cache colliders once to avoid GetComponentsInChildren calls at runtime
        // => some items may have more than one collider (e.g. crossbow), so we
        //    really do need to find them all
        colliders = GetComponentsInChildren<Collider>();
    }

    public override void OnStartServer()
    {
        // create slot from template, unless we assigned it manually already
        // (e.g. if an item spawner assigns it after instantiating it)
        if (item.hash == 0 && itemData != null)
            item = new Item(itemData);
    }

    // interactable ////////////////////////////////////////////////////////////
    public string GetInteractionText()
    {
        if (Player.localPlayer != null && itemData != null && amount > 0)
            return amount > 1 ? item.name + " x " + amount : item.name;
        return "";
    }

    [Client]
    public void OnInteractClient(GameObject player) {}

    [Server]
    public void OnInteractServer(GameObject player)
    {
        // only allowed while not hidden. someone might try to send this Cmd
        // on an item that is currently hidden for respawning, in which case
        // we should not allow anything here
        if (proximityChecker.forceHidden)
            return;

        // try to add it to the inventory, destroy drop if it worked
        if (amount > 0 && player.GetComponent<Inventory>().Add(item, amount))
        {
            // does it respawn?
            if (respawn)
            {
                // just hide it, then show it again later to 'respawn'
                // => no separate Respawner objects needed
                // => extremely easy to use. just place in scene and tick 'respawns'
                // => extremely efficient. instead of destroying & spawning, we
                //    simply hide and show it
                // => use Coroutine instead of Invoke to avoid reflection string
                //    search
                Disappear();
            }
            // otherwise just destroy it
            else
            {
                // clear drop's item slot too so it can't be looted again
                // before truly destroyed
                amount = 0;
                NetworkServer.Destroy(gameObject);
            }
        }
    }

    [Server]
    void Disappear()
    {
        // disable colliders while hidden, so we don't see the interaction UI
        // when looking at it
        foreach (Collider co in colliders)
            co.enabled = false;

        // hide
        proximityChecker.forceHidden = true;

        // force observer rebuild IMMEDIATELY so it hides RIGHT NOW instead of
        // next rebuild observers interval
        //
        // IMPORTANT: GridChecker always needs to recalculate current position
        //            in Grid before rebuilding. but ItemDrops never move so we
        //            don't have to worry about it here. in fact, if they would
        //            move then they couldn't be used for respawning. otherwise
        //            they would respawn where ever they were moved, which would
        //            open the doors for exploits where someone might move an
        //            item spawn to their camp etc.
        netIdentity.RebuildObservers(false);

        // reappear in a while
        Invoke(nameof(Reappear), respawnInterval);
    }

    [Server]
    void Reappear()
    {
        // enable colliders again
        foreach (Collider co in colliders)
            co.enabled = true;

        // show again
        proximityChecker.forceHidden = false;
    }
}
