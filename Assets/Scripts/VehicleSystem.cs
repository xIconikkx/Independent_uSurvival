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
    [Tooltip("This is the Vehicle Camera that gets enabled and disabled when you enter/exit the vehicle")]
    public GameObject VehicleCamera;

    [Space(5)]
    [Header("Seating Settings")]
    [Tooltip("How many passengers can this vehicle fit?")]
    public int passengerSeating = 3;

    private bool inVehicle;
    [SyncVar] private bool hasDriver;
    [SyncVar] private string driverName;
    [SyncVar] private int passengerCount;

    private string returnString;

    void Start()
    {
        VehicleScriptsEnable(false);

        if (VehicleCamera.activeSelf)
        {
            VehicleCamera.SetActive(false);
        }
    }

    void Update()
    {
        if(inVehicle && Input.GetKeyDown(KeyCode.E))
        {
            uint localPlayerNetID = Player.localPlayer.GetComponent<NetworkIdentity>().netId;

            if (hasDriver && driverName == Player.localPlayer.GetComponent<Player>().PlayerName)
            {
                Player playerScript = Player.localPlayer.GetComponent<Player>();

                //When Call This To People So The Information About The Car Is Up To Date, E.g HasDriver, Drivename etc; 
                CmdExitVehicleDriver(localPlayerNetID);
                // Because We Are Exiting, We Want To Set Our Player GameObject Visible So People Can See Us;
                playerScript.CmdPlayerVisible(true, localPlayerNetID);
                // As The Driver, On Exit We Want To Remove Authority Because We Are Not Driving The Vehicle Anymore;
                playerScript.CmdRemoveAuthority(GetComponent<NetworkIdentity>().netId);
                // As The Driver We Want To Disable The Vehicle Scripts;
                VehicleScriptsEnable(false);
            }
            else
            {
                //When we are the Passenger we cannot call [Commands] On The Vehicle Because We Don't Authority, So We Have To Use The Player.
                Player.localPlayer.GetComponent<Player>().CmdPlayerVisible(true, localPlayerNetID);
            }
            
            // We Just Set OurSelf Active To Make Sure We Can See OurSelves Locally.
            Player.localPlayer.SetActive(true);
            // Set The Players Position To The Exit Point
            Player.localPlayer.transform.position = ExitPoint.position;
            // Set Locally that we are no longer inside a vehicle;
            inVehicle = false;
            // Set The Vehicle VehicleCamera To Off So Its Not Active;
            VehicleCamera.SetActive(false);
        }
    }

    //When we exit the vehicle, we call this to let other players know
    [Command] 
    private void CmdExitVehicleDriver(uint player)
    {
        if(driverName == NetworkIdentity.spawned[player].name)
        {
            inVehicle = false;
            hasDriver = false;
            driverName = null;
        }
    }

    //####################################//
    //# Functions For Entering A Vehicle #//
    //####################################//

    private void EnterVehicleDriver(GameObject player)
    {

        inVehicle = true;
        VehicleCamera.SetActive(true);

        Player.localPlayer.GetComponent<PlayerInteraction>().current = null;
        Player playerScript = Player.localPlayer.GetComponent<Player>();

        playerScript.CmdPlayerVisible(false, Player.localPlayer.GetComponent<NetworkIdentity>().netId);
        playerScript.CmdAssignAuthority(GetComponent<NetworkIdentity>().netId);

        player.SetActive(false);
        VehicleScriptsEnable(true);
    }

    private void EnterVehiclePassenger(GameObject player)
    {
        inVehicle = true;
        VehicleCamera.SetActive(true);
        player.SetActive(false);

        Player.localPlayer.GetComponent<PlayerInteraction>().current = null;
        Player.localPlayer.GetComponent<Player>().CmdPlayerVisible(false, Player.localPlayer.GetComponent<NetworkIdentity>().netId);
    }

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
        else if (hasDriver && passengerCount < passengerSeating)
        {
            EnterVehiclePassenger(player);
        }
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

    //####################################################//
    //# Function Just To Disable/Enable Vehicle Controls #//
    //####################################################//

    //This is called whenever we want to activate/de-activate vehicle control scripts;
    private void VehicleScriptsEnable(bool enable)
    {
        foreach (MonoBehaviour script in VehicleScripts)
        {
            script.enabled = enable;
        }
    }
}
