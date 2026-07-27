using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuPanel; // Панель меню паузы

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Проверяем нажатие ESC (подходит под новый InputSystem)
        bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (escPressed)
        {
            // Нажимать ESC имеет смысл только если мы зашли в игру/матч
            if (NetworkClient.active || NetworkServer.active)
            {
                TogglePause();
            }
        }
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(IsPaused);

        if (IsPaused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // Кнопка "Продолжить"
    public void OnResumeClicked()
    {
        if (IsPaused)
            TogglePause();
    }

    // Кнопка "Настройки"
    public void OnSettingsClicked()
    {
        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.OnSettingsClicked();
        }
    }

    // Кнопка "Выйти в главное меню"
    public void OnLeaveMatchClicked()
    {
        IsPaused = false;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        // Отключаемся от сервера / хоста Mirror
        if (NetworkManager.singleton != null)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopClient();
            }
        }

        // Показываем Главное Меню
        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.ShowMenu();
        }
    }
}