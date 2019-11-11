using UnityEngine;
using UnityEngine.UI;

public class UIConstructionHotkeys : MonoBehaviour
{
    public GameObject panel;
    public Text rotationText;

    void Update()
    {
        // holding a structure?
        GameObject player = Player.localPlayer;
        if (player != null)
        {
            PlayerConstruction construction = player.GetComponent<PlayerConstruction>();
            rotationText.text = construction.rotationKey + " - Rotate";
            panel.SetActive(construction.GetCurrentStructure() != null);
        }
        else panel.SetActive(false);
    }
}
