using System;
using Mirror;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) StopGame();
    }

    public void StopGame()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopHost();
        }
        else if (NetworkClient.isConnected)
        {
            NetworkManager.singleton.StopClient();
        }
        else if  (NetworkServer.active)
        {
            NetworkManager.singleton.StopServer();
        }
    }
}
