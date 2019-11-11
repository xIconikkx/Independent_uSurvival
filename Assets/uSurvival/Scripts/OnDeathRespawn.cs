// a simple script that hides the GameObject on death and shows it again later
using UnityEngine;
using UnityEngine.Events;
using Mirror;

public class OnDeathRespawn : NetworkBehaviour
{
    [Header("Components")]
    public NetworkProximityGridChecker proximityChecker;
#pragma warning disable CS0109 // member does not hide accessible member
    new public Collider collider;
#pragma warning restore CS0109 // member does not hide accessible member

    [Header("Death")]
    public float deathTime = 30; // enough for animation & looting

    [Header("Respawn")]
    public float respawnTime = 10;

    [Header("Events")]
    public UnityEvent onRespawn;
    public UnityEvent onDeathTimeElapsed;

    [Server]
    public void OnDeath()
    {
        // be dead for a while, then disappear
        Invoke(nameof(Disappear), deathTime);
    }

    [Server]
    void Disappear()
    {
        // hide
        proximityChecker.forceHidden = true;

        // disable collider while dead, so we don't block player movement or
        // shots. otherwise the collider is still there while respawning etc.
        collider.enabled = false;

        // call OnDeathTimeElapsed event in case other components need to know
        // about it (like for the tree's rigidbody falling trick)
        onDeathTimeElapsed.Invoke();

        // reappear in a while
        Invoke(nameof(Reappear), respawnTime);
    }

    [Server]
    void Reappear()
    {
        // show again
        proximityChecker.forceHidden = false;

        // enable collider again
        collider.enabled = true;

        // refill all energies
        foreach (Energy energy in GetComponents<Energy>())
            energy.current = energy.max;

        // call OnRespawn event
        onRespawn.Invoke();
    }
}
