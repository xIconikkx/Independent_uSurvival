using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class VehicleSystem : NetworkBehaviour, Interactable
{
    [Header("Scripts To Enable/Disable")]
    [Tooltip("These scripts will get enabled when you enter as a driver and disabled when not the driver")]
    public MonoBehaviour[] VehicleScripts;

    [Space(5)]
    [Header("Exit For Player")]
    [Tooltip("When the player wants to leave the vehicle, they will exit at this position")]
    public Transform ExitPoint;

    [Space(5)]
    [Header("Vehicle Camera")]
    [Tooltip("This is the camera that gets enabled and disabled when you enter/exit the vehicle")]
    public GameObject Camera;

    [Header("Temp Info")]
    public bool inVehicle;
    [SyncVar] public bool hasDriver;
    [SyncVar] public string driverName;
    [SyncVar] public int passengerCount;

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
            if (hasDriver && driverName == Player.localPlayer.GetComponent<Player>().PlayerName)
            {
                CmdExitVehicle(Player.localPlayer.GetComponent<NetworkIdentity>().netId);
                Player.localPlayer.GetComponent<SetAuthority>().CmdRemoveAuthority(GetComponent<NetworkIdentity>().netId, Player.localPlayer.GetComponent<NetworkIdentity>().netId);
                VehicleScriptsCall(false);
            }
            else
            {
                Player.localPlayer.GetComponent<Player>().CmdPlayerVisible(true, Player.localPlayer.GetComponent<NetworkIdentity>().netId);
            }
            

            Player.localPlayer.SetActive(true);
            Player.localPlayer.transform.position = ExitPoint.position;

            inVehicle = false;
            Camera.SetActive(false);
            
            
            Debug.Log("You just pressed E To Exit");
        }
    }


    
    //When we exit the vehicle, we call this to let other players know
    [Command] 
    private void CmdExitVehicle(uint player)
    {
        if(driverName == NetworkIdentity.spawned[player].name)
        {
            inVehicle = false;
            hasDriver = false;
            driverName = null;

            Debug.Log("You just exited the vehicle");
        }
    }

    [Server]
    private void Passenger(bool entering, uint player)
    {
        NetworkIdentity.spawned[player].gameObject.SetActive(!entering);

        if (entering)
        {
            passengerCount++;
        }
        else
        {
            passengerCount--;
        }
        
    }

    //This is called whenever we want to activate/de-activate vehicle control scripts;
    private void VehicleScriptsCall(bool enable)
    {
        foreach(MonoBehaviour script in VehicleScripts)
        {
            script.enabled = enable;
        }
    }

    //####################################//
    //# Functions For Entering A Vehicle #//
    //####################################//

    //This returns the correct text when you look at the car
    public string GetInteractionText()
    {
        if (!inVehicle && !hasDriver)
        {
            returnString = "Enter As Driver";
        }
        else if (!inVehicle && hasDriver && passengerCount < passengerSeating)
        {
            returnString = "Enter As Passenger";
        }
        else if (hasDriver && passengerCount >= passengerSeating)
        {
            returnString = "Vehicle Full";
        }

        return returnString;
    }

    //We Call This When We Press 'F' on the vehicle. These are called locally on the client
    public void OnInteractClient(GameObject player)
    {
        if (!hasDriver && !inVehicle)
        {
            EnterVehicleDriver(player);
        }
        else if(hasDriver && passengerCount < passengerSeating)
        {
            EnterVehiclePassenger(player);
        }
    }

    private void EnterVehicleDriver(GameObject player)
    {
        inVehicle = true;
        Camera.SetActive(true);
        player.SetActive(false);

        Player.localPlayer.GetComponent<PlayerInteraction>().current = null;
        Player.localPlayer.GetComponent<SetAuthority>().CmdAssignAuthority(GetComponent<NetworkIdentity>().netId, Player.localPlayer.GetComponent<NetworkIdentity>().netId);

        VehicleScriptsCall(true);
    }

    private void EnterVehiclePassenger(GameObject player)
    {
        inVehicle = true;
        Camera.SetActive(true);
        player.SetActive(false);

        Player.localPlayer.GetComponent<PlayerInteraction>().current = null;
        Player.localPlayer.GetComponent<Player>().CmdPlayerVisible(false, Player.localPlayer.GetComponent<NetworkIdentity>().netId);
    }

    //We use this to change values on the server so other people know when we have entered vehicle as driver/passenger etc
    public void OnInteractServer(GameObject player)
    {
        if (!hasDriver)
        {
            hasDriver = true;
            driverName = player.name;
        }
    }
}
