using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

public class BuildingIDSystem : NetworkBehaviour
{
    [SerializeField]
    private Database db;

    [Header("Housing Area")]
    public HousingSystem[] worldHouses;
    private int HouseUID;

    [Header("Shop System")]
    public ShopSystem[] worldShops;
    private int ShopUID;

    private void Start()
    {
        foreach (HousingSystem i in worldHouses)
        {
            HouseUID++;
            i.houseUID = HouseUID;
            i.db = db;
            i.UIDAssigned = true;
        }

        foreach (ShopSystem i in worldShops)
        {
            ShopUID++;
            i.shopUID = ShopUID;
            //i.db = db;
            i.UIDAssigned = true;
        }
    }
}
