// Heartbeat sound
// Make sure to set AudioSource's default volume to 0 so it isn't played before
// the first Update call.
using UnityEngine;
using Mirror;

public class PlayerHeartbeat : NetworkBehaviour
{
    public AudioSource audioSource;
    public Health health;

    void Update()
    {
        audioSource.volume = isLocalPlayer ? (1 - health.Percent()) : 0;
    }
}
