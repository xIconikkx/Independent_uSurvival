using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.Events;

public class InteractEvent : NetworkBehaviour, Interactable
{
    public string interactText = "EDITME" ;

    public UnityEvent OnInteract;

    public UnityEvent ShopOwnerName;

    public string GetInteractionText()
    {
        return interactText;
    }

    public void OnInteractClient(GameObject player)
    {
        //Do Nothing for now
    }

    public void OnInteractServer(GameObject player)
    {
        OnInteract.Invoke();
    }
}
