using System;
using UnityEngine;
using Mirror;

// objects need position offsets for some grid sizes.
// (e.g. if grid = 1 and we want to build a thin palette, it should not be in
//  the center of a unit cube, but on the sides)
[Serializable]
public struct CustomRotation
{
    public Vector3 positionOffset;
    public Vector3 rotation;
}

[CreateAssetMenu(menuName="uSurvival Item/Structure", order=999)]
public class StructureItem : UsableItem
{
    // shown while deciding where to build
    [Header("Structure")]
    public GameObject structurePrefab;

    // show the preview 'n' units away from player.
    // -> for example, a huge building needs to be shown at least size/2 away
    //    from player
    public float previewDistance = 2;

    // resolution 1: 1,2,3,4,...
    // resolution 2: 0.5, 1, 1.5, 2, ...
    // note: modify availableRotations[] position offsets to fit grid if needed
    [Range(1, 10)] public int gridResolution = 1;

    // rotation key switches through available rotations
    public CustomRotation[] availableRotations = { new CustomRotation() };

    // can build checks
    [Range(0, 1)] public float buildToleranceCollision = 0.1f;
    [Range(0, 1)] public float buildToleranceAir = 0.1f;

    // can't use it from inventory. need to aim where to build it
    public override Usability CanUse(PlayerInventory inventory, int inventoryIndex)
    {
        return Usability.Never;
    }
    public override Usability CanUse(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // check base usability first (cooldown etc.)
        Usability baseUsable = base.CanUse(hotbar, hotbarIndex, lookAt);
        if (baseUsable != Usability.Usable)
            return baseUsable;

        // get components
        PlayerConstruction construction = hotbar.GetComponent<PlayerConstruction>();
        PlayerLook look = hotbar.GetComponent<PlayerLook>();

        // calculate look direction in a way that works on clients and server
        // (via lookAt)
        Vector3 lookDirection = (lookAt - look.headPosition).normalized;

        // calculate bounds based on structurePrefab + position + rotation
        // (server doesn't have construction.preview GameObject)
        // THIS POSITION IS DIFFERENT
        Vector3 position = construction.CalculatePreviewPosition(this, look.headPosition, lookDirection);
        Quaternion rotation = construction.CalculatePreviewRotation(this);

        // we need the structure prefab's bounds, but rotated and positioned to
        // where we want to build.
        //
        // this doesn't work yet:
        /*
            Bounds bounds = new Bounds();
            Bounds originalBounds = structurePrefab.GetComponentInChildren<Renderer>().bounds;
            Vector3 p0 = new Vector3(originalBounds.center.x - bounds.size.x,
                                     originalBounds.center.y - bounds.size.y,
                                     originalBounds.center.z - bounds.size.z);

            Vector3 p1 = new Vector3(originalBounds.center.x + bounds.size.x,
                                     originalBounds.center.y - bounds.size.y,
                                     originalBounds.center.z - bounds.size.z);

            Vector3 p2 = new Vector3(originalBounds.center.x - bounds.size.x,
                                     originalBounds.center.y + bounds.size.y,
                                     originalBounds.center.z - bounds.size.z);

            Vector3 p3 = new Vector3(originalBounds.center.x - bounds.size.x,
                                     originalBounds.center.y - bounds.size.y,
                                     originalBounds.center.z + bounds.size.z);

            Vector3 p4 = new Vector3(originalBounds.center.x + bounds.size.x,
                                     originalBounds.center.y + bounds.size.y,
                                     originalBounds.center.z - bounds.size.z);

            Vector3 p5 = new Vector3(originalBounds.center.x + bounds.size.x,
                                     originalBounds.center.y - bounds.size.y,
                                     originalBounds.center.z + bounds.size.z);

            Vector3 p6 = new Vector3(originalBounds.center.x - bounds.size.x,
                                     originalBounds.center.y + bounds.size.y,
                                     originalBounds.center.z + bounds.size.z);

            Vector3 p7 = new Vector3(originalBounds.center.x + bounds.size.x,
                                     originalBounds.center.y + bounds.size.y,
                                     originalBounds.center.z + bounds.size.z);

            bounds.Encapsulate(position + rotation * p0);
            bounds.Encapsulate(position + rotation * p1);
            bounds.Encapsulate(position + rotation * p2);
            bounds.Encapsulate(position + rotation * p3);
            bounds.Encapsulate(position + rotation * p4);
            bounds.Encapsulate(position + rotation * p5);
            bounds.Encapsulate(position + rotation * p6);
            bounds.Encapsulate(position + rotation * p7);

        */
        // so for now, let's set the prefab's position/rotation, get the bounds
        // and then reset it
        Vector3 prefabPosition = structurePrefab.transform.position;
        Quaternion prefabRotation = structurePrefab.transform.rotation;
          structurePrefab.transform.position = position;
          structurePrefab.transform.rotation = rotation;
        Bounds bounds = structurePrefab.GetComponentInChildren<Renderer>().bounds;
          structurePrefab.transform.position = prefabPosition;
          structurePrefab.transform.rotation = prefabRotation;

        return CanBuildThere(look.headPosition, bounds, look.raycastLayers)
               ? Usability.Usable
               : Usability.Empty; // for empty sound. better than 'Never'.
    }

    // [Server] Use logic
    public override void Use(PlayerInventory inventory, int hotbarIndex) {}

    public override void Use(PlayerHotbar hotbar, int hotbarIndex, Vector3 lookAt)
    {
        // call base function to start cooldown
        base.Use(hotbar, hotbarIndex, lookAt);

        // get components
        PlayerConstruction construction = hotbar.GetComponent<PlayerConstruction>();
        PlayerLook look = hotbar.GetComponent<PlayerLook>();

        // get position and rotation from Construction component
        // calculate look direction in a way that works on clients and server
        // (via lookAt)
        Vector3 lookDirection = (lookAt - look.headPosition).normalized;
        Vector3 position = construction.CalculatePreviewPosition(this, look.headPosition, lookDirection);
        Quaternion rotation = construction.CalculatePreviewRotation(this);

        // spawn it into the world
        GameObject go = Instantiate(structurePrefab, position, rotation);
        go.name = structurePrefab.name; // avoid "(Clone)". important for saving..
        NetworkServer.Spawn(go);

        // decrease amount
        ItemSlot slot = hotbar.slots[hotbarIndex];
        slot.DecreaseAmount(1);
        hotbar.slots[hotbarIndex] = slot;
    }

    // can we build the structure at this position with this rotation?
    // -> having CanBuildAt in here instead of in Construction script allows for
    //    custom structures like windows that can only be built between walls
    public virtual bool CanBuildThere(Vector3 headPosition, Bounds bounds, LayerMask raycastLayers)
    {
        // nothing at this position yet?
        // => we check 90% of size so things can at least barely touch each other
        if (Physics.CheckBox(bounds.center, bounds.extents * (1 - buildToleranceCollision), Quaternion.identity, raycastLayers))
            return false;

        // not floating in air?
        // => needs to touch anything else if 110% of collider
        if (!Physics.CheckBox(bounds.center, bounds.extents * (1 + buildToleranceAir), Quaternion.identity, raycastLayers))
            return false;

        // linecast to make sure that nothing is between us and the build preview
        return !Physics.Linecast(headPosition, bounds.center, raycastLayers);
    }

    protected override void OnValidate()
    {
        // call base function
        base.OnValidate();

        // need at least one available rotation
        if (availableRotations.Length == 0)
            availableRotations = new CustomRotation[]{ new CustomRotation() };
    }
}
