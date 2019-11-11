// Saves Character Data in a SQLite database. We use SQLite for several reasons
//
// - SQLite is file based and works without having to setup a database server
//   - We can 'remove all ...' or 'modify all ...' easily via SQL queries
//   - A lot of people requested a SQL database and weren't comfortable with XML
//   - We can allow all kinds of character names, even chinese ones without
//     breaking the file system.
// - It's very easy to switch to MYSQL if a real database server is needed later
//
// Tools to open sqlite database files:
//   Windows/OSX program: http://sqlitebrowser.org/
//   Firefox extension: https://addons.mozilla.org/de/firefox/addon/sqlite-manager/
//   Webhost: Adminer/PhpLiteAdmin
//
// About performance:
// - It's recommended to only keep the SQLite connection open while it's used.
//   MMO Servers use it all the time, so we keep it open all the time. This also
//   allows us to use transactions easily, and it will make the transition to
//   MYSQL easier.
// - Transactions are definitely necessary:
//   saving 100 players without transactions takes 3.6s
//   saving 100 players with transactions takes    0.38s
// - Using tr = conn.BeginTransaction() + tr.Commit() and passing it through all
//   the functions is ultra complicated. We use a BEGIN + END queries instead.
//
// Some benchmarks:
//   saving  100 players unoptimized: 4s
//   saving  100 players always open connection + transactions: 3.6s
//   saving  100 players always open connection + transactions + WAL: 3.6s
//   saving  100 players in 1 'using tr = ...' transaction: 380ms
//   saving  100 players in 1 BEGIN/END style transactions: 380ms
//   saving  100 players with XML: 369ms
//   saving 1000 players with mono-sqlite @ 2019-10-03: 843ms
//   saving 1000 players with sqlite-net  @ 2019-10-03:  90ms (!)
//
// Build notes:
// - requires Player settings to be set to '.NET' instead of '.NET Subset',
//   otherwise System.Data.dll causes ArgumentException.
// - requires sqlite3.dll x86 and x64 version for standalone (windows/mac/linux)
//   => found on sqlite.org website
// - requires libsqlite3.so x86 and armeabi-v7a for android
//   => compiled from sqlite.org amalgamation source with android ndk r9b linux
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using SQLite; // from https://github.com/praeclarum/sqlite-net
using Debug = UnityEngine.Debug;

public class Database : MonoBehaviour
{
    // singleton for easier access
    public static Database singleton;

    // file name
    public string databaseFile = "Database.sqlite";

    // connection
    SQLiteConnection connection;

    // database layout via .NET classes:
    // https://github.com/praeclarum/sqlite-net/wiki/GettingStarted
    class accounts
    {
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        public string name { get; set; }
        public string password { get; set; }
        public DateTime created { get; set; }
        public DateTime lastlogin { get; set; }
        public bool banned { get; set; }
    }
    class characters
    {
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        [Collation("NOCASE")] // [COLLATE NOCASE for case insensitive compare. this way we can't both create 'Archer' and 'archer' as characters]
        public string name { get; set; }
        [Indexed] // add index on account to avoid full scans when loading characters
        public string account { get; set; }
        public string classname { get; set; } // 'class' isn't available in C#
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float yrotation { get; set; }
        public int health { get; set; }
        public int hydration { get; set; }
        public int nutrition { get; set; }
        public int temperature { get; set; }
        public int endurance { get; set; }
        public bool online { get; set; }
        public DateTime lastsaved { get; set; }
        public bool deleted { get; set; }
    }
    class character_inventory
    {
        public string character { get; set; }
        public int slot { get; set; }
        public string name { get; set; }
        public int amount { get; set; }
        public int ammo { get; set; }
        public int durability { get; set; }
        // PRIMARY KEY (character, slot) is created manually.
    }
    class character_equipment : character_inventory
    {
        // PRIMARY KEY (character, slot) is created manually.
    }
    class character_hotbar : character_inventory
    {
        // PRIMARY KEY (character, slot) is created manually.
    }
    class character_hotbar_selection
    {
        [PrimaryKey] // important for performance: O(log n) instead of O(n)
        public string character { get; set; }
        public int selection { get; set; }
    }
    class storages
    {
        public string storage { get; set; }
        public int slot { get; set; }
        public string name { get; set; }
        public int amount { get; set; }
        public int ammo { get; set; }
        public int durability { get; set; }
        // PRIMARY KEY (storage, slot) is created manually.
    }
    class furnaces
    {
        public string furnace { get; set; }
        public int slot { get; set; }
        public string name { get; set; }
        public int amount { get; set; }
        public int ammo { get; set; }
        public int durability { get; set; }
        // PRIMARY KEY (furnace, slot) is created manually.
    }
    class structures
    {
        public string name { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float xrotation { get; set; }
        public float yrotation { get; set; }
        public float zrotation { get; set; }
    }
    class houses
    {
        public int houseID { get; set; }
        public string owner { get; set; }

        public bool owned { get; set; }
    }
    class shops
    {
        public int shopID { get; set; }
        public string shopName { get; set; }
        public string owner { get; set; }
        public bool owned { get; set; }
        public bool shopOpen { get; set; }
    }

    void Awake()
    {
        // initialize singleton
        if (singleton == null) singleton = this;
    }

    // connect /////////////////////////////////////////////////////////////////
    // only call this from the server, not from the client. otherwise the client
    // would create a database file / webgl would throw errors, etc.
    public void Connect()
    {
        // initialize singleton
        if (singleton == null) singleton = this;

        // database path: Application.dataPath is always relative to the project,
        // but we don't want it inside the Assets folder in the Editor (git etc.),
        // instead we put it above that.
        // we also use Path.Combine for platform independent paths
        // and we need persistentDataPath on android
#if UNITY_EDITOR
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, databaseFile);
#elif UNITY_ANDROID
        string path = Path.Combine(Application.persistentDataPath, databaseFile);
#elif UNITY_IOS
        string path = Path.Combine(Application.persistentDataPath, databaseFile);
#else
        string path = Path.Combine(Application.dataPath, databaseFile);
#endif

        // open connection
        // note: automatically creates database file if not created yet
        connection = new SQLiteConnection(path);

        // create tables if they don't exist yet or were deleted
        connection.CreateTable<accounts>();
        connection.CreateTable<characters>();
        connection.CreateTable<character_inventory>();
        connection.CreateIndex(nameof(character_inventory), new[] { "character", "slot" });
        connection.CreateTable<character_equipment>();
        connection.CreateIndex(nameof(character_equipment), new[] { "character", "slot" });
        connection.CreateTable<character_hotbar>();
        connection.CreateIndex(nameof(character_hotbar), new[] { "character", "slot" });
        connection.CreateTable<character_hotbar_selection>();
        connection.CreateTable<storages>();
        connection.CreateIndex(nameof(storages), new[] { "storage", "slot" });
        connection.CreateTable<furnaces>();
        connection.CreateIndex(nameof(furnaces), new[] { "furnace", "slot" });
        connection.CreateTable<structures>();
        connection.CreateTable<houses>();
        connection.CreateTable<shops>();

        Debug.Log("connected to database");
    }

    // close connection when Unity closes to prevent locking
    void OnApplicationQuit()
    {
        connection?.Close();
    }

    // account data ////////////////////////////////////////////////////////////
    // try to log in with an account.
    // -> not called 'CheckAccount' or 'IsValidAccount' because it both checks
    //    if the account is valid AND sets the lastlogin field
    public bool TryLogin(string account, string password)
    {
        // this function can be used to verify account credentials in a database
        // or a content management system.
        //
        // for example, we could setup a content management system with a forum,
        // news, shop etc. and then use a simple HTTP-GET to check the account
        // info, for example:
        //
        //   var request = new WWW("example.com/verify.php?id="+id+"&amp;pw="+pw);
        //   while (!request.isDone)
        //       print("loading...");
        //   return request.error == null && request.text == "ok";
        //
        // where verify.php is a script like this one:
        //   <?php
        //   // id and pw set with HTTP-GET?
        //   if (isset($_GET['id']) && isset($_GET['pw']))
        //   {
        //       // validate id and pw by using the CMS, for example in Drupal:
        //       if (user_authenticate($_GET['id'], $_GET['pw']))
        //           echo "ok";
        //       else
        //           echo "invalid id or pw";
        //   }
        //   ?>
        //
        // or we could check in a MYSQL database:
        //   var dbConn = new MySql.Data.MySqlClient.MySqlConnection("Persist Security Info=False;server=localhost;database=notas;uid=root;password=" + dbpwd);
        //   var cmd = dbConn.CreateCommand();
        //   cmd.CommandText = "SELECT id FROM accounts WHERE id='" + account + "' AND pw='" + password + "'";
        //   dbConn.Open();
        //   var reader = cmd.ExecuteReader();
        //   if (reader.Read())
        //       return reader.ToString() == account;
        //   return false;
        //
        // as usual, we will use the simplest solution possible:
        // create account if not exists, compare password otherwise.
        // no CMS communication necessary and good enough for an Indie MMORPG.

        // not empty?
        if (!string.IsNullOrWhiteSpace(account) && !string.IsNullOrWhiteSpace(password))
        {
            // demo feature: create account if it doesn't exist yet.
            // note: sqlite-net has no InsertOrIgnore so we do it in two steps
            if (connection.FindWithQuery<accounts>("SELECT * FROM accounts WHERE name=?", account) == null)
                connection.Insert(new accounts { name = account, password = password, created = DateTime.UtcNow, lastlogin = DateTime.Now, banned = false });

            // check account name, password, banned status
            if (connection.FindWithQuery<accounts>("SELECT * FROM accounts WHERE name=? AND password=? and banned=0", account, password) != null)
            {
                // save last login time and return true
                connection.Execute("UPDATE accounts SET lastlogin=? WHERE name=?", DateTime.UtcNow, account);
                return true;
            }
        }
        return false;
    }

    // character data //////////////////////////////////////////////////////////
    public bool CharacterExists(string characterName)
    {
        // checks deleted ones too so we don't end up with duplicates if we un-
        // delete one
        return connection.FindWithQuery<characters>("SELECT * FROM characters WHERE name=?", characterName) != null;
    }

    public void CharacterDelete(string characterName)
    {
        // soft delete the character so it can always be restored later
        connection.Execute("UPDATE characters SET deleted=1 WHERE name=?", characterName);
    }

    // returns the list of character names for that account
    // => all the other values can be read with CharacterLoad!
    public List<string> CharactersForAccount(string account)
    {
        List<string> result = new List<string>();
        foreach (characters character in connection.Query<characters>("SELECT * FROM characters WHERE account=? AND deleted=0", account))
            result.Add(character.name);
        return result;
    }

    void LoadInventory(PlayerInventory inventory)
    {
        // fill all slots first
        for (int i = 0; i < inventory.size; ++i)
            inventory.slots.Add(new ItemSlot());

        // then load valid items and put into their slots
        // (one big query is A LOT faster than querying each slot separately)
        foreach (character_inventory row in connection.Query<character_inventory>("SELECT * FROM character_inventory WHERE character=?", inventory.name))
        {
            if (row.slot < inventory.size)
            {
                if (ScriptableItem.dict.TryGetValue(row.name.GetStableHashCode(), out ScriptableItem itemData))
                {
                    Item item = new Item(itemData);
                    item.ammo = row.ammo;
                    item.durability = Mathf.Min(row.durability, item.maxDurability);
                    inventory.slots[row.slot] = new ItemSlot(item, row.amount);
                }
                else Debug.LogWarning("LoadInventory: skipped item " + row.name + " for " + inventory.name + " because it doesn't exist anymore. If it wasn't removed intentionally then make sure it's in the Resources folder.");
            }
            else Debug.LogWarning("LoadInventory: skipped slot " + row.slot + " for " + inventory.name + " because it's bigger than size " + inventory.size);
        }
    }

    void LoadEquipment(PlayerEquipment equipment)
    {
        // fill all slots first
        for (int i = 0; i < equipment.slotInfo.Length; ++i)
            equipment.slots.Add(new ItemSlot());

        // then load valid equipment and put into their slots
        // (one big query is A LOT faster than querying each slot separately)
        foreach (character_equipment row in connection.Query<character_equipment>("SELECT * FROM character_equipment WHERE character=?", equipment.name))
        {
            if (row.slot < equipment.slotInfo.Length)
            {
                if (ScriptableItem.dict.TryGetValue(row.name.GetStableHashCode(), out ScriptableItem itemData))
                {
                    Item item = new Item(itemData);
                    item.ammo = row.ammo;
                    item.durability = Mathf.Min(row.durability, item.maxDurability);
                    equipment.slots[row.slot] = new ItemSlot(item, row.amount);
                }
                else Debug.LogWarning("LoadEquipment: skipped item " + row.name + " for " + equipment.name + " because it doesn't exist anymore. If it wasn't removed intentionally then make sure it's in the Resources folder.");
            }
            else Debug.LogWarning("LoadEquipment: skipped slot " + row.slot + " for " + equipment.name + " because it's bigger than size " + equipment.slotInfo.Length);
        }
    }

    void LoadHotbar(PlayerHotbar hotbar)
    {
        // fill all slots first
        for (int i = 0; i < hotbar.size; ++i)
            hotbar.slots.Add(new ItemSlot());

        // then load valid items and put into their slots
        // (one big query is A LOT faster than querying each slot separately)
        foreach (character_hotbar row in connection.Query<character_hotbar>("SELECT * FROM character_hotbar WHERE character=?", hotbar.name))
        {
            if (row.slot < hotbar.size)
            {
                if (ScriptableItem.dict.TryGetValue(row.name.GetStableHashCode(), out ScriptableItem itemData))
                {
                    Item item = new Item(itemData);
                    item.ammo = row.ammo;
                    item.durability = Mathf.Min(row.durability, item.maxDurability);
                    hotbar.slots[row.slot] = new ItemSlot(item, row.amount);
                }
                else Debug.LogWarning("LoadHotbar: skipped item " + row.name + " for " + hotbar.name + " because it doesn't exist anymore. If it wasn't removed intentionally then make sure it's in the Resources folder.");
            }
            else Debug.LogWarning("LoadHotbar: skipped slot " + row.slot + " for " + hotbar.name + " because it's bigger than size " + hotbar.size);
        }
    }

    void LoadHotbarSelection(PlayerHotbar hotbar)
    {
        // load selection
        character_hotbar_selection row = connection.FindWithQuery<character_hotbar_selection>("SELECT * FROM character_hotbar_selection WHERE character=?", hotbar.name);
        if (row != null)
        {
            hotbar.selection = row.selection;
        }
    }

    public GameObject CharacterLoad(string characterName, List<GameObject> prefabs, bool isPreview)
    {
        characters row = connection.FindWithQuery<characters>("SELECT * FROM characters WHERE name=? AND deleted=0", characterName);
        if (row != null)
        {
            // instantiate based on the class name
            GameObject prefab = prefabs.Find(p => p.name == row.classname);
            if (prefab != null)
            {
                GameObject player = Instantiate(prefab.gameObject);

                player.name = row.name;
                player.GetComponent<Player>().account = row.account;
                player.GetComponent<Player>().className = row.classname;
                player.transform.position = new Vector3(row.x, row.y, row.z);
                player.transform.rotation = Quaternion.Euler(0, row.yrotation, 0);

                LoadInventory(player.GetComponent<PlayerInventory>());
                LoadEquipment(player.GetComponent<PlayerEquipment>());
                LoadHotbar(player.GetComponent<PlayerHotbar>());
                LoadHotbarSelection(player.GetComponent<PlayerHotbar>());

                // assign health / hydration etc. after max values were fully loaded
                // (they depend on equipment)
                player.GetComponent<Health>().current = row.health;
                player.GetComponent<Hydration>().current = row.hydration;
                player.GetComponent<Nutrition>().current = row.nutrition;
                player.GetComponent<Temperature>().current = row.temperature;
                player.GetComponent<Endurance>().current = row.endurance;

                // set 'online' directly. otherwise it would only be set during
                // the next CharacterSave() call, which might take 5-10 minutes.
                // => don't set it when loading previews though. only when
                //    really joining the world (hence setOnline flag)
                if (!isPreview)
                    connection.Execute("UPDATE characters SET online=1, lastsaved=? WHERE name=?", characterName, DateTime.UtcNow);

                return player;
            }
            else Debug.LogError("no prefab found for class: " + row.classname);
        }
        return null;
    }

    void SaveInventory(PlayerInventory inventory)
    {
        // inventory: remove old entries first, then add all new ones
        // (we could use UPDATE where slot=... but deleting everything makes
        //  sure that there are never any ghosts)
        connection.Execute("DELETE FROM character_inventory WHERE character=?", inventory.name);
        for (int i = 0; i < inventory.slots.Count; ++i)
        {
            ItemSlot slot = inventory.slots[i];
            if (slot.amount > 0) // only relevant items to save queries/storage/time
            {
                // note: .Insert causes a 'Constraint' exception. use Replace.
                connection.InsertOrReplace(new character_inventory
                {
                    character = inventory.name,
                    slot = i,
                    name = slot.item.name,
                    amount = slot.amount,
                    ammo = slot.item.ammo,
                    durability = slot.item.durability
                });
            }
        }
    }

    void SaveEquipment(PlayerEquipment equipment)
    {
        // equipment: remove old entries first, then add all new ones
        // (we could use UPDATE where slot=... but deleting everything makes
        //  sure that there are never any ghosts)
        connection.Execute("DELETE FROM character_equipment WHERE character=?", equipment.name);
        for (int i = 0; i < equipment.slots.Count; ++i)
        {
            ItemSlot slot = equipment.slots[i];
            if (slot.amount > 0) // only relevant items to save queries/storage/time
            {
                // note: .Insert causes a 'Constraint' exception. use Replace.
                connection.InsertOrReplace(new character_equipment
                {
                    character = equipment.name,
                    slot = i,
                    name = slot.item.name,
                    amount = slot.amount,
                    ammo = slot.item.ammo,
                    durability = slot.item.durability
                });
            }
        }
    }

    void SaveHotbar(PlayerHotbar hotbar)
    {
        // hotbar: remove old entries first, then add all new ones
        // (we could use UPDATE where slot=... but deleting everything makes
        //  sure that there are never any ghosts)
        connection.Execute("DELETE FROM character_hotbar WHERE character=?", hotbar.name);
        for (int i = 0; i < hotbar.slots.Count; ++i)
        {
            ItemSlot slot = hotbar.slots[i];
            if (slot.amount > 0) // only relevant items to save queries/storage/time
            {
                // note: .Insert causes a 'Constraint' exception. use Replace.
                connection.InsertOrReplace(new character_hotbar
                {
                    character = hotbar.name,
                    slot = i,
                    name = slot.item.name,
                    amount = slot.amount,
                    ammo = slot.item.ammo,
                    durability = slot.item.durability
                });
            }
        }
    }

    void SaveHotbarSelection(PlayerHotbar hotbar)
    {
        connection.InsertOrReplace(new character_hotbar_selection { character = hotbar.name, selection = hotbar.selection });
    }

    // adds or overwrites character data in the database
    public void CharacterSave(GameObject player, bool online, bool useTransaction = true)
    {
        // only use a transaction if not called within SaveMany transaction
        if (useTransaction) connection.BeginTransaction();

        connection.InsertOrReplace(new characters
        {
            name = player.name,
            account = player.GetComponent<Player>().account,
            classname = player.GetComponent<Player>().className,
            x = player.transform.position.x,
            y = player.transform.position.y,
            z = player.transform.position.z,
            yrotation = player.transform.rotation.eulerAngles.y,
            health = player.GetComponent<Health>().current,
            hydration = player.GetComponent<Hydration>().current,
            nutrition = player.GetComponent<Nutrition>().current,
            temperature = player.GetComponent<Temperature>().current,
            endurance = player.GetComponent<Endurance>().current,
            online = online,
            lastsaved = DateTime.UtcNow
        });

        SaveInventory(player.GetComponent<PlayerInventory>());
        SaveEquipment(player.GetComponent<PlayerEquipment>());
        SaveHotbar(player.GetComponent<PlayerHotbar>());
        SaveHotbarSelection(player.GetComponent<PlayerHotbar>());

        if (useTransaction) connection.Commit();
    }

    // save multiple characters at once (useful for ultra fast transactions)
    public void CharacterSaveMany(IEnumerable<GameObject> players, bool online = true)
    {
        connection.BeginTransaction(); // transaction for performance
        foreach (GameObject player in players)
            CharacterSave(player, online, false);
        connection.Commit(); // end transaction
    }

    // storage /////////////////////////////////////////////////////////////////
    public void LoadStorage(Storage storage)
    {
        // fill all slots first
        for (int i = 0; i < storage.size; ++i)
            storage.slots.Add(new ItemSlot());

        // then load valid items and put into their slots
        // (one big query is A LOT faster than querying each slot separately)
        foreach (storages row in connection.Query<storages>("SELECT * FROM storages WHERE storage=?", storage.name))
        {
            if (row.slot < storage.size &&
                ScriptableItem.dict.TryGetValue(row.name.GetStableHashCode(), out ScriptableItem itemData))
            {
                Item item = new Item(itemData);
                item.ammo = row.ammo;
                item.durability = Mathf.Min(row.durability, item.maxDurability);
                storage.slots[row.slot] = new ItemSlot(item, row.amount);
            }
        }
    }

    void SaveStorage(Storage storage, bool useTransaction = true)
    {
        // only use a transaction if not called within SaveMany transaction
        if (useTransaction) connection.BeginTransaction();

        // storage: remove old entries first, then add all new ones
        // (we could use UPDATE where slot=... but deleting everything makes
        //  sure that there are never any ghosts)
        connection.Execute("DELETE FROM storages WHERE storage=?", storage.name);
        for (int i = 0; i < storage.slots.Count; ++i)
        {
            ItemSlot slot = storage.slots[i];
            if (slot.amount > 0) // only relevant items to save queries/storage/time
            {
                // note: .Insert causes a 'Constraint' exception. use Replace.
                connection.InsertOrReplace(new storages
                {
                    storage = storage.name,
                    slot = i,
                    name = slot.item.name,
                    amount = slot.amount,
                    ammo = slot.item.ammo,
                    durability = slot.item.durability
                });
            }
        }

        if (useTransaction) connection.Commit();
    }

    // save multiple storages at once (useful for ultra fast transactions)
    public void SaveStorages(IEnumerable<Storage> storages)
    {
        connection.BeginTransaction(); // transaction for performance
        foreach (Storage storage in storages)
            SaveStorage(storage, false);
        connection.Commit(); // end transaction
    }

    // furnace /////////////////////////////////////////////////////////////////
    public void LoadFurnace(Furnace furnace)
    {
        // load all saved slots first. it's not guaranteed that we have exactly
        // 3 of them for ingredient+fuel+result. we only save the non-empty ones
        // (one big query is A LOT faster than querying each slot separately)
        foreach (furnaces row in connection.Query<furnaces>("SELECT * FROM furnaces WHERE furnace=?", furnace.name))
        {
            if (ScriptableItem.dict.TryGetValue(row.name.GetStableHashCode(), out ScriptableItem itemData))
            {
                Item item = new Item(itemData);
                item.ammo = row.ammo;
                item.durability = Mathf.Min(row.durability, item.maxDurability);

                // ingredient?
                if (row.slot == 0)
                    furnace.ingredientSlot = new ItemSlot(item, row.amount);
                // fuel slot?
                else if (row.slot == 1)
                    furnace.fuelSlot = new ItemSlot(item, row.amount);
                // result slot?
                else if (row.slot == 2)
                    furnace.resultSlot = new ItemSlot(item, row.amount);
            }
        }
    }

    void SaveFurnace(Furnace furnace, bool useTransaction = true)
    {
        // only use a transaction if not called within SaveMany transaction
        if (useTransaction) connection.BeginTransaction();

        // furnace: remove old entries first, then add all new ones
        // (we could use UPDATE where slot=... but deleting everything makes
        //  sure that there are never any ghosts)
        connection.Execute("DELETE FROM furnaces WHERE furnace=?", furnace.name);

        // save ingredient slot at index 0
        if (furnace.ingredientSlot.amount > 0) // only relevant items to save queries/storage/time
        {
            // note: .Insert causes a 'Constraint' exception. use Replace.
            connection.InsertOrReplace(new furnaces
            {
                furnace = furnace.name,
                slot = 0,
                name = furnace.ingredientSlot.item.name,
                amount = furnace.ingredientSlot.amount,
                ammo = furnace.ingredientSlot.item.ammo,
                durability = furnace.ingredientSlot.item.durability
            });
        }

        // save fuel slot at index 1
        if (furnace.fuelSlot.amount > 0) // only relevant items to save queries/storage/time
        {
            // note: .Insert causes a 'Constraint' exception. use Replace.
            connection.InsertOrReplace(new furnaces
            {
                furnace = furnace.name,
                slot = 1,
                name = furnace.fuelSlot.item.name,
                amount = furnace.fuelSlot.amount,
                ammo = furnace.fuelSlot.item.ammo,
                durability = furnace.fuelSlot.item.durability
            });
        }

        // save result slot at index 2
        if (furnace.resultSlot.amount > 0) // only relevant items to save queries/storage/time
        {
            // note: .Insert causes a 'Constraint' exception. use Replace.
            connection.InsertOrReplace(new furnaces
            {
                furnace = furnace.name,
                slot = 2,
                name = furnace.resultSlot.item.name,
                amount = furnace.resultSlot.amount,
                ammo = furnace.resultSlot.item.ammo,
                durability = furnace.resultSlot.item.durability
            });
        }

        if (useTransaction) connection.Commit();
    }

    // save multiple furnaces at once (useful for ultra fast transactions)
    public void SaveFurnaces(IEnumerable<Furnace> furnaces)
    {
        connection.BeginTransaction(); // transaction for performance
        foreach (Furnace furnace in furnaces)
            SaveFurnace(furnace, false);
        connection.Commit(); // end transaction
    }

    // structures //////////////////////////////////////////////////////////////
    void SaveStructure(Structure structure, bool useTransaction = true)
    {
        // only use a transaction if not called within SaveMany transaction
        if (useTransaction) connection.BeginTransaction();

        // get position and rotation (so we don't have to access .transform 6x)
        Vector3 position = structure.transform.position;
        Vector3 rotation = structure.transform.rotation.eulerAngles;

        connection.Insert(new structures
        {
            name = structure.name,
            x = position.x,
            y = position.y,
            z = position.z,
            xrotation = rotation.x,
            yrotation = rotation.y,
            zrotation = rotation.z
        });

        if (useTransaction) connection.Commit();
    }

    public void SaveStructures(HashSet<Structure> structures)
    {
        connection.BeginTransaction(); // transaction for performance

        // remove all old entries first, then add all the new ones
        // (we could use UPDATE where ... but deleting everything makes sure
        //  that there are never any ghosts)
        connection.DeleteAll<structures>();
        foreach (Structure structure in structures)
            SaveStructure(structure, false);

        connection.Commit(); // end transaction
    }

    // loads and spawns all structures
    public void LoadStructures()
    {
        // build a dict of spawnable structures so we don't have to go through
        // the networkmanager's spawnable prefabs for each one of them
        Dictionary<string, GameObject> spawnable = NetworkManager.singleton.spawnPrefabs
                                                     .Where(p => p.tag == "Structure")
                                                     .ToDictionary(p => p.name, p => p);

        foreach (structures row in connection.Query<structures>("SELECT * FROM structures"))
        {
            // do we still have a spawnable structure with that name?
            if (spawnable.ContainsKey(row.name))
            {
                Vector3 position = new Vector3(row.x, row.y, row.z);
                Quaternion rotation = Quaternion.Euler(row.xrotation, row.yrotation, row.zrotation);
                GameObject prefab = spawnable[row.name];
                GameObject go = Instantiate(prefab, position, rotation);
                go.name = prefab.name; // avoid "(Clone)". important for saving.
                NetworkServer.Spawn(go);
            }
        }
    }

    public void SaveHouse(int UID, string Owner, bool houseOwned)
    {

        if (connection.FindWithQuery<houses>("SELECT * FROM houses WHERE houseID=?", UID) == null)
        {
            connection.Insert(new houses { houseID = UID, owner = Owner, owned = houseOwned });
        }
        else if (connection.FindWithQuery<houses>("SELECT * FROM houses WHERE houseID=?", UID) != null)
        {
            connection.Execute("UPDATE houses SET owner=? WHERE houseID=?", Owner, UID);
            connection.Execute("UPDATE houses SET owned=? WHERE houseID=?", houseOwned, UID);
        }
    }

    public void LoadHouse(int UID, HousingSystem house)
    {
        if (connection.FindWithQuery<houses>("SELECT * FROM houses WHERE houseID=?", UID) == null)
        {
            SaveHouse(UID, "", false);
        }
        else
        {
            foreach (houses row in connection.Query<houses>("SELECT * FROM houses WHERE houseID=?", UID))
            {
                house.HouseInfoReturn(row.houseID, row.owner, row.owned);
            }
        }
    }

    public void SaveShop(int UID, string shop_Name ,string Owner, bool shopOwned)
    {

        if (connection.FindWithQuery<shops>("SELECT * FROM shops WHERE shopID=?", UID) == null)
        {
            connection.Insert(new shops { shopID = UID, shopName = shop_Name, owner = Owner, owned = shopOwned });
        }
        else if (connection.FindWithQuery<shops>("SELECT * FROM shops WHERE shopID=?", UID) != null)
        {
            connection.Execute("UPDATE shops SET owner=? WHERE shopID=?", Owner, UID);
            connection.Execute("UPDATE shops SET owned=? WHERE shopID=?", shopOwned, UID);
        }
    }

    public void LoadShop(int UID, ShopSystem shop)
    {
        if (connection.FindWithQuery<shops>("SELECT * FROM shops WHERE shopID=?", UID) == null)
        {
            SaveShop(UID,"" ,"", false);
        }
        else
        {
            foreach (shops row in connection.Query<shops>("SELECT * FROM shops WHERE shopID=?", UID))
            {
                shop.ShopInfoReturn(row.shopID, row.shopName ,row.owner, row.owned);
            }
        }
    }
}