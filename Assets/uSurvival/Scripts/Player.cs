// keep track of some player data like class, account etc.
using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class SyncDictionaryIntDouble : SyncDictionary<int, double> {}

public class Player : NetworkBehaviour
{
    static public string PlayerName;
    public Combat combat;
    [HideInInspector] public string account = "";
    [HideInInspector] public string className = "";
    public Sprite classIcon; // for character selection

    // first allowed logout time after combat
    public double allowedLogoutTime => combat.lastCombatTime + ((NetworkManagerSurvival)NetworkManager.singleton).combatLogoutDelay;
    public double remainingLogoutTime => NetworkTime.time < allowedLogoutTime ? (allowedLogoutTime - NetworkTime.time) : 0;

    // online players cache on the server to save lots of computations
    // (otherwise we'd have to iterate NetworkServer.objects all the time)
    public static Dictionary<string, GameObject> onlinePlayers = new Dictionary<string, GameObject>();

    // localPlayer singleton for easier access from UI scripts etc.
    public static GameObject localPlayer =>
        ClientScene.localPlayer != null ? ClientScene.localPlayer.gameObject : null;

    // item cooldowns
    // it's based on a 'cooldownCategory' that can be set in ScriptableItems.
    // -> they can use their own name for a cooldown that only applies to them
    // -> they can use a category like 'HealthPotion' for a shared cooldown
    //    amongst all health potions
    // => we use hash(category) as key to significantly reduce bandwidth!
    SyncDictionaryIntDouble itemCooldowns = new SyncDictionaryIntDouble();

    // additional cooldowns dictionary for local player prediction.
    // this way we don't have to experience latency effects while firing fast
    // weapons
    // (we can't use itemCooldowns because that can only be used on server due
    //  to the delta compression nature)
    Dictionary<int, double> itemCooldownsPrediction = new Dictionary<int, double>();

    public override void OnStartServer()
    {
        onlinePlayers[name] = gameObject;
        PlayerName = this.gameObject.name;
        Debug.Log("Local Player Name: " + PlayerName);
    }

    void OnDestroy()
    {
        onlinePlayers.Remove(name);
    }

    // get remaining item cooldown, or 0 if none
    public float GetItemCooldown(string cooldownCategory)
    {
        // get stable hash to reduce bandwidth
        int hash = cooldownCategory.GetStableHashCode();

        // local player? then see if we have a prediction in there
        if (isLocalPlayer)
        {
            if (itemCooldownsPrediction.TryGetValue(hash, out double cooldownPredictionEnd))
            {
                return NetworkTime.time >= cooldownPredictionEnd ? 0 : (float)(cooldownPredictionEnd - NetworkTime.time);
            }
        }

        // otherwise use the regular one
        if (itemCooldowns.TryGetValue(hash, out double cooldownEnd))
        {
            return NetworkTime.time >= cooldownEnd ? 0 : (float)(cooldownEnd - NetworkTime.time);
        }

        // none found
        return 0;
    }

    // reset item cooldown
    public void SetItemCooldown(string cooldownCategory, float cooldown)
    {
        // get stable hash to reduce bandwidth
        int hash = cooldownCategory.GetStableHashCode();

        // calculate end time
        double cooldownEnd = NetworkTime.time + cooldown;

        // called by local player for prediction?
        if (isLocalPlayer)
            itemCooldownsPrediction[hash] = cooldownEnd;
        else
            itemCooldowns[hash] = cooldownEnd;
    }

    
}