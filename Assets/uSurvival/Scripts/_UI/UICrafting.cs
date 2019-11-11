using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UICrafting : MonoBehaviour
{
    public UICraftingIngredientSlot ingredientSlotPrefab;
    public Transform ingredientContent;
    public Image resultSlotImage;
    public UIShowToolTip resultSlotToolTip;
    public Button craftButton;
    public Text resultText;
    public Color successColor = Color.green;
    public Color failedColor = Color.red;

    void Update()
    {
        GameObject player = Player.localPlayer;
        if (player)
        {
            PlayerCrafting crafting = player.GetComponent<PlayerCrafting>();
            Inventory inventory = player.GetComponent<Inventory>();

            // instantiate/destroy enough slots
            UIUtils.BalancePrefabs(ingredientSlotPrefab.gameObject, crafting.indices.Count, ingredientContent);

            // refresh all
            for (int i = 0; i < crafting.indices.Count; ++i)
            {
                UICraftingIngredientSlot slot = ingredientContent.GetChild(i).GetComponent<UICraftingIngredientSlot>();
                slot.dragAndDropable.name = i.ToString(); // drag and drop index
                int itemIndex = crafting.indices[i];

                if (0 <= itemIndex && itemIndex < inventory.slots.Count &&
                    inventory.slots[itemIndex].amount > 0)
                {
                    ItemSlot itemSlot = inventory.slots[itemIndex];

                    // refresh valid item
                    slot.tooltip.enabled = true;
                    slot.tooltip.text = itemSlot.ToolTip();
                    slot.dragAndDropable.dragable = true;
                    slot.image.color = Color.white;
                    slot.image.sprite = itemSlot.item.image;
                    slot.amountOverlay.SetActive(itemSlot.amount > 1);
                    slot.amountText.text = itemSlot.amount.ToString();
                }
                else
                {
                    // reset the index because it's invalid
                    crafting.indices[i] = -1;

                    // refresh invalid item
                    slot.tooltip.enabled = false;
                    slot.dragAndDropable.dragable = false;
                    slot.image.color = Color.clear;
                    slot.image.sprite = null;
                    slot.amountOverlay.SetActive(false);
                }
            }

            // find valid indices => item templates => matching recipe
            List<int> validIndices = crafting.indices.Where(
                index => 0 <= index && index < inventory.slots.Count &&
                       inventory.slots[index].amount > 0
            ).ToList();
            List<ItemSlot> items = validIndices.Select(index => inventory.slots[index]).ToList();
            CraftingRecipe recipe = CraftingRecipe.Find(items);
            if (recipe != null)
            {
                // refresh valid recipe
                Item item = new Item(recipe.result);
                resultSlotToolTip.enabled = true;
                resultSlotToolTip.text = new ItemSlot(item).ToolTip(); // ItemSlot so that {AMOUNT} is replaced too
                resultSlotImage.color = Color.white;
                resultSlotImage.sprite = recipe.result.image;
            }
            else
            {
                // refresh invalid recipe
                resultSlotToolTip.enabled = false;
                resultSlotImage.color = Color.clear;
                resultSlotImage.sprite = null;
            }

            // craft result
            // (no recipe != null check because it will be null if those were
            //  the last two ingredients in our inventory)
            if (crafting.craftingState == CraftingState.Success)
            {
                resultText.color = successColor;
                resultText.text = "Success!";
            }
            else if (crafting.craftingState == CraftingState.Failed)
            {
                resultText.color = failedColor;
                resultText.text = "Failed :(";
            }
            else
            {
                resultText.text = "";
            }

            // craft button with 'Try' prefix to let people know that it might fail
            // (disabled while in progress)
            craftButton.GetComponentInChildren<Text>().text = recipe != null &&
                                                              recipe.probability < 1 ? "Try Craft" : "Craft";
            craftButton.interactable = recipe != null &&
                                       crafting.craftingState != CraftingState.InProgress &&
                                       inventory.CanAdd(new Item(recipe.result), 1);
            craftButton.onClick.SetListener(() => {
                crafting.craftingState = CraftingState.InProgress; // wait for result
                crafting.CmdCraft(recipe.name, validIndices.ToArray());
            });
        }
    }
}
