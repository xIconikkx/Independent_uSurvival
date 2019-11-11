using UnityEngine;
using Mirror;

public class ShopDoor : NetworkBehaviour, Interactable
{
    // components to be assigned in inspector
    public Animator animator;
    [HideInInspector]
    public ShopSystem shopSys;

    [SyncVar] public bool open;

    private string returnString;
    // animate on client AND on server, otherwise the collider stays always
    // closed on the server
    void Update()
    {
        if (animator.GetBool("Open") != open)
        {
            animator.SetBool("Open", open);
        }
    }

    // interactable ////////////////////////////////////////////////////////////
    public string GetInteractionText()
    {
        if (shopSys.shopOpen)
        {
            if (!open)
            {
                returnString = "Open Door";
            }
            else
            {
                returnString = "Close Door";
            }
        }
        else
        {
            returnString = "Shop Is Currently Closed";
        }

        return returnString;
    }

    [Client]
    public void OnInteractClient(GameObject player) { }

    [Server]
    public void OnInteractServer(GameObject player)
    {
        if (!shopSys.shopOpen)
        {
            //Do nothing if the shop is not open/locked
        }
        else
        {
            open = !open;
        }
    }
}
