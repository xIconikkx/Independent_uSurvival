using UnityEngine;
using UnityEngine.UI;

public class UIMainPanel : MonoBehaviour
{
    // singleton to access it from player scripts without FindObjectOfType
    public static UIMainPanel singleton;

    public KeyCode hotKey = KeyCode.Tab;
    public GameObject panel;
    public Button quitButton;

    public UIMainPanel()
    {
        // assign singleton only once (to work with DontDestroyOnLoad when
        // using Zones / switching scenes)
        if (singleton == null) singleton = this;
    }

    void Update()
    {
        GameObject player = Player.localPlayer;
        if (player)
        {
            // hotkey (not while typing in chat, etc.)
            if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
                panel.SetActive(!panel.activeSelf);

            // show "(5)Quit" if we can't log out during combat
            // -> CeilToInt so that 0.1 shows as '1' and not as '0'
            Player playerComponent = player.GetComponent<Player>();
            string quitPrefix = "";
            if (playerComponent.remainingLogoutTime > 0)
                quitPrefix = "(" + Mathf.CeilToInt((float)playerComponent.remainingLogoutTime) + ") ";
            quitButton.GetComponent<UIShowToolTip>().text = quitPrefix + "Quit";
            quitButton.interactable = playerComponent.remainingLogoutTime == 0;
            quitButton.onClick.SetListener(NetworkManagerSurvival.Quit);
        }
    }

    public void Show()
    {
        panel.SetActive(true);
    }
}
