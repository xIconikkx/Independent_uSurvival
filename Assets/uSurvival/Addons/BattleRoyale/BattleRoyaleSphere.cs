using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class BattleRoyaleSphere : NetworkBehaviour
{
#pragma warning disable CS0109 // member does not hide accessible member
    new public SphereCollider collider;
#pragma warning restore CS0109 // member does not hide accessible member

    public float shrinkSpeed = 0.01f;
    public int damagePerTick = 100;

    public override void OnStartServer()
    {
        // start damage tick
        InvokeRepeating(nameof(DamageTick), 1, 1);
    }

    [ServerCallback]
    void Update()
    {
        // shrink
        transform.localScale *= (1 - shrinkSpeed * Time.deltaTime);
    }

    // unity uses the biggest x/y/z component as world space radius
    static float CalculateWorldSpaceRadius(float radius, Vector3 scale)
    {
        return radius * Mathf.Max(scale.x, scale.y, scale.z);
    }

    [Server]
    void DamageTick()
    {
        // collider.radius is local. convert it to world space first (* scale)
        float worldSpaceRadius = CalculateWorldSpaceRadius(collider.radius, transform.lossyScale);

        // deal damage to all players outside of the sphere
        // (calculating distance to center is the easiest solution)
        foreach (GameObject player in Player.onlinePlayers.Values)
            if (Vector3.Distance(player.transform.position, transform.position) > worldSpaceRadius)
                player.GetComponent<Health>().current -= damagePerTick;
    }
}
