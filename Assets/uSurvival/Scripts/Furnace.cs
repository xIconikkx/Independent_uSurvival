using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Furnace : NetworkBehaviour, Interactable
{
    [SyncVar, HideInInspector] public ItemSlot ingredientSlot;
    [SyncVar, HideInInspector] public ItemSlot fuelSlot;
    [SyncVar, HideInInspector] public ItemSlot resultSlot;

    // to save computations, we only search if slots have changed.
    // (SyncVar hooks are only called on clients, can't use those on server)
    ItemSlot lastIngredientSlot;
    ItemSlot lastFuelSlot;

    // cache all storages on the server to save lots of computations
    // (otherwise we'd have to iterate NetworkServer.objects all the time)
    // -> differentiate them by name. make sure to not use the same name for
    //    two storages.
    //    (sceneId is not a good alternative because it changes when changing
    //     the hierarchy)
    public static Dictionary<string, Furnace> furnaces = new Dictionary<string, Furnace>();

    // bake in progress
    // -> save the recipe during the bake so we don't have to search it again
    public FurnaceRecipe currentBake;
    [SyncVar] public double bakeTimeStart; // so we can show the progress on clients
    [SyncVar] public double bakeTimeEnd;

    public override void OnStartServer()
    {
        // add to storages if none with the same name already exists
        if (!furnaces.ContainsKey(name))
        {
            furnaces[name] = this;

            // load from db
            Database.singleton.LoadFurnace(this);
        }
        else Debug.LogWarning("A Furnace with name " + name + " already exists. Use a different name for each Furnace, otherwise it won't be saved to the Database.");
    }

    void OnDestroy()
    {
        furnaces.Remove(name);
    }

    public string GetInteractionText() { return "Furnace"; }

    [Client]
    public void OnInteractClient(GameObject player)
    {
        UIMainPanel.singleton.Show();
    }

    [Server]
    public void OnInteractServer(GameObject player) {}

    // helper function to check if we are currently baking. works on server &
    // client.
    public bool IsBaking() => NetworkTime.time < bakeTimeEnd;

    // start a bake
    [Server]
    public bool StartBaking(FurnaceRecipe recipe)
    {
        // result slot needs to be empty or of same type and not maxstack
        if (resultSlot.amount == 0 ||
            (resultSlot.item.data == recipe.result &&
             resultSlot.amount < resultSlot.item.maxStack))
        {
            // start baking
            currentBake = recipe;
            bakeTimeStart = NetworkTime.time;
            bakeTimeEnd = NetworkTime.time + recipe.bakingTime;
            return true;
        }
        return false;
    }

    // finish the current bake
    [Server]
    public void FinishBaking()
    {
        // only if we have a valid recipe
        if (currentBake != null)
        {
            // decrease ingredient amount
            ingredientSlot.DecreaseAmount(1);

            // decrease fuel amount
            fuelSlot.DecreaseAmount(1);

            // increase result amount or create new one
            // (we checked maxStack before baking, no need to check again here)
            if (resultSlot.amount > 0 && resultSlot.item.data == currentBake.result)
                resultSlot.IncreaseAmount(1);
            else
                resultSlot = new ItemSlot(new Item(currentBake.result), 1);

            // not baking anymore.
            currentBake = null;
        }
    }

    [Server]
    public void CancelBaking()
    {
        // cancel baking if needed
        currentBake = null;
        bakeTimeEnd = NetworkTime.time;
    }

    [ServerCallback]
    void Update()
    {
        // currently baking?
        if (IsBaking())
        {
            // just wait
        }
        // a bake that was in progress is now finished?
        else if (currentBake != null)
        {
            FinishBaking();
        }
        // not baking. start a new bake if possible.
        else
        {
            // has ingredient + fuel? and
            if (ingredientSlot.amount > 0 &&
                fuelSlot.amount > 0)
            {
                // only search if something changed to save computations!
                if (!lastIngredientSlot.Equals(ingredientSlot) ||
                    !lastFuelSlot.Equals(fuelSlot))
                {
                    // find recipe (if any)
                    FurnaceRecipe recipe = FurnaceRecipe.Find(ingredientSlot, fuelSlot);
                    if (recipe != null)
                    {
                        // try to start baking
                        StartBaking(recipe);
                    }

                }
            }

            // remember the last searched slot in any case (even if amount == 0)
            lastIngredientSlot = ingredientSlot;
            lastFuelSlot = fuelSlot;
        }
    }
}
