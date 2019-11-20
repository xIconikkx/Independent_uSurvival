using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MenuSystem : MonoBehaviour
{
    public GameObject LoginPanel;
    public GameObject HomePanel;
    public GameObject CharacterPanel;
    public GameObject MulitplayerPanel;
    public GameObject ProfilePanel;
    public GameObject TopPanel;
    public GameObject BottomPanel;
    public GameObject FriendsPanel;
    public GameObject BlurEffect;


    private void Start()
    {
        SwitchPanel(1);
    }

    public void SwitchPanel(int panelID)
    {
        switch (panelID)
        {
            case 1: //Login Panel
                LoginPanel.SetActive(true);
                HomePanel.SetActive(false);
                CharacterPanel.SetActive(false);
                MulitplayerPanel.SetActive(false);
                ProfilePanel.SetActive(false);

                MiscUI(false);
                break;
            case 2: //Home Panel
                LoginPanel.SetActive(false);
                HomePanel.SetActive(true);
                CharacterPanel.SetActive(false);
                MulitplayerPanel.SetActive(false);
                ProfilePanel.SetActive(false);

                MiscUI(true);
                break;
            case 3: //CharacterPanel
                LoginPanel.SetActive(false);
                HomePanel.SetActive(false);
                CharacterPanel.SetActive(true);
                MulitplayerPanel.SetActive(false);
                ProfilePanel.SetActive(false);

                MiscUI(true);
                break;
            case 4: //Mulitplayer Panel
                LoginPanel.SetActive(false);
                HomePanel.SetActive(false);
                CharacterPanel.SetActive(false);
                MulitplayerPanel.SetActive(true);
                ProfilePanel.SetActive(false);

                MiscUI(true);
                break;
            case 5: //Profile Panel
                LoginPanel.SetActive(false);
                HomePanel.SetActive(false);
                CharacterPanel.SetActive(false);
                MulitplayerPanel.SetActive(false);
                ProfilePanel.SetActive(true);

                MiscUI(true);
                break;
        }
    }

    private void MiscUI(bool i)
    {
        TopPanel.SetActive(i);
        BottomPanel.SetActive(i);
        FriendsPanel.SetActive(i);
        BlurEffect.SetActive(i);
    }

    public void TestMessage(string i)
    {
        Debug.Log(i);
    }
}

