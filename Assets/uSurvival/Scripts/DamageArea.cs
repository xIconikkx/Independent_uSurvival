// put this next to a collider to have multiplied damage, e.g. on the head
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DamageArea : MonoBehaviour
{
    public float multiplier = 1;
}
