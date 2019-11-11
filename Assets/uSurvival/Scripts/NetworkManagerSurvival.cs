// We use a custom NetworkManager that also takes care of login, character
// selection, character creation and more.
//
// We don't use the playerPrefab, instead all available player classes should be
// dragged into the spawnable objects property.
//
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Mirror;

// we need a clearly defined state to know if we are offline/in world/in lobby
// otherwise UICharacterSelection etc. never know 100% if they should be visible
// or not.
public enum NetworkState {Offline, Handshake, Lobby, World}

[RequireComponent(typeof(Database))]
public class NetworkManagerSurvival : NetworkManager
{
    // current network manager state on client
    public NetworkState state = NetworkState.Offline;

    // <conn, account> dict for the lobby
    // (people that are still creating or selecting characters)
    public Dictionary<NetworkConnection, string> lobby = new Dictionary<NetworkConnection, string>();

    // UI components to avoid FindObjectOfType
    [Header("UI")]
    public UIPopup uiPopup;

    // we may want to add another game server if the first one gets too crowded.
    // the server list allows people to choose a server.
    //
    // note: we use one port for all servers, so that a headless server knows
    // which port to bind to. otherwise it would have to know which one to
    // choose from the list, which is far too complicated. one port for all
    // servers will do just fine for an Indie game.
    [Serializable]
    public class ServerInfo
    {
        public string name;
        public string ip;
    }
    public List<ServerInfo> serverList = new List<ServerInfo>()
    {
        new ServerInfo{name="Local", ip="localhost"}
    };

    [Header("Logout")]
    [Tooltip("Players shouldn't be able to log out instantly to flee combat. There should be a delay.")]
    public float combatLogoutDelay = 5;

    [Header("Database")]
    public int characterLimit = 4;
    public int characterNameMaxLength = 16;
    public float saveInterval = 60f; // in seconds

    [Header("Debug")]
    public bool showDebugGUI = true;

    // cache player classes in Awake
    [HideInInspector] public List<GameObject> playerClasses = new List<GameObject>();

    // store characters available message on client so that UI can access it
    [HideInInspector] public CharactersAvailableMsg charactersAvailableMsg;

    // name checks /////////////////////////////////////////////////////////////
    public bool IsAllowedCharacterName(string characterName)
    {
        // not too long?
        // only contains letters, number and underscore and not empty (+)?
        // (important for database safety etc.)
        return characterName.Length <= characterNameMaxLength &&
               Regex.IsMatch(characterName, @"^[a-zA-Z0-9_]+$");
    }

    // player classes //////////////////////////////////////////////////////////
    // find all available player classes
    public List<GameObject> FindPlayerClasses()
    {
        // search manually. Linq is HEAVY(!) on GC and performance
        List<GameObject> classes = new List<GameObject>();
        foreach (GameObject go in spawnPrefabs)
            if (go.GetComponent<Player>() != null)
                classes.Add(go);
        return classes;
    }

    // events //////////////////////////////////////////////////////////////////
    public override void Awake()
    {
        base.Awake();

        // cache list of player classes from spawn prefabs.
        // => we assume that this won't be changed at runtime (why would it?)
        // => this is way better than looping all prefabs in character
        //    select/create/delete each time!
        playerClasses = FindPlayerClasses();
    }

    void Update()
    {
        // any valid local player? then set state to world
        if (ClientScene.localPlayer != null)
            state = NetworkState.World;
    }

    // error messages //////////////////////////////////////////////////////////
    public void ServerSendError(NetworkConnection conn, string error, bool disconnect)
    {
        conn.Send(new ErrorMsg{text=error, causesDisconnect=disconnect});
    }

    void OnClientError(NetworkConnection conn, ErrorMsg message)
    {
        print("OnClientError: " + message.text);

        // show a popup
        uiPopup.Show(message.text);

        // disconnect if it was an important network error
        // (this is needed because the login failure message doesn't disconnect
        //  the client immediately (only after timeout))
        if (message.causesDisconnect)
        {
            conn.Disconnect();

            // also stop the host if running as host
            // (host shouldn't start server but disconnect client for invalid
            //  login, which would be pointless)
            if (NetworkServer.active) StopHost();
        }
    }

    // start & stop ////////////////////////////////////////////////////////////
    public override void OnStartClient()
    {
        // setup handlers
        NetworkClient.RegisterHandler<ErrorMsg>(OnClientError, false); // allowed before auth!
        NetworkClient.RegisterHandler<CharactersAvailableMsg>(OnClientCharactersAvailable);
    }

    public override void OnStartServer()
    {
        // connect to database
        Database.singleton.Connect();

        // handshake packet handlers
        NetworkServer.RegisterHandler<CharacterCreateMsg>(OnServerCharacterCreate);
        NetworkServer.RegisterHandler<CharacterSelectMsg>(OnServerCharacterSelect);
        NetworkServer.RegisterHandler<CharacterDeleteMsg>(OnServerCharacterDelete);

        // load all player generated structures
        // (after base.OnStartServer so that we can call NetworkServer.Spawn)
        Database.singleton.LoadStructures();

        // invoke saving
        InvokeRepeating(nameof(Save), saveInterval, saveInterval);
    }

    public override void OnStopServer()
    {
        print("OnStopServer");
        CancelInvoke(nameof(Save));
    }

    // handshake: login ////////////////////////////////////////////////////////
    public bool IsConnecting() => NetworkClient.active && !ClientScene.ready;

    // called on the client if a client connects after successful auth
    public override void OnClientConnect(NetworkConnection conn)
    {
        // do NOT call base function, otherwise client becomes "ready".
        // => it should only be "ready" after selecting a character. otherwise
        //    it might receive world messages from monsters etc. already
        //base.OnClientConnect(conn);
    }

    // called on the server if a client connects after successful auth
    public override void OnServerConnect(NetworkConnection conn)
    {
        // grab the account from the lobby
        string account = lobby[conn];

        // send necessary data to client
        conn.Send(MakeCharactersAvailableMessage(account));
    }

    // the default OnClientSceneChanged sets the client as ready automatically,
    // which makes no sense for our situation. this was more for situations
    // where the server tells all clients to load a new scene.
    // -> setting client as ready will cause 'already set as ready' errors if
    //    we call StartClient before loading a new scene (e.g. for zones)
    // -> it's best to just overwrite this with an empty function
    public override void OnClientSceneChanged(NetworkConnection conn) {}

    // helper function to make a CharactersAvailableMsg from all characters in
    // an account
    CharactersAvailableMsg MakeCharactersAvailableMessage(string account)
    {
        // load names from database
        List<string> names = Database.singleton.CharactersForAccount(account);

        // load characters
        // avoid Linq, because it is HEAVY(!) on GC and performance
        List<GameObject> characters = new List<GameObject>();
        foreach (string character in names)
            characters.Add(Database.singleton.CharacterLoad(character, playerClasses, true));

        // construct the message
        CharactersAvailableMsg message = new CharactersAvailableMsg();
        message.Load(characters);

        // destroy the temporary players again and return the result
        characters.ForEach(player => Destroy(player.gameObject));
        return message;
    }

    // handshake: character selection //////////////////////////////////////////
    void OnClientCharactersAvailable(NetworkConnection conn, CharactersAvailableMsg message)
    {
        charactersAvailableMsg = message;
        print("characters available:" + charactersAvailableMsg.characters.Length);

        // set state
        state = NetworkState.Lobby;
    }

    // handshake: character creation ///////////////////////////////////////////

    GameObject CreateCharacter(GameObject classPrefab, string characterName, string account)
    {
        // create new character based on the prefab.
        // -> we also assign default items and equipment for new characters
        // (instantiate temporary player)
        //print("creating character: " + message.name + " " + message.classIndex);
        GameObject player = Instantiate(classPrefab);
        player.name = characterName;
        player.GetComponent<Player>().account = account;
        player.GetComponent<Player>().className = classPrefab.name;
        player.transform.position = GetStartPosition().position;
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        for (int i = 0; i < inventory.size; ++i)
        {
            // add empty slot or default item if any
            inventory.slots.Add(i < inventory.defaultItems.Length ? new ItemSlot(new Item(inventory.defaultItems[i].item), inventory.defaultItems[i].amount) : new ItemSlot());
        }
        PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
        for (int i = 0; i < equipment.slotInfo.Length; ++i)
        {
            // add empty slot or default item if any
            EquipmentInfo info = equipment.slotInfo[i];
            equipment.slots.Add(info.defaultItem.item != null ? new ItemSlot( new Item(info.defaultItem.item), info.defaultItem.amount) : new ItemSlot());
        }
        PlayerHotbar hotbar = player.GetComponent<PlayerHotbar>();
        for (int i = 0; i < hotbar.size; ++i)
        {
            // add empty slot or default item if any
            hotbar.slots.Add(i < hotbar.defaultItems.Length ? new ItemSlot(new Item(hotbar.defaultItems[i])) : new ItemSlot());
        }
        // fill all energies (after equipment in case of boni)
        foreach (Energy energy in player.GetComponents<Energy>())
            energy.current = energy.max;

        return player;
    }

    void OnServerCharacterCreate(NetworkConnection conn, CharacterCreateMsg message)
    {
        //print("OnServerCharacterCreate " + conn);

        // only while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(conn))
        {
            // allowed character name?
            if (IsAllowedCharacterName(message.name))
            {
                // not existant yet?
                string account = lobby[conn];
                if (!Database.singleton.CharacterExists(message.name))
                {
                    // not too may characters created yet?
                    if (Database.singleton.CharactersForAccount(account).Count < characterLimit)
                    {
                        // valid class index?
                        if (0 <= message.classIndex && message.classIndex < playerClasses.Count)
                        {
                            // create new character based on the prefab.
                            //print("creating character: " + message.name + " " + message.classIndex);
                            GameObject player = CreateCharacter(playerClasses[message.classIndex], message.name, account);

                            // save the player
                            Database.singleton.CharacterSave(player, false);
                            Destroy(player);

                            // send available characters list again, causing
                            // the client to switch to the character
                            // selection scene again
                            conn.Send(MakeCharactersAvailableMessage(account));
                        }
                        else
                        {
                            //print("character invalid class: " + message.classIndex); <- don't show on live server
                            ServerSendError(conn, "character invalid class", false);
                        }
                    }
                    else
                    {
                        //print("character limit reached: " + message.name); <- don't show on live server
                        ServerSendError(conn, "character limit reached", false);
                    }
                }
                else
                {
                    //print("character name already exists: " + message.name); <- don't show on live server
                    ServerSendError(conn, "name already exists", false);
                }
            }
            else
            {
                //print("character name not allowed: " + message.name); <- don't show on live server
                ServerSendError(conn, "character name not allowed", false);
            }
        }
        else
        {
            //print("CharacterCreate: not in lobby"); <- don't show on live server
            ServerSendError(conn, "CharacterCreate: not in lobby", true);
        }
    }

    // overwrite the original OnServerAddPlayer function so nothing happens if
    // someone sends that message.
    public override void OnServerAddPlayer(NetworkConnection conn) { Debug.LogWarning("Use the CharacterSelectMsg instead"); }

    // called after the client calls ClientScene.AddPlayer with a msg parameter
    void OnServerCharacterSelect(NetworkConnection conn, CharacterSelectMsg message)
    {
        //print("OnServerAddPlayer extra");
        // only while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(conn))
        {
            // read the index and find the n-th character
            // (only if we know that he is not ingame, otherwise lobby has
            //  no netMsg.conn key)
            string account = lobby[conn];
            List<string> characters = Database.singleton.CharactersForAccount(account);

            // validate index
            if (0 <= message.value && message.value < characters.Count)
            {
                //print(account + " selected player " + characters[index]);

                // load character data
                GameObject go = Database.singleton.CharacterLoad(characters[message.value], playerClasses, false);

                // add to client
                NetworkServer.AddPlayerForConnection(conn, go);

                // remove from lobby
                lobby.Remove(conn);
            }
            else
            {
                print("invalid character index: " + account + " " + message.value);
                ServerSendError(conn, "invalid character index", false);
            }
        }
        else
        {
            print("AddPlayer: not in lobby" + conn);
            ServerSendError(conn, "AddPlayer: not in lobby", true);
        }
    }

    void OnServerCharacterDelete(NetworkConnection conn, CharacterDeleteMsg message)
    {
        //print("OnServerCharacterDelete " + conn);

        // only while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(conn))
        {
            string account = lobby[conn];
            List<string> characters = Database.singleton.CharactersForAccount(account);

            // validate index
            if (0 <= message.value && message.value < characters.Count)
            {
                // delete the character
                print("delete character: " + characters[message.value]);
                Database.singleton.CharacterDelete(characters[message.value]);

                // send the new character list to client
                conn.Send(MakeCharactersAvailableMessage(account));
            }
            else
            {
                print("invalid character index: " + account + " " + message.value);
                ServerSendError(conn, "invalid character index", false);
            }
        }
        else
        {
            print("CharacterDelete: not in lobby: " + conn);
            ServerSendError(conn, "CharacterDelete: not in lobby", true);
        }
    }

    // saving /////////////////////////////////////////////////////////////////
    void Save()
    {
        // we have to save all players at once to make sure that item trading is
        // perfectly save. if we would invoke a save function every few minutes on
        // each player separately then it could happen that two players trade items
        // and only one of them is saved before a server crash - hence causing item
        // duplicates.
        Database.singleton.CharacterSaveMany(Player.onlinePlayers.Values);
        if (Player.onlinePlayers.Count > 0) Debug.Log("saved " + Player.onlinePlayers.Count + " player(s)");

        // save storages
        Database.singleton.SaveStorages(Storage.storages.Values);
        if (Storage.storages.Count > 0) Debug.Log("saved " + Storage.storages.Count + " storage(s)");

        // save furnaces
        Database.singleton.SaveFurnaces(Furnace.furnaces.Values);
        if (Furnace.furnaces.Count > 0) Debug.Log("saved " + Furnace.furnaces.Count + " furnace(s)");

        // save player generated structures
        Database.singleton.SaveStructures(Structure.structures);
        if (Structure.structures.Count > 0) Debug.Log("saved " + Structure.structures.Count + " structure(s)");
    }

    // stop/disconnect /////////////////////////////////////////////////////////
    // called on the server when a client disconnects
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        print("OnServerDisconnect " + conn);

        // players shouldn't be able to log out instantly to flee combat.
        // there should be a delay.
        float delay = 0;
        if (conn.identity != null)
        {
            Player player = conn.identity.GetComponent<Player>();
            delay = (float)player.remainingLogoutTime;
        }

        StartCoroutine(DoServerDisconnect(conn, delay));
    }

    IEnumerator<WaitForSeconds> DoServerDisconnect(NetworkConnection conn, float delay)
    {
        yield return new WaitForSeconds(delay);

        //print("DoServerDisconnect " + conn);

        // save player (if any. nothing to save if disconnecting while in lobby.)
        if (conn.identity != null)
        {
            Database.singleton.CharacterSave(conn.identity.gameObject, false);
            print("saved:" + conn.identity.name);
        }

        // remove logged in account after everything else was done
        lobby.Remove(conn); // just returns false if not found

        // do base function logic (removes the player for the connection)
        base.OnServerDisconnect(conn);
    }

    // called on the client if he disconnects
    public override void OnClientDisconnect(NetworkConnection conn)
    {
        print("OnClientDisconnect");

        // take the camera out of the local player so it doesn't get destroyed
        if (Camera.main.transform.parent != null)
            Camera.main.transform.SetParent(null);

        // show a popup so that users know what happened
        uiPopup.Show("Disconnected.");

        // call base function to guarantee proper functionality
        base.OnClientDisconnect(conn);

        // set state
        state = NetworkState.Offline;
    }

    // universal quit function for editor & build
    public static void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public override void OnValidate()
    {
        base.OnValidate();

        // ip has to be changed in the server list. make it obvious to users.
        if (!Application.isPlaying && networkAddress != "")
            networkAddress = "Use the Server List below!";
    }
}
