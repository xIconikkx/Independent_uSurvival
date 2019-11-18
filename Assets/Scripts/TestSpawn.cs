using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class TestSpawn : NetworkBehaviour,Interactable
{
    public GameObject objToSpawn;
    [Space(5)]
    public Transform spawnpos;

    public string GetInteractionText()
    {
        return "Spawn " + objToSpawn.name;
    }

    public void OnInteractClient(GameObject player)
    {
        
    }

    public void OnInteractServer(GameObject player)
    {
        var go = Instantiate(objToSpawn, spawnpos.position, new Quaternion(0, 0, 0, 0), null);
        NetworkServer.Spawn(go);
    }
}
