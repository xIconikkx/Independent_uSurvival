using UnityEngine;
using Mirror;

public class HouseDoor : NetworkBehaviour, Interactable
{
    // components to be assigned in inspector
    public Animator animator;
    public HousingSystem houseSys;

    [SyncVar] public bool open;

    private string returnString;
    // animate on client AND on server, otherwise the collider stays always
    // closed on the server
    void Update()
    {
        if(animator.GetBool("Open") != open)
        {
            animator.SetBool("Open", open);
        }
    }

    // interactable ////////////////////////////////////////////////////////////
    public string GetInteractionText()
    {
        if (Player.PlayerName == houseSys.ownerName)
        {
            if (open)
            {
                returnString = "Close Door";
            }
            else
            {
                returnString = "Open Door";
            }
        }
        else
        {
            returnString = "Owner Access Only";
        }

        return returnString;
    }

    [Client]
    public void OnInteractClient(GameObject player) { }

    [Server]
    public void OnInteractServer(GameObject player)
    {
        if(player.name == houseSys.ownerName)
        {
            open = !open;
        }        
    }
}
