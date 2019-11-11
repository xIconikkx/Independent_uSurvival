// Attach to the prefab for easier component access by the UI Scripts.
// Otherwise we would need slot.GetChild(0).GetComponentInChildren<Text> etc.
using UnityEngine;
using UnityEngine.UI;

public class UIHotbarSlot : MonoBehaviour
{
    public UIShowToolTip tooltip;
    public UIDragAndDropable dragAndDropable;
    public Image image;
    public Button button;
    public Image cooldownCircle;
    public GameObject amountOverlay;
    public Text amountText;
    public Text hotkeyText;
    public GameObject selectionOutline;
}
