using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.Events;

public class InteractEvent : NetworkBehaviour, Interactable
{
    public string interactText = "EDITME" ;

    public MonoBehaviour scriptToBeSentOver;

    public UnityEvent OnInteract;

    public string GetInteractionText()
    {
        return interactText;
    }

    public void OnInteractClient(GameObject player)
    {
        OnInteract.Invoke();
    }

    public void OnInteractServer(GameObject player)
    {
        
    }
}
