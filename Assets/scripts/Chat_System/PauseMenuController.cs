using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Подключаем New Input System

namespace MultiplayerGame.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(AudioSource))]
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private CanvasGroup mainCanvasGroup;
        [SerializeField] private GameObject rootMenuPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Buttons (Main)")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Buttons (Settings)")]
        [SerializeField] private Button closeSettingsButton;

        [Header("Settings Controls")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Slider optimizationSlider;

        [Header("Input (New Input System)")]
        [Tooltip("Кнопка вызова меню. По умолчанию 'Escape'")]
        [SerializeField] private InputAction pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");

        [Header("Game Feel : Audio")]
        [SerializeField] private AudioClip[] openMenuSounds;
        [SerializeField] private AudioClip[] clickSounds;
        [SerializeField] private AudioClip[] sliderChangeSounds;
        
        [Header("Game Feel : Animation")]
        [SerializeField] private float fadeSpeed = 10f;

        [Header("Debug")]
        [SerializeField] private bool showDebug;

        private AudioSource _audioSource;
        private Coroutine _fadeCoroutine;
        private bool _isMenuOpen = false;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            
            mainCanvasGroup.alpha = 0f;
            mainCanvasGroup.interactable = false;
            mainCanvasGroup.blocksRaycasts = false;
            
            rootMenuPanel.SetActive(true);
            settingsPanel.SetActive(false);

            BindUIEvents();
        }

        private void Start()
        {
            volumeSlider.value = AudioListener.volume;
        }

        private void OnEnable()
        {
            // Включаем прослушивание кнопки и подписываемся на событие
            pauseAction.Enable();
            pauseAction.performed += OnPausePerformed;
        }

        private void OnDisable()
        {
            // Отписываемся, чтобы избежать утечек памяти
            pauseAction.performed -= OnPausePerformed;
            pauseAction.Disable();
        }

        private void BindUIEvents()
        {
            resumeButton.onClick.AddListener(ToggleMenu);
            settingsButton.onClick.AddListener(OpenSettings);
            quitButton.onClick.AddListener(QuitGame);
            closeSettingsButton.onClick.AddListener(CloseSettings);
            
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            optimizationSlider.onValueChanged.AddListener(OnOptimizationChanged);
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            if (settingsPanel.activeSelf)
            {
                CloseSettings();
            }
            else
            {
                ToggleMenu();
            }
        }

        private void ToggleMenu()
        {
            _isMenuOpen = !_isMenuOpen;
            
            if (showDebug) Debug.Log($"[PauseMenu] Toggled. IsOpen: {_isMenuOpen}");

            if (_isMenuOpen)
            {
                PlayRandomSound(openMenuSounds);
                rootMenuPanel.SetActive(true);
                settingsPanel.SetActive(false);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                PlayRandomSound(clickSounds);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(_isMenuOpen ? 1f : 0f));
        }

        private void OpenSettings()
        {
            PlayRandomSound(clickSounds);
            rootMenuPanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        private void CloseSettings()
        {
            PlayRandomSound(clickSounds);
            settingsPanel.SetActive(false);
            rootMenuPanel.SetActive(true);
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            mainCanvasGroup.interactable = targetAlpha > 0.5f;
            mainCanvasGroup.blocksRaycasts = targetAlpha > 0.5f;

            while (!Mathf.Approximately(mainCanvasGroup.alpha, targetAlpha))
            {
                mainCanvasGroup.alpha = Mathf.MoveTowards(mainCanvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private void OnVolumeChanged(float value)
        {
            AudioListener.volume = value;
            
            if (_audioSource != null && !_audioSource.isPlaying) 
            {
                PlayRandomSound(sliderChangeSounds);
            }
        }

        private void OnOptimizationChanged(float value)
        {
            if (showDebug) Debug.Log($"[PauseMenu] Optimization intensity changed to: {value}");
        }

        private void QuitGame()
        {
            PlayRandomSound(clickSounds);
            
            if (Mirror.NetworkServer.active && Mirror.NetworkClient.isConnected)
                Mirror.NetworkManager.singleton.StopHost();
            else if (Mirror.NetworkClient.isConnected)
                Mirror.NetworkManager.singleton.StopClient();
            else
                Application.Quit();
        }

        private void PlayRandomSound(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            
            _audioSource.clip = clips[Random.Range(0, clips.Length)];
            _audioSource.pitch = Random.Range(0.9f, 1.1f);
            _audioSource.Play();
        }
    }
}