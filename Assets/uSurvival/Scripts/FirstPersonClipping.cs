// hide model parts in first person by modifying the layer
// => this is better than using a transparent texture because this way we can
//    still display those parts in the equipment avatar
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class FirstPersonClipping : NetworkBehaviour
{
    [Header("Components")]
    public PlayerLook look;
    public PlayerEquipment equipment;

    [Header("Mesh Hiding")]
    public Transform[] hideRenderers; // transform because equipment slots won't have renderers initially
    Dictionary<Renderer, int> layerBackups = new Dictionary<Renderer, int>();
    public string hideInFirstPersonLayer = "HideInFirstPerson"; // hide this one in the main Camera

    [Header("Disable Depth Check (to avoid clipping)")]
    public string noDepthLayer = "NoDepthInFirstPerson";
    public Renderer[] disableArmsDepthCheck;
    Camera weaponCamera;

    public override void OnStartLocalPlayer()
    {
        // find weapon camera
        foreach (Transform t in Camera.main.transform)
            if (t.tag == "WeaponCamera")
                weaponCamera = t.GetComponent<Camera>();
    }

    void HideMeshes(bool firstPerson)
    {
        // convert name to layer only once
        int hiddenLayer = LayerMask.NameToLayer(hideInFirstPersonLayer);

        // hide body etc. if needed, so that we don't see ourself when looking
        // downwards
        // -> we have to do it in Update because proximity checker may overwrite
        //    it
        // -> we don't just destroy it, because that won't work for the textmesh
        // -> do it continously to overwrite proximitychecker changes
        // -> disabling renderer causes ik to stop working, so we need to
        // swap out the material with something transparent instead.
        foreach (Transform tf in hideRenderers)
        {
            foreach (Renderer rend in tf.GetComponentsInChildren<Renderer>())
            {
                // not hidden yet? then backup the layer
                if (rend.gameObject.layer != hiddenLayer)
                    layerBackups[rend] = rend.gameObject.layer;

                // hide
                rend.gameObject.layer = firstPerson ? hiddenLayer : layerBackups[rend];
            }
        }
    }

    void DisableDepthCheck(bool firstPerson)
    {
        // enable weapon camera only in first person
        // (to draw arms and weapon without depth check to avoid clipping
        //  through walls)
        if (weaponCamera != null)
            weaponCamera.enabled = firstPerson;

        // convert name to layer only once
        int noDepth = LayerMask.NameToLayer(noDepthLayer);

        // set weapon layer to NoDepth (only for localplayer so we don't see
        // others without depth checks)
        // -> do for arms etc.
        foreach (Renderer renderer in disableArmsDepthCheck)
            renderer.gameObject.layer = noDepth;

        // -> do for weapon
        foreach (Renderer renderer in equipment.weaponMount.GetComponentsInChildren<Renderer>())
            renderer.gameObject.layer = noDepth;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // only hide while in first person mode
        bool firstPerson = look.InFirstPerson();
        HideMeshes(firstPerson);
        DisableDepthCheck(firstPerson);
    }
}
