// make shoulders look at the look direction so up/down aiming works properly.
//
// there are several different ways to make arms look up/down when aiming up/down
// -> using body weight IK is easiest to set up (just enable body weight IK),
//    but then the player bends over forward when looking down, which means he
//    can look through / into walls
// -> making a weaponMount look at the look direction and then glueing the hands
//    onto the weapon parts via IK is an option, but it never properly follows
//    the run animation, it needs different mounts for different states (e.g.
//    laying on the ground mount needs to be in front of head, etc.)
// -> using a weapon-holding pose animation and placing weapon in right hand
//    gives perfect animations. up/down rotations of the arms to follow camera
//    look direction is then done via this script's shoulder rotations
//
// => the third option gives perfect ingame results, but it's sometimes a bit
//    unpleasant to set up if the model's shoulder bones have strange rotations,
//    in which case a rotation offset is needed here.
using System;
using UnityEngine;

[Serializable]
public class ShoulderInfo
{
    public Transform bone;
    [HideInInspector] public Quaternion initialRotation;
    public Vector3 rotationOffset; // some models need an offset
}

public class ShouldersLookAt : MonoBehaviour
{
    [Header("Components")]
    public PlayerHotbar hotbar;
    public PlayerLook look;

    [Header("Shoulders/Arms")]
    public ShoulderInfo leftShoulder;
    public ShoulderInfo rightShoulder;

    void Awake()
    {
        // reset rotation before reading initial bone rotations. otherwise the
        // whole shoulder rotation might be wrong if we spawn into the world
        // and try to get the rotation of an already rotated player, etc.
        Quaternion backup = transform.rotation;
        transform.rotation = Quaternion.identity;

        // remember initial rotations
        // -> .rotation works best for most models, e.g. Space Robot Kyle.
        // -> .localRotation works for less models.
        leftShoulder.initialRotation = leftShoulder.bone.rotation;
        rightShoulder.initialRotation = rightShoulder.bone.rotation;

        // revert rotation
        transform.rotation = backup;
    }

    void AdjustShoulder(ShoulderInfo shoulder)
    {
        // calculate transform.LookAt result manually first:
        Quaternion lookRotation = Quaternion.LookRotation(look.lookPositionFar - shoulder.bone.position);

        // apply it, but factor in the bone's original rotation because it's
        // most likely not rotated forward perfectly
        shoulder.bone.rotation = lookRotation * shoulder.initialRotation * Quaternion.Euler(shoulder.rotationOffset);
    }

    // after animations, IK, etc. using Update would cause race conditions where
    // the weapon twitches
    void LateUpdate()
    {
        // not while free looking
        if (look.IsFreeLooking()) return;

        // make shoulders look at IK target if usable item requires it
        // (usually for ranged weapon aiming)
        // -> on the server too for weapon firing raycasts etc.
        // -> only ranged weapons because melee weapons like the axe might be
        //    carried on shoulder without any aiming being required
        // -> works for hands item too (see GetCurrentUsableItem())
        if (hotbar.GetCurrentUsableItemOrHands().shoulderLookAtWhileHolding)
        {
            AdjustShoulder(leftShoulder);
            AdjustShoulder(rightShoulder);
        }
    }
}
