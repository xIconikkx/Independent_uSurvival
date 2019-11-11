// Simple character selection list. The charcter prefabs are known, so we could
// easily show 3D models, stats, etc. too.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class UICharacterSelection : MonoBehaviour
{
    public UICharacterCreation uiCharacterCreation;
    public UIConfirmation uiConfirmation;
    public NetworkManagerSurvival manager; // singleton is null until update
    public GameObject panel;
    public UICharacterSelectionSlot slotPrefab;
    public Transform content;
    // available characters (set after receiving the message from the server)
    public Button createButton;
    public Button quitButton;

    void Update()
    {
        // show while in lobby and while not creating a character
        if (manager.state == NetworkState.Lobby && !uiCharacterCreation.IsVisible())
        {
            panel.SetActive(true);

            // characters available message received already?
            if (manager.charactersAvailableMsg != null)
            {
                // instantiate/destroy enough slots
                CharactersAvailableMsg.CharacterPreview[] characters = manager.charactersAvailableMsg.characters;
                UIUtils.BalancePrefabs(slotPrefab.gameObject, characters.Length, content);

                // refresh all
                for (int i = 0; i < characters.Length; ++i)
                {
                    GameObject prefab = manager.playerClasses.Find(p => p.name == characters[i].className);
                    UICharacterSelectionSlot slot = content.GetChild(i).GetComponent<UICharacterSelectionSlot>();

                    // name and icon
                    slot.nameText.text = characters[i].name;
                    slot.image.sprite = prefab.GetComponent<Player>().classIcon;

                    // select button: calls AddPlayer which calls OnServerAddPlayer
                    // -> button sends a request to the server
                    // -> if we press button again while request hasn't finished
                    //    then we will get the error:
                    //    'ClientScene::AddPlayer: playerControllerId of 0 already in use.'
                    //    which will happen sometimes at low-fps or high-latency
                    // -> internally ClientScene.AddPlayer adds to localPlayers
                    //    immediately, so let's check that first
                    slot.selectButton.interactable = ClientScene.localPlayer == null;
                    int icopy = i; // needed for lambdas, otherwise i is Count
                    slot.selectButton.onClick.SetListener(() => {
                        // set client "ready". we will receive world messages from
                        // monsters etc. then.
                        ClientScene.Ready(NetworkClient.connection);

                        // send CharacterSelect message (need to be ready first!)
                        NetworkClient.connection.Send(new CharacterSelectMsg{ value=icopy });

                        // make sure we can't select twice and call AddPlayer twice
                        panel.SetActive(false);
                    });

                    // delete button: sends delete message
                    slot.deleteButton.onClick.SetListener(() => {
                        uiConfirmation.Show(
                            "Do you really want to delete <b>" + characters[icopy].name + "</b>?",
                            () => { NetworkClient.Send(new CharacterDeleteMsg{value=icopy}); }
                        );
                    });
                }

                createButton.interactable = characters.Length < manager.characterLimit;
                createButton.onClick.SetListener(() => {
                    panel.SetActive(false);
                    uiCharacterCreation.Show();
                });

                quitButton.onClick.SetListener(() => { NetworkManagerSurvival.Quit(); });
            }
        }
        else panel.SetActive(false);
    }
}
