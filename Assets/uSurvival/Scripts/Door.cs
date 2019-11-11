using UnityEngine;
using Mirror;

public class Door : NetworkBehaviour, Interactable
{
    // components to be assigned in inspector
    public Animator animator;

    [SyncVar] public bool open;

    // animate on client AND on server, otherwise the collider stays always
    // closed on the server
    void Update()
    {
        animator.SetBool("Open", open);
    }

    // interactable ////////////////////////////////////////////////////////////
    public string GetInteractionText()
    {
        return (open ? "Close" : "Open") + " door";
    }

    [Client]
    public void OnInteractClient(GameObject player) {}

    [Server]
    public void OnInteractServer(GameObject player)
    {
        open = !open;
    }
}
