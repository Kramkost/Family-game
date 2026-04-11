using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using TMPro;

public class LobbyDataEntry : MonoBehaviour
{
    //Data
    public CSteamID lobbyID;
    public string lobbyName;
    public TMP_Text lobbyNameText;

    public void SetLobbyData()
    {
        if (lobbyName == "")
        {
            lobbyNameText.text = "Empty";
        }
        else
        {
            lobbyNameText.text = lobbyName;
        }
    }

    public void JoinLobby()
    {
        SteamLobby.JoinLobby(lobbyID);
    }
}
