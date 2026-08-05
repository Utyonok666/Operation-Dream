using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

// ============================================================
// PauseMenuController
// In-match pause menu (ESC). Purely a local UI overlay.
// Меню паузы во время матча (ESC). Просто локальный UI-оверлей.
// NOTE: doesn't itself block weapons/movement — other scripts must check IsPaused.
// ВАЖНО: сам не блокирует оружие/движение — другие скрипты сами должны проверять IsPaused.
// ============================================================
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuPanel;

    public bool IsPaused { get; private set; } // Other scripts read this in Update() // Другие скрипты читают это в Update()

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
        // New Input System check for ESC // Проверка ESC через новый Input System
        bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (escPressed)
        {
            // Only matters if we're actually in a match // Имеет смысл только если мы в матче
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

        // Standard FPS cursor pattern: locked during gameplay, free during UI
        // Стандартный паттерн для FPS: заблокирован в игре, свободен в UI
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

    public void OnResumeClicked()
    {
        if (IsPaused)
            TogglePause();
    }

    public void OnSettingsClicked()
    {
        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.OnSettingsClicked(); // Reuse main menu's settings logic // Переиспользуем логику настроек из главного меню
        }
    }

    public void OnLeaveMatchClicked()
    {
        IsPaused = false;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        // Host = stop everything, Client = disconnect only ourselves
        // Хост = останавливаем всё, Клиент = отключаем только себя
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

        if (MainMenuController.Instance != null)
        {
            MainMenuController.Instance.ShowMenu();
        }
    }
}