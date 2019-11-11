using UnityEngine;
using Mirror;
using UnityEngine.UI;

public class HousingSystem : NetworkBehaviour, Interactable
{
    public int houseUID;
    [HideInInspector]
    public bool UIDAssigned;
    public bool houseOwned;
    public string ownerName;
    public GameObject playerObject;

    public Database db;

    [Header("UI")]
    public Text HouseID;
    public Text ForSale;
    public Text OwnerName;
    //public Text SaleButton;

    public float saveInterval = 5f;
    private float timer;


    private void LateUpdate()
    {
        if(Player.localPlayer != null)
        {
            if (Vector3.Distance(Player.localPlayer.transform.position, this.transform.position) <= 10 )
            {
                UpdateUI();
            }
        }
    }

    //Server Recieves the Info Back
    public void HouseInfoReturn(int houseiD, string owner, bool owned)
    {
        houseUID = houseiD;
        ownerName = owner;
        houseOwned = owned;

        UpdateUI();

        if (isServer)
        {
            RpcSetInfo(houseiD, owner,owned);
        }
    }

    [ClientRpc]
    public void RpcSetInfo(int houseiD, string owner, bool owned)
    {
        houseUID = houseiD;
        ownerName = owner;
        houseOwned = owned;

        UpdateUI();
    }

    private void UpdateUI()
    {
        HouseID.text = "HouseID: " + houseUID;
        ForSale.text = "For Sale: " + !houseOwned;
        OwnerName.text = "Owner: " + ownerName;
    }   


    public string GetInteractionText()
    {
        if (houseOwned)
        {
            return "Sell House";
        }
        else
        {
            return "Purchase House";
        }
    }

    [Client]
    public void OnInteractClient(GameObject player) { playerObject = player; }

    [Server]
    public void OnInteractServer(GameObject player)
    {
        playerObject = player;
        if (!houseOwned)
        {
            ownerName = player.name;
            houseOwned = true;
            
            db.SaveHouse(houseUID, ownerName, houseOwned);
        }
        else if (player.name == ownerName)
        {
            ownerName = "";
            houseOwned = false;

            db.SaveHouse(houseUID, "", false);
        }

        RpcOnInteract(ownerName, houseOwned);
        UpdateUI();
    }

    [ClientRpc]
    public void RpcOnInteract(string owner, bool owned)
    {
        ownerName = owner;
        houseOwned = owned;

        UpdateUI();
    }

    
}
