using System.Net;
using System.Net.Sockets;
using Mirror;
using TMPro;
using UnityEngine;

// ============================================================
// MainMenuController
// Main menu logic: Host / Join / Settings / Quit.
// Логика главного меню: Host / Join / Settings / Quit.
// Plain MonoBehaviour (not networked) — runs BEFORE any connection exists.
// Обычный MonoBehaviour (не сетевой) — работает ДО подключения к сети.
// ============================================================
public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TMP_InputField joinIpInputField; // Only used for Join // Только для подключения

    [Header("Settings")]
    [SerializeField] private string defaultAddress = "localhost";

    // Static so it survives scene load; read later by RoomCodeDisplay
    // Статик, чтобы пережить смену сцены; читается потом в RoomCodeDisplay
    public static string CurrentRoomCode { get; private set; } = "";

    private void Awake()
    {
        Instance = this;

        if (joinIpInputField != null && string.IsNullOrEmpty(joinIpInputField.text))
            joinIpInputField.text = defaultAddress; // Prefill only if empty // Заполняем, только если пусто

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowMenu()
    {
        if (menuRoot != null) menuRoot.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None; // Free cursor for menu clicks // Свободный курсор для кликов
        Cursor.visible = true;
    }

    public void HideMenu()
    {
        if (menuRoot != null) menuRoot.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // HOST button -> generates room code, starts hosting // Кнопка HOST -> генерит код, стартует хост
    public void OnHostClicked()
    {
        if (NetworkManager.singleton == null)
            return;

        CurrentRoomCode = GenerateRandomRoomCode(); // Cosmetic code, not a real address // Просто "красивый" код, не адрес

        NetworkManager.singleton.networkAddress = GetLocalIPAddress(); // Real LAN IP for connections // Реальный LAN IP для подключений

        HideMenu();
        NetworkManager.singleton.StartHost(); // Server + local client in one process // Сервер + клиент в одном процессе
    }

    // JOIN button -> connects using typed address // Кнопка JOIN -> подключается по введённому адресу
    public void OnJoinClicked()
    {
        if (NetworkManager.singleton == null)
            return;

        string address = joinIpInputField != null && !string.IsNullOrWhiteSpace(joinIpInputField.text)
            ? joinIpInputField.text.Trim()
            : defaultAddress;

        CurrentRoomCode = address;
        NetworkManager.singleton.networkAddress = address;

        HideMenu();
        NetworkManager.singleton.StartClient();
    }

    public void OnSettingsClicked()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf); // Simple toggle // Простой переключатель
    }

    public void OnCloseSettingsClicked()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OnQuitClicked()
    {
        Application.Quit(); // Works only in builds, not in Editor // Работает только в билде, не в редакторе

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Editor-only workaround // Обходной путь для редактора
#endif
    }

    // Random room code, avoids confusing chars like O/0, I/1
    // Случайный код комнаты без похожих символов O/0, I/1
    private string GenerateRandomRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[4];
        for (int i = 0; i < code.Length; i++)
        {
            code[i] = chars[Random.Range(0, chars.Length)];
        }
        return $"ROOM-{new string(code)}";
    }

    // Finds local IPv4 address for LAN play // Ищет локальный IPv4 адрес для игры по LAN
    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4 only // Только IPv4
                {
                    return ip.ToString();
                }
            }
        }
        catch { }

        return "127.0.0.1"; // Fallback if network lookup fails // Заглушка если не нашли адрес
    }
}