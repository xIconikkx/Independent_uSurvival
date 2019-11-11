using UnityEngine;
using UnityEngine.UI;

public class UIProgressBar : MonoBehaviour
{
    public GameObject panel;
    public Slider slider;
    public Text actionText;
    public Text progressText;

    bool ReloadInProgress(GameObject player, out float percentage, out string action, out string progress)
    {
        percentage = 0;
        action = "";
        progress = "";

        // get components
        PlayerHotbar hotbar = player.GetComponent<PlayerHotbar>();
        PlayerReloading reloading = player.GetComponent<PlayerReloading>();

        // currently reloading?
        ItemSlot slot = hotbar.slots[hotbar.selection];
        if (slot.amount > 0 && slot.item.data is RangedWeaponItem)
        {
            float reloadTime = ((RangedWeaponItem)slot.item.data).reloadTime;
            if (reloading.ReloadTimeRemaining() > 0)
            {
                percentage = (reloadTime - reloading.ReloadTimeRemaining()) / reloadTime;
                action = "Reloading:";
                progress = (percentage * 100).ToString("F0") + "%";
                return true;
            }
        }

        return false;
    }

    void Update()
    {
        GameObject player = Player.localPlayer;
        if (player)
        {
            panel.SetActive(true);
            float percentage;
            string action;
            string progress;

            //  reloading?
            if (ReloadInProgress(player, out percentage, out action, out progress))
            {
                panel.SetActive(true);
                slider.value = percentage;
                actionText.text = action;
                progressText.text = progress;
            }
            // otherwise hide
            else panel.SetActive(false);
        }
        else panel.SetActive(false);
    }
}
