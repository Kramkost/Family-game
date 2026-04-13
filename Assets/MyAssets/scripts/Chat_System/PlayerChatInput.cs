using System.Collections;
using UnityEngine;
using Mirror;
using TMPro;
using UnityEngine.InputSystem;
// =========================================================================
    // 3. ВВОД ЧАТА (New Input System Event-Driven)
    // =========================================================================
    [RequireComponent(typeof(PlayerChatNetwork))]
    public class PlayerChatInput : NetworkBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField chatInputField;
        
        [Header("Input Controls (New Input System)")]
        [Tooltip("Кнопка открытия чата. По умолчанию 'T'")]
        [SerializeField] private InputAction openChatAction = new InputAction("OpenChat", binding: "<Keyboard>/t");
        [Tooltip("Кнопка отправки сообщения. По умолчанию 'Enter'")]
        [SerializeField] private InputAction sendChatAction = new InputAction("SendChat", binding: "<Keyboard>/enter");

        [Header("Debug")]
        [SerializeField] private bool showDebug;

        private PlayerChatNetwork _network;

        private void Awake()
        {
            _network = GetComponent<PlayerChatNetwork>();
        }

        public override void OnStartLocalPlayer()
        {
            if (chatInputField == null)
            {
                var uiObject = GameObject.FindWithTag("ChatInput");
                if (uiObject != null) chatInputField = uiObject.GetComponent<TMP_InputField>();
            }

            if (chatInputField != null)
            {
                chatInputField.gameObject.SetActive(false);
            }

            // Включаем экшены только для локального игрока
            openChatAction.Enable();
            sendChatAction.Enable();

            // Подписываемся на события нажатий
            openChatAction.performed += OnOpenChatPerformed;
            sendChatAction.performed += OnSendChatPerformed;
        }

        public override void OnStopLocalPlayer()
        {
            // Важно: отписываемся и выключаем экшены при уничтожении/отключении игрока
            openChatAction.performed -= OnOpenChatPerformed;
            sendChatAction.performed -= OnSendChatPerformed;
            
            openChatAction.Disable();
            sendChatAction.Disable();
        }

        private void OnOpenChatPerformed(InputAction.CallbackContext context)
        {
            if (chatInputField == null) return;

            // Если чат закрыт - открываем
            if (!chatInputField.gameObject.activeSelf)
            {
                OpenChat();
            }
        }

        private void OnSendChatPerformed(InputAction.CallbackContext context)
        {
            if (chatInputField == null) return;

            // Если чат открыт - отправляем
            if (chatInputField.gameObject.activeSelf)
            {
                SendMessage();
            }
        }

        private void OpenChat()
        {
            chatInputField.gameObject.SetActive(true);
            chatInputField.ActivateInputField();
            chatInputField.text = "";
            if (showDebug) Debug.Log("[Client] Opened chat input.");
        }

        private void SendMessage()
        {
            string msg = chatInputField.text.Trim();
            if (!string.IsNullOrEmpty(msg))
            {
                _network.CmdSendMessage(msg);
            }
            
            chatInputField.text = "";
            chatInputField.DeactivateInputField();
            chatInputField.gameObject.SetActive(false);
            
            if (showDebug) Debug.Log("[Client] Chat input closed.");
        }
    }