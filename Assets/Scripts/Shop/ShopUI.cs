using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    public GameObject shopMainUI;
    public GameObject defaultPanel;
    public GameObject adminPanel;
    public GameObject shopPanel;

    [Space(5)]
    [Header("Default Panel Information")]
    public GameObject ownerNameTxt;
    public GameObject purchaseBtn;


    [Space(5)]
    [Header("Shop Admin Panel Information")]
    public Text ownerTxt;
    public Text shopNameTxt;
    public Text shopOpenTxt;
    public GameObject shopOpenBtn;

    [Space(2)]
    public Slider priceDifference;

    [Space(5)]
    [Header("Shop Cart Panel")]
    public GameObject spawnPos;

    //Current Shop Interacting With;
    private ShopSystem shopSys;
            
    void Start()
    {
        if (shopMainUI.activeSelf) //We just want to make sure its disabled when we start the game.
        {
            shopMainUI.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if(defaultPanel.activeSelf || adminPanel.activeSelf || shopPanel.activeSelf)
            {
                defaultPanel.SetActive(false);
                adminPanel.SetActive(false);
                shopPanel.SetActive(false);
            }
        }

        if(defaultPanel.activeSelf || adminPanel.activeSelf || shopPanel.activeSelf)
        {
            shopSys.GetComponent<BoxCollider>().enabled = false;
        }
        else
        {
            if(shopSys.GetComponent<BoxCollider>().enabled != true)
            {
                shopSys.GetComponent<BoxCollider>().enabled = true;
            }
        }
    }

    public void OnInteractWithShop(ShopSystem sSys)
    {
        //So we need to recieve all information about this particular shop;
        shopSys = sSys;

        //So does anyone own the shop?
        if (shopSys.shopOwned)
        {
            //If its owned already, are we the owner?
            if(Player.PlayerName == shopSys.ownerName)
            {
                //If so, we reveal the admin panel
                defaultPanel.SetActive(false);
                adminPanel.SetActive(true);
                shopPanel.SetActive(false);

                //Run Any UI Updates If Needed
                UpdateAdminPanelUI();
            }
            else
            {
                //If not, we just show the no access panel
                defaultPanel.SetActive(true);
                adminPanel.SetActive(false);
                shopPanel.SetActive(false);
                //We then need to disable the purchase button so the player can't buy it...
                purchaseBtn.SetActive(false);
                //We then enable the ownername text so the player knows who owns it.
                ownerNameTxt.GetComponent<Text>().text = "CURRENT OWNER:" + " " + shopSys.ownerName;
                ownerNameTxt.SetActive(true);
            }
        }
        else
        {
            //If the shop is not owned, then we need to reveal
            defaultPanel.SetActive(true);
            adminPanel.SetActive(false);
            shopPanel.SetActive(false);
            //the purchase button and the no access text
            purchaseBtn.SetActive(true);
            ownerNameTxt.SetActive(false);
        }

        //We need to update all information
        shopMainUI.SetActive(true);
    }

    public void PurchaseShop()
    {
        bool purchased = shopSys.PlayerBuyShop();

        if (purchased)
        {
            defaultPanel.SetActive(false);
            adminPanel.SetActive(true);
            shopPanel.SetActive(false);
        }
        else
        {
            //Do Fuck All
        }
    }

    public void SellShop()
    {
        bool sold = shopSys.PlayerSellShop();

        if (sold)
        {
            defaultPanel.SetActive(true);
            adminPanel.SetActive(false);
            shopPanel.SetActive(false);

            UpdateDefaultPanelUI();
            UpdateAdminPanelUI();
        }
        else
        {
            //Do Fuck All
        }
    }

    public void ShopOpenStatus()
    {
        bool open = shopSys.PlayerShopOpen();
        if (open)
        {
            shopOpenBtn.GetComponentInChildren<Text>().text = "Close Store";
        }
        else
        {
            shopOpenBtn.GetComponentInChildren<Text>().text = "Open Store";
        }
    }

    private void UpdateDefaultPanelUI()
    {
        if (!shopSys.shopOwned)
        {
            purchaseBtn.SetActive(true);
            ownerNameTxt.SetActive(false);
        }
    }

    public void UpdateAdminPanelUI()
    {
        if (shopSys.shopOpen)
        {
            shopOpenBtn.GetComponentInChildren<Text>().text = "Close Store";
        }
        else
        {
            shopOpenBtn.GetComponentInChildren<Text>().text = "Open Store";
        }
        
    }
}
