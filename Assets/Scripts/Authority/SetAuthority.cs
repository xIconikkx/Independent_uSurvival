using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class SetAuthority : NetworkBehaviour
{
    [Command]
    public void CmdAssignAuthority(uint vehicle,uint player)
    {
        GameObject theVeh = NetworkIdentity.spawned[vehicle].gameObject;
        theVeh.GetComponent<NetworkIdentity>().AssignClientAuthority(connectionToClient);

        GameObject p = NetworkIdentity.spawned[player].gameObject;
        p.SetActive(false);


        Debug.Log("Assigned Authority");
    }
    [Command]
    public void CmdRemoveAuthority(uint vehicle, uint player)
    {
        GameObject p = NetworkIdentity.spawned[player].gameObject;
        p.SetActive(true);

        GameObject theVeh = NetworkIdentity.spawned[vehicle].gameObject;
        theVeh.GetComponent<NetworkIdentity>().RemoveClientAuthority();

        Debug.Log("Removed Authority");
    }

    
}
