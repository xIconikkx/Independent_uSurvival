// Contains all the network messages that we need.
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// client to server ////////////////////////////////////////////////////////////
public class LoginMsg : MessageBase
{
    public string account;
    public string password;
    public string version;
}

public class CharacterCreateMsg : MessageBase
{
    public string name;
    public int classIndex;
}

public partial class CharacterSelectMsg : IntegerMessage {}

public partial class CharacterDeleteMsg : IntegerMessage {}

// server to client ////////////////////////////////////////////////////////////
// we need an error msg packet because we can't use TargetRpc with the Network-
// Manager, since it's not a MonoBehaviour.
public class ErrorMsg : MessageBase
{
    public string text;
    public bool causesDisconnect;
}

public partial class LoginSuccessMsg : MessageBase
{
}

public class CharactersAvailableMsg : MessageBase
{
    public struct CharacterPreview
    {
        public string name;
        public string className; // = the prefab name
    }
    public CharacterPreview[] characters;

    // load method in this class so we can still modify the characters structs
    // in the addon hooks
    public void Load(List<GameObject> players)
    {
        // we only need name and class for our UI
        // (avoid Linq because it is HEAVY(!) on GC and performance)
        characters = new CharacterPreview[players.Count];
        for (int i = 0; i < players.Count; ++i)
        {
            GameObject player = players[i];
            characters[i] = new CharacterPreview
            {
                name = player.name,
                className = player.GetComponent<Player>().className
            };
        }
    }
}