using UnityEngine;
using Mirror;
using UnityEngine.UI;
using System.Collections.Generic;

public class ShopSystem : NetworkBehaviour, Interactable
{
    [Header("Shop UI Reference")]
    public ShopUI shopUI;

    [Header("All Shop Doors")]
    public List<ShopDoor> sDoor = new List<ShopDoor>();

    [Space(10)]
    [SyncVar] public int shopUID;
    [HideInInspector]
    [SyncVar] public bool UIDAssigned;
    [HideInInspector]
    [SyncVar] public bool shopOwned;
    [HideInInspector]
    [SyncVar] public string ownerName;
    [HideInInspector]
    [SyncVar] public string shopName;
    [HideInInspector]
    [SyncVar] public bool shopOpen;
    [HideInInspector]
    [SyncVar] public int shopPriceAdjustInt;

    //#####################################//

    private GameObject currentlyInteractingPlayer;

    [Space(5)]
    [Header("Items For Sale")]
    public ScriptableItem[] ItemsForSale;


    //Server Recieves the Info Back
    

    [ClientRpc]
    public void RpcSetInfo(int houseiD, string shopname, string owner, bool owned)
    {
        shopUID = houseiD;
        shopName = shopname;
        ownerName = owner;
        shopOwned = owned;

        //UpdateUI();
    }


    public string GetInteractionText()
    {
            return "Access Shop Panel";
    }

    [Client]
    public void OnInteractClient(GameObject player) 
    {
        currentlyInteractingPlayer = player;

        shopUI.OnInteractWithShopPanel(this, player);
    }

    [Server]
    public void OnInteractServer(GameObject player)
    {
        currentlyInteractingPlayer = player;
    }

    //###############//
    // NEW FUNCTIONS //
    //###############//

    [Server] //So When The Server Starts, It Will Fetch All The Information For The Current Shop
    public override void OnStartServer() 
    {
        while (!UIDAssigned)
        {
            //Do Nothing Until We Get the UID
        }
        Database.singleton.LoadShop(shopUID, this);

        foreach(ShopDoor door in sDoor)
        {
            door.shopSys = this;
        }
    }

    public void OpenShopPanel()
    {
        shopUI.itemsForSaleUI.Clear();
        foreach (ScriptableItem item in ItemsForSale)
        {
            shopUI.itemsForSaleUI.Add(item);
        }


        shopUI.OpenShopPanel(this);
    }

    [Server]
    public void ShopInfoReturn(int houseiD, string shopname, string owner, bool owned)
    {
        shopUID = houseiD;
        shopName = shopname;
        ownerName = owner;
        shopOwned = owned;
    }


    [Server] //Called By The Purchase Button On The ShopUI Script
    public bool PlayerBuyShop()
    {
        if (!shopOwned)
        {
            ownerName = currentlyInteractingPlayer.name;
            shopOwned = true;

            Database.singleton.SaveShop(shopUID, "SOLD", ownerName, shopOwned);

            return true;
        }
        else
        {
            return false;
        }
        
    }

    [Server] //Called By The Sell Button On The ShopUI Script
    public bool PlayerSellShop()
    {
        if(shopOwned && ownerName == Player.PlayerName)
        {
            ownerName = "";
            shopOwned = false;
            shopName = "FORSALE";
            shopOpen = false;

            Database.singleton.SaveShop(shopUID, shopName, ownerName, shopOwned);

            return true;
        }
        else
        {
            return false;
        }
    }

    [Server]
    public bool PlayerShopOpen()
    {
        shopOpen = !shopOpen;
        //foreach(ShopDoor door in sDoor)
        //{
        //    door.open = shopOpen;
        //}
        return shopOpen;
    }

    [Server]
    public void ShopPriceAdjust(int percent)
    {
        shopPriceAdjustInt = percent;
    }

    [Server]
    public bool PlayerPurchase(GameObject player, int itemCost, int quantity)
    {
        Money mon = player.GetComponent<Money>();

        if (mon.moneyPlayer >= itemCost * quantity)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
