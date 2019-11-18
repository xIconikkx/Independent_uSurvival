using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class VehicleSystem : NetworkBehaviour, Interactable
{
    public MonoBehaviour[] VehicleScripts;
    [Space(2)]
    public Transform ExitPoint;
    [Space(2)]
    public GameObject Camera;

    [Header("Temp Info")]
    public bool inVehicle;
    [SyncVar] public bool hasDriver;
    [SyncVar] public string driverName;
    [SyncVar] public int passengerCount;

    [Space(5)]
    public bool HasAuthority;

    private string returnString;
    public int passengerSeating = 3;

    void Start()
    {
        Camera.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if(inVehicle && Input.GetKeyDown(KeyCode.E))
        {
            Player.localPlayer.GetComponent<SetAuthority>().CmdRemoveAuthority(GetComponent<NetworkIdentity>().netId, Player.localPlayer.GetComponent<NetworkIdentity>().netId);
            Player.localPlayer.transform.position = ExitPoint.position;
            //We Want The Player To Be Shown

            ExitVehicle(Player.localPlayer);

            inVehicle = false;
            Camera.SetActive(false);
            
            VehicleScriptsCall();
            Debug.Log("You just pressed E To Exit");
        }
    }


    public string GetInteractionText()
    {
        if (!inVehicle && !hasDriver)
        {
            returnString = "Enter Vehicle";
        }
        else if(!inVehicle && hasDriver)
        {
            returnString = "Full Vehicle";
        }

        return returnString;
    }

    public void OnInteractClient(GameObject player)
    {
        if (!hasDriver && !inVehicle)
        {
            inVehicle = true;
            player.SetActive(false);
            Camera.SetActive(true);

            Player.localPlayer.GetComponent<SetAuthority>().CmdAssignAuthority(GetComponent<NetworkIdentity>().netId,Player.localPlayer.GetComponent<NetworkIdentity>().netId);

            VehicleScriptsCall();
        }
    }

    public void OnInteractServer(GameObject player)
    {
        if (!hasDriver)
        {
            hasDriver = true;
            driverName = player.name;
        }
    }

    [Server]
    private void ExitVehicle(GameObject player)
    {
        if(driverName == player.name)
        {
            inVehicle = false;
            hasDriver = false;
            driverName = null;

            Debug.Log("You just exited the vehicle");
        }
    }

    private void VehicleScriptsCall()
    {
        foreach(MonoBehaviour script in VehicleScripts)
        {
            script.enabled =! script.isActiveAndEnabled;
        }
    }
}
