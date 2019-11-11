// can be added to weapons to define more details like muzzle location, etc.
using UnityEngine;

public class WeaponDetails : MonoBehaviour
{
    [Header("Muzzle")]
    public MuzzleFlash muzzleFlash;
    public Transform muzzleLocation;
}
