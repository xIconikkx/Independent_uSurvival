using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class UIFurnace : MonoBehaviour
{
    public GameObject panel;
    public Slider progressSlider;

    [Header("Ingredient UI Slot")]
    public UIDragAndDropable ingredient;
    public Image ingredientImage;
    public UIShowToolTip ingredientToolip;
    public GameObject ingredientOverlay;
    public Text ingredientAmountText;

    [Header("Fuel UI Slot")]
    public UIDragAndDropable fuel;
    public Image fuelImage;
    public UIShowToolTip fuelToolip;
    public GameObject fuelOverlay;
    public Text fuelAmountText;

    [Header("Result UI Slot")]
    public UIDragAndDropable result;
    public Image resultImage;
    public UIShowToolTip resultToolip;
    public GameObject resultOverlay;
    public Text resultAmountText;

    [Header("Durability Colors")]
    public Color brokenDurabilityColor = Color.red;
    public Color lowDurabilityColor = Color.magenta;
    [Range(0.01f, 0.99f)] public float lowDurabilityThreshold = 0.1f;

    void Update()
    {
        GameObject player = Player.localPlayer;
        if (player)
        {
            PlayerInteraction interaction = player.GetComponent<PlayerInteraction>();
            if (interaction.current != null && ((NetworkBehaviour)interaction.current).GetComponent<Furnace>() != null)
            {
                panel.SetActive(true);

                Furnace furnace = ((NetworkBehaviour)interaction.current).GetComponent<Furnace>();

                // refresh ingredient slot
                if (furnace.ingredientSlot.amount > 0)
                {
                    // refresh valid item
                    ingredientToolip.enabled = true;
                    ingredientToolip.text = furnace.ingredientSlot.ToolTip();
                    ingredient.dragable = true;
                    // use durability colors?
                    if (furnace.ingredientSlot.item.maxDurability > 0)
                    {
                        if (furnace.ingredientSlot.item.durability == 0)
                            ingredientImage.color = brokenDurabilityColor;
                        else if (furnace.ingredientSlot.item.DurabilityPercent() < lowDurabilityThreshold)
                            ingredientImage.color = lowDurabilityColor;
                        else
                            ingredientImage.color = Color.white;
                    }
                    else ingredientImage.color = Color.white; // reset for no-durability items
                    ingredientImage.sprite = furnace.ingredientSlot.item.image;
                    ingredientOverlay.SetActive(furnace.ingredientSlot.amount > 1);
                    ingredientAmountText.text = furnace.ingredientSlot.amount.ToString();
                }
                else
                {
                    // refresh invalid item
                    ingredientToolip.enabled = false;
                    ingredient.dragable = false;
                    ingredientImage.color = Color.clear;
                    ingredientImage.sprite = null;
                    ingredientOverlay.SetActive(false);
                }

                // refresh fuel slot
                if (furnace.fuelSlot.amount > 0)
                {
                    // refresh valid item
                    fuelToolip.enabled = true;
                    fuelToolip.text = furnace.fuelSlot.ToolTip();
                    fuel.dragable = true;
                    // use durability colors?
                    if (furnace.fuelSlot.item.maxDurability > 0)
                    {
                        if (furnace.fuelSlot.item.durability == 0)
                            fuelImage.color = brokenDurabilityColor;
                        else if (furnace.fuelSlot.item.DurabilityPercent() < lowDurabilityThreshold)
                            fuelImage.color = lowDurabilityColor;
                        else
                            fuelImage.color = Color.white;
                    }
                    else fuelImage.color = Color.white; // reset for no-durability items
                    fuelImage.sprite = furnace.fuelSlot.item.image;
                    fuelOverlay.SetActive(furnace.fuelSlot.amount > 1);
                    fuelAmountText.text = furnace.fuelSlot.amount.ToString();
                }
                else
                {
                    // refresh invalid item
                    fuelToolip.enabled = false;
                    fuel.dragable = false;
                    fuelImage.color = Color.clear;
                    fuelImage.sprite = null;
                    fuelOverlay.SetActive(false);
                }

                // refresh result slot
                if (furnace.resultSlot.amount > 0)
                {
                    // refresh valid item
                    resultToolip.enabled = true;
                    resultToolip.text = furnace.resultSlot.ToolTip();
                    result.dragable = true;
                    // use durability colors?
                    if (furnace.resultSlot.item.maxDurability > 0)
                    {
                        if (furnace.resultSlot.item.durability == 0)
                            resultImage.color = brokenDurabilityColor;
                        else if (furnace.resultSlot.item.DurabilityPercent() < lowDurabilityThreshold)
                            resultImage.color = lowDurabilityColor;
                        else
                            resultImage.color = Color.white;
                    }
                    else resultImage.color = Color.white; // reset for no-durability items
                    resultImage.sprite = furnace.resultSlot.item.image;
                    resultOverlay.SetActive(furnace.resultSlot.amount > 1);
                    resultAmountText.text = furnace.resultSlot.amount.ToString();
                }
                else
                {
                    // refresh invalid item
                    resultToolip.enabled = false;
                    result.dragable = false;
                    resultImage.color = Color.clear;
                    resultImage.sprite = null;
                    resultOverlay.SetActive(false);
                }

                // show progress bar while baking
                // (show 100% if craft time = 0 because it's just better feedback)
                double elapsedTime = NetworkTime.time - furnace.bakeTimeStart;
                double bakingTime = furnace.bakeTimeEnd - furnace.bakeTimeStart;
                progressSlider.value = furnace.IsBaking() ? (float)(elapsedTime / bakingTime) : 0;
            }
            else panel.SetActive(false);
        }
    }
}
