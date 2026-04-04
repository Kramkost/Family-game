using System.Collections;
using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.InputSystem;
// =========================================================================
    // 1. СЕТЕВОЙ МЕНЕДЖЕР (Network Routing & Server Validation)
    // =========================================================================
    [RequireComponent(typeof(NetworkIdentity))]
    public class PlayerChatNetwork : NetworkBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int maxMessageLength = 100;

        [Header("Debug")]
        [SerializeField] private bool showDebug;

        public System.Action<string> OnMessageReceived;

        [Command]
        public void CmdSendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            string sanitizedMessage = message.Length > maxMessageLength 
                ? message.Substring(0, maxMessageLength) 
                : message;

            if (showDebug) Debug.Log($"[Server] Message approved from {netId}: {sanitizedMessage}");

            RpcDisplayMessage(sanitizedMessage);
        }

        [ClientRpc]
        private void RpcDisplayMessage(string message)
        {
            OnMessageReceived?.Invoke(message);
        }
    }