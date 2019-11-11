// this component handles the construction preview while a structure item is
// selected on the hotbar
using UnityEngine;
using Mirror;

public class PlayerConstruction : NetworkBehaviour
{
    [Header("Components")]
    public PlayerHotbar hotbar;
    public PlayerLook look;

    [Header("Configuration")]
    public KeyCode rotationKey = KeyCode.R;
    public Color canBuildColor = Color.cyan;
    public Color cantBuildColor = Color.red;

    // current preview (on client)
    // -> no need to sync it to server. server can estimate it via:
    //    - position from look.raycast direction
    //    - rotation from rotationIndex
    //    - bounds from structure.previewPrefab
    [HideInInspector] public GameObject preview;

    // rotation index needs to be known on the server to decide final build
    // position / rotation
    [SyncVar] int rotationIndex;

    // helper function to get the current structure in hands (if any)
    public StructureItem GetCurrentStructure()
    {
        UsableItem itemData = hotbar.GetCurrentUsableItemOrHands();
        return itemData is StructureItem ? (StructureItem)itemData : null;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // selected a structure in hotbar right now?
        StructureItem structure = GetCurrentStructure();
        if (structure != null)
        {
            // destroy preview if not matching anymore
            if (preview != null && preview.name != structure.structurePrefab.name)
                Destroy(preview);

            // load preview if not loaded yet
            if (preview == null || preview.name != structure.structurePrefab.name)
            {
                // instantiate
                preview = Instantiate(structure.structurePrefab,
                                      CalculatePreviewPosition(structure, look.headPosition, look.lookDirectionRaycasted),
                                      CalculatePreviewRotation(structure));

                // avoid "(Clone)"
                preview.name = structure.structurePrefab.name;

                // remove all script logic. it's only used as a preview.
                // (except NetworkIdentity, which would throw an error)
                foreach (Behaviour behaviour in preview.GetComponentsInChildren<Behaviour>())
                    if (!(behaviour is NetworkIdentity))
                        Destroy(behaviour);

                // remove all colliders. it's only used as a preview.
                foreach (Collider co in preview.GetComponentsInChildren<Collider>())
                    Destroy(co);
            }

            // set position in front of player
            preview.transform.position = CalculatePreviewPosition(structure, look.headPosition, look.lookDirectionRaycasted);

            // set rotation
            preview.transform.rotation = CalculatePreviewRotation(structure);

            // rotate if R key pressed
            if (Input.GetKeyDown(rotationKey))
            {
                int newIndex = (rotationIndex + 1) % structure.availableRotations.Length;
                CmdSetRotationIndex(newIndex);
            }

            // set color depending on if we can build there or not
            // (use sharedMaterial for prefabs and material for runtime)
            Bounds bounds = preview.GetComponentInChildren<Renderer>().bounds;
            bool canBuild = structure.CanBuildThere(look.headPosition, bounds, look.raycastLayers);
            foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>())
                renderer.material.color = canBuild ? canBuildColor : cantBuildColor;
        }
        // no more structure selected. destroy preview (if any)
        else if (preview != null) Destroy(preview);
    }

    [Command]
    void CmdSetRotationIndex(int index)
    {
        rotationIndex = index;
    }

    static float RoundToGrid(float value, int resolution)
    {
        // if we want to round to 0, 0.5, 1, 1.5 etc. then:
        //   multiply by 2, round, divide by 2
        return Mathf.Round(value * resolution) / resolution;
    }

    public Vector3 CalculatePreviewPosition(StructureItem structure, Vector3 headPosition, Vector3 lookDirection)
    {
        // calulate position in front of player, depending on where he looks
        Vector3 inFront = headPosition + lookDirection * structure.previewDistance;

        // snap to grid so we have some kind of order when building
        inFront.x = RoundToGrid(inFront.x, structure.gridResolution);
        inFront.y = RoundToGrid(inFront.y, structure.gridResolution);
        inFront.z = RoundToGrid(inFront.z, structure.gridResolution);

        // make sure rotationIndex is still in range. might have switched to
        // another item with fewer available rotations.
        rotationIndex = rotationIndex % structure.availableRotations.Length;

        // apply the offset
        Vector3 offset = structure.availableRotations[rotationIndex].positionOffset;
        return inFront + offset;
    }

    public Quaternion CalculatePreviewRotation(StructureItem structure)
    {
        // make sure rotationIndex is still in range. might have switched to
        // another item with fewer available rotations.
        rotationIndex = rotationIndex % structure.availableRotations.Length;

        // get euler rotation
        Vector3 euler = structure.availableRotations[rotationIndex].rotation;

        // convert to quaternion
        return Quaternion.Euler(euler);
    }

    void OnDrawGizmos()
    {
        // show the renderer.bounds check that is used to check StructureItem's
        // CanBuildAt check
        if (preview != null)
        {
            StructureItem structure = GetCurrentStructure();
            Bounds bounds = preview.GetComponentInChildren<Renderer>().bounds;

            // regular bounds
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // bounds - build tolerance (for collision check)
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(bounds.center, bounds.size * (1 - structure.buildToleranceCollision));

            // bounds + build tolerance (for air check)
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(bounds.center, bounds.size * (1 + structure.buildToleranceAir));
        }
    }
}
