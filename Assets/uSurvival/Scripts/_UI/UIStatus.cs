// Note: this script has to be on an always-active UI parent, so that we can
// always react to the hotkey.
using UnityEngine;
using UnityEngine.UI;

public class UIStatus : MonoBehaviour
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

    public Text moneyTxt;
    public Text defenseText;

    void Update()
    {
        GameObject player = Player.localPlayer;
        if (player)
        {
            Health health = player.GetComponent<Health>();
            healthSlider.value = health.Percent();
            healthStatus.text = health.current + " / " + health.max;

            Hydration hydration = player.GetComponent<Hydration>();
            hydrationSlider.value = hydration.Percent();
            hydrationStatus.text = hydration.current + " / " + hydration.max;

            Nutrition nutrition = player.GetComponent<Nutrition>();
            nutritionSlider.value = nutrition.Percent();
            nutritionStatus.text = nutrition.current + " / " + nutrition.max;

            Temperature temperature = player.GetComponent<Temperature>();
            temperatureSlider.value = temperature.Percent();
            float currentTemperature = temperature.current / 100f;
            float maxTemperature = temperature.max / 100f;
            string toStringFormat = "F" + temperatureDecimalDigits.ToString(); // "F2" etc.
            temperatureStatus.text = currentTemperature.ToString(toStringFormat) + " / " +
                                     maxTemperature.ToString(toStringFormat) + " " +
                                     temperatureUnit;

            Endurance endurance = player.GetComponent<Endurance>();
            enduranceSlider.value = endurance.Percent();
            enduranceStatus.text = endurance.current + " / " + endurance.max;

            moneyTxt.text = "$" + player.GetComponent<Money>().FetchMoney().ToString();
            defenseText.text = player.GetComponent<Combat>().defense.ToString();
        }
    }
}
