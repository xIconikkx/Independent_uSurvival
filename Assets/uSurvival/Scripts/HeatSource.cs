// for fireplaces, ovens, etc. to affect temperature recovery
// -> heat sources should always have a trigger collider for the heat area
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HeatSource : MonoBehaviour
{
    public int recoveryBonus = 100; // 1°C / s
}
