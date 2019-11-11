using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Structure : NetworkBehaviour
{
    // cache all structures on the server to save lots of computations
    // (otherwise we'd have to iterate NetworkServer.objects all the time)
    // -> differentiate them by name. make sure to not use the same name for
    //    two storages.
    //    (sceneId is not a good alternative because it changes when changing
    //     the hierarchy)
    public static HashSet<Structure> structures = new HashSet<Structure>();

    public override void OnStartServer()
    {
        structures.Add(this);
    }

    void OnDestroy()
    {
        structures.Remove(this);
    }
}
