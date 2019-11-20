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


        RpcPlayerVisible(player, false);

        Debug.Log("Assigned Authority");
    }

    [ClientRpc]
    public void RpcPlayerVisible(uint player, bool i)
    {
        GameObject p = NetworkIdentity.spawned[player].gameObject;
        p.SetActive(i);
    }


    [Command]
    public void CmdRemoveAuthority(uint vehicle, uint player)
    {
        RpcPlayerVisible(player, true);

        GameObject theVeh = NetworkIdentity.spawned[vehicle].gameObject;
        theVeh.GetComponent<NetworkIdentity>().RemoveClientAuthority();

        Debug.Log("Removed Authority");
    }

    
}
