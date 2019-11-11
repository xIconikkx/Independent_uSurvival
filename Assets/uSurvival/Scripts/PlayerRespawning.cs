using UnityEngine;
using UnityEngine.Events;
using Mirror;

public class PlayerRespawning : NetworkBehaviour
{
    // Used components. Assign in Inspector. Easier than GetComponent caching.
    public Health health;
    public PlayerMovement movement;

    public float respawnTime = 10;
    [SyncVar, HideInInspector] public double respawnTimeEnd; // syncvar for UI. double for long term precision

    [Header("Events")]
    public UnityEvent onRespawn;

    [ServerCallback]
    void Update()
    {
        if (health.current == 0 && NetworkTime.time >= respawnTimeEnd)
            onRespawn.Invoke();
    }

    [Server]
    public void OnDeath()
    {
        // set respawn end time
        respawnTimeEnd = NetworkTime.time + respawnTime;
    }

    [Server]
    public void OnRespawn()
    {
        // go to start position
        Vector3 start = NetworkManager.singleton.GetStartPosition().position;

        // force position
        movement.ForcePosition(start);

        // revive to closest spawn, with full energies, then go to idle
        foreach (Energy energy in GetComponents<Energy>())
            energy.current = energy.max;

        print(name + " respawned");
    }
}
