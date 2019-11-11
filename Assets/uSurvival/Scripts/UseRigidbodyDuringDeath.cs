// there is an old game development trick where a Rigidbody is only enabled as
// soon as the object died. this can be used for trees that aren't supposed to
// fall until they actually die.
using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkTransform))] // for position sync
public class UseRigidbodyDuringDeath : NetworkBehaviour
{
    public Rigidbody rigidBody;
    public float applyForce = 1000;

    // remember original position and rotation
    public bool resetPositionWhenRespawning = true;
    Vector3 startPosition;
    Quaternion startRotation;
    bool dirty;

    // needs to be known on client and server
    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    // on death events /////////////////////////////////////////////////////////
    [Server]
    public void OnDeath()
    {
        // fall on server and client (via rpc)
        StartFall();
        RpcStartFall();
    }

    [Server]
    public void OnDeathTimeElapsed()
    {
        StopFall();
        RpcStopFall();
    }

    [Server]
    public void OnRespawn()
    {
        // reset position after fall
        if (resetPositionWhenRespawning)
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }
    }

    // falling /////////////////////////////////////////////////////////////////
    void StartFall()
    {
        rigidBody.isKinematic = false;

        // the tree won't fall if it stands perfectly straight, so let's add a
        // small force to make it fall
        rigidBody.AddForce(transform.forward * applyForce);
    }

    void StopFall()
    {
        rigidBody.isKinematic = true;
    }

    // rpcs ////////////////////////////////////////////////////////////////////
    // syncing a falling tree to the client would cost a lot of bandwidth if we
    // use NetworkTransform for thousands of trees. it's better to use one Rpc
    // and hope that the results will be roughly the same on client and server
    [ClientRpc]
    void RpcStartFall()
    {
        if (isServer) return; // don't call it in host mode again
        StartFall();
    }

    [ClientRpc]
    void RpcStopFall()
    {
        if (isServer) return; // don't call it in host mode again
        StopFall();
    }
}
