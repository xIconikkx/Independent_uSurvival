using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ERWeapons : MonoBehaviour
{
    public UsableItem[] weaponsCanBeUsed;
    bool theBool;

    public bool CheckIfUsingTool(UsableItem i)
    {
       
        foreach(UsableItem ui in weaponsCanBeUsed)
        {
            if (ui.Equals(i))
            {
                theBool = true;
            }
            else
            {
                theBool = false;
            }
        }
        return theBool;
    }
}
