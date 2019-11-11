// Catches the Aggro Sphere's OnTrigger functions and forwards them to the
// owner. Make sure that the aggro area's layer is IgnoreRaycast, so that
// clicking on the area won't select the entity.
//
// Note that a player's collider might be on the pelvis for animation reasons,
// so we need to use GetComponentInParent to find the owner script.
using UnityEngine;

[RequireComponent(typeof(Collider))] // aggro area trigger
public class AggroArea : MonoBehaviour
{
    public Monster owner; // set in the inspector

    // same as OnTriggerStay
    void OnTriggerEnter(Collider co)
    {
        // is this a living thing that we could attack?
        // (look in parents because AggroArea doesn't collide with player's main
        //  layer (IgnoreRaycast), only with body part layers. this way
        //  AggroArea only interacts with player layers, not with other
        //  monster's IgnoreRaycast layers etc.)
        Health health = co.GetComponentInParent<Health>();
        if (health) owner.OnAggro(health.gameObject);
    }

    void OnTriggerStay(Collider co)
    {
        // is this a living thing that we could attack?
        // (look in parents because AggroArea doesn't collide with player's main
        //  layer (IgnoreRaycast), only with body part layers. this way
        //  AggroArea only interacts with player layers, not with other
        //  monster's IgnoreRaycast layers etc.)
        Health health = co.GetComponentInParent<Health>();
        if (health) owner.OnAggro(health.gameObject);
    }
}
