using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Money : MonoBehaviour
{
    public int moneyBank;
    public int  moneyPlayer;
    public ScriptableItem Cash;
    private Item CashItem;

    public PlayerInventory pInvo;

    private void Start()
    {
        pInvo = GetComponent<PlayerInventory>();
        CashItem = new Item(Cash);
        UpdateMoney();
    }

    public void UpdateMoney()
    {
        moneyPlayer = pInvo.Count(CashItem);
    }

    public int FetchMoney()
    {
        return moneyPlayer;
    }
}
