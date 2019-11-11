using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Activate : NetworkBehaviour, Interactable
{
    [Tooltip("This is the thing you want to show/hide, etc")]
    public GameObject activateItem;
    [Tooltip("E.g Shop, or Car, etc, etc. Leave empty for nothing")]
    public string itemName;

    public string GetInteractionText()
    {
        return "USE " + itemName;
    }

    [Client]
    public void OnInteractClient(GameObject player)
    {
        activateItem.SetActive(!activateItem.activeSelf);
        this.GetComponent<BoxCollider>().enabled = false;
    }

    public void OnInteractServer(GameObject player)
    {
        //Dont need to do anything on the server
    }

    public void Exit()
    {
        activateItem.SetActive(false);
        this.GetComponent<BoxCollider>().enabled = true;
    }
}
