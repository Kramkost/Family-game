using UnityEngine;
using UnityEngine.UI;
using Mirror;

/// <summary>
/// Простой и надежный контроллер главного меню.
/// Связывает UI-кнопки с логикой запуска сервера/клиента в Mirror.
/// </summary>
public class MainMenuNetworkUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Главная панель меню, которая скроется после входа в игру.")]
    [SerializeField] private GameObject menuPanel;
    
    [Tooltip("Поле для ввода IP-адреса (нужно только для подключения к чужому серверу).")]
    [SerializeField] private InputField ipAddressInput;

    // Локальная ссылка на синглтон Mirror для быстрого доступа
    private NetworkManager networkManager;

    private void Start()
    {
        // Кэшируем менеджер при старте. Он должен быть в сцене!
        networkManager = NetworkManager.singleton;
        
        // Ставим localhost по умолчанию, чтобы было быстрее тестировать
        if (ipAddressInput != null)
        {
            ipAddressInput.text = "localhost";
        }
    }

    /// <summary>
    /// Запускает игру в режиме Хоста (вы одновременно и Сервер, и Клиент).
    /// Идеально для создателя лобби. Вызывается по клику на кнопку "Создать игру".
    /// </summary>
    public void StartHostGame()
    {
        // Проверяем, что мы еще никуда не подключены
        if (!NetworkClient.active && !NetworkServer.active)
        {
            networkManager.StartHost();
            HideMenu();
        }
    }

    /// <summary>
    /// Подключается к уже созданной игре по указанному IP.
    /// Вызывается по клику на кнопку "Подключиться".
    /// </summary>
    public void JoinGame()
    {
        if (!NetworkClient.active && !NetworkServer.active)
        {
            // Обновляем IP адрес в менеджере перед подключением
            if (ipAddressInput != null && !string.IsNullOrEmpty(ipAddressInput.text))
            {
                networkManager.networkAddress = ipAddressInput.text;
            }
            
            networkManager.StartClient();
            HideMenu();
        }
    }

    /// <summary>
    /// Прячет UI после успешного старта сессии.
    /// </summary>
    private void HideMenu()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
    }
}