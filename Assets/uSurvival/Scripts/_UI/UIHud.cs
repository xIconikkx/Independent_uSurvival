using UnityEngine;
using UnityEngine.UI;

public class UIHud : MonoBehaviour
{
    public GameObject panel;
    public Slider healthSlider;
    public Text healthStatus;
    public Slider hydrationSlider;
    public Text hydrationStatus;
    public Slider nutritionSlider;
    public Text nutritionStatus;
    public Slider temperatureSlider;
    public Text temperatureStatus;
    public string temperatureUnit = "°C";
    public int temperatureDecimalDigits = 1;
    public Slider enduranceSlider;
    public Text enduranceStatus;
    public Text ammoText;

    void Update()
    {
        GameObject player = Player.localPlayer;
        if (player)
        {
            panel.SetActive(true);

            // health
            Health health = player.GetComponent<Health>();
            healthSlider.value = health.Percent();
            healthStatus.text = health.current + " / " + health.max;

            // hydration
            Hydration hydration = player.GetComponent<Hydration>();
            hydrationSlider.value = hydration.Percent();
            hydrationStatus.text = hydration.current + " / " + hydration.max;

            // nutrition
            Nutrition nutrition = player.GetComponent<Nutrition>();
            nutritionSlider.value = nutrition.Percent();
            nutritionStatus.text = nutrition.current + " / " + nutrition.max;

            // temperature (scaled down, see Temperature script)
            Temperature temperature = player.GetComponent<Temperature>();
            temperatureSlider.value = temperature.Percent();
            float currentTemperature = temperature.current / 100f;
            float maxTemperature = temperature.max / 100f;
            string toStringFormat = "F" + temperatureDecimalDigits.ToString(); // "F2" etc.
            temperatureStatus.text = currentTemperature.ToString(toStringFormat) + " / " +
                                     maxTemperature.ToString(toStringFormat) + " " +
                                     temperatureUnit;

            // endurance
            Endurance endurance = player.GetComponent<Endurance>();
            enduranceSlider.value = endurance.Percent();
            enduranceStatus.text = endurance.current + " / " + endurance.max;

            // ammo
            PlayerHotbar hotbar = player.GetComponent<PlayerHotbar>();
            ItemSlot slot = hotbar.slots[hotbar.selection];
            if (slot.amount > 0 && slot.item.data is RangedWeaponItem)
            {
                RangedWeaponItem itemData = (RangedWeaponItem)slot.item.data;
                if (itemData.requiredAmmo != null)
                {
                    ammoText.text = slot.item.ammo + " / " + itemData.magazineSize;
                }
                else ammoText.text = "0 / 0";
            }
            else ammoText.text = "0 / 0";
        }
        else panel.SetActive(false);
    }
}