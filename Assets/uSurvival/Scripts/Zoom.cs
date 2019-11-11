using UnityEngine;
using Mirror;

public class Zoom : NetworkBehaviour
{
    // components to be assigned in inspector
    public PlayerHotbar hotbar;

    // cache cameras (main and weapon camera) and default FOW
    Camera[] cameras;
    float defaultFieldOfView;

    void Awake()
    {
        cameras = Camera.main.GetComponentsInChildren<Camera>();
        defaultFieldOfView = cameras[0].fieldOfView;
    }

    void AssignFieldOfView(float value)
    {
        foreach (Camera cam in cameras)
            cam.fieldOfView = value;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // holding down the right mouse button and using a ranged weapon?
        UsableItem itemData = hotbar.GetCurrentUsableItemOrHands();
        if (Input.GetMouseButton(1) && itemData is RangedWeaponItem)
        {
            AssignFieldOfView(defaultFieldOfView - ((RangedWeaponItem)itemData).zoom);
        }
        // otherwise reset field of view
        else AssignFieldOfView(defaultFieldOfView);
    }
}
