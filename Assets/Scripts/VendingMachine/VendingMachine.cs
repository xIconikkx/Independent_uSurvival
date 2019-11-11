using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class VendingMachine : NetworkBehaviour, Interactable
{
    [Tooltip("These are your item drops such as bannans, drinks etc")]
    public GameObject[] DispensableItems;
    public Transform itemSpawn;

    public string GetInteractionText()
    {
        return "Grab a drink";
    }

    [Client]
    public void OnInteractClient(GameObject player)
    {
        //Give Random Item
        
    }

    public void OnInteractServer(GameObject player)
    {
        int randomNum = Random.Range(0, DispensableItems.Length); //Here we get a random number between 0 and the lists length;

        GameObject go = Instantiate(DispensableItems[randomNum], itemSpawn.position, itemSpawn.rotation);
        go.name = DispensableItems[randomNum].GetComponent<ItemDrop>().name; // avoid "(Clone)"
        NetworkServer.Spawn(go);
        //recentSpawn = go.GetComponent<ItemDrop>();
        //NetworkBehaviour.Instantiate(DispensableItems[randomNum], itemSpawn.position, itemSpawn.rotation);
    }
}
