using System.Net;
using System.Net.Sockets;
using Mirror;
using TMPro;
using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TMP_InputField joinIpInputField; // Инпут ТОЛЬКО для подключения

    [Header("Settings")]
    [SerializeField] private string defaultAddress = "localhost";

    // Глобальный код, который заберет HUD игрока
    public static string CurrentRoomCode { get; private set; } = "";

    private void Awake()
    {
        Instance = this;

        if (joinIpInputField != null && string.IsNullOrEmpty(joinIpInputField.text))
            joinIpInputField.text = defaultAddress;

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

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void HideMenu()
    {
        if (menuRoot != null) menuRoot.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // КНОПКА "HOST" (Создание) -> Генерирует случайный код комнаты
    public void OnHostClicked()
    {
        if (NetworkManager.singleton == null)
            return;

        // Генерируем случайный код комнаты (напр. ROOM-7B2X)
        CurrentRoomCode = GenerateRandomRoomCode();

        // Для работы локальной сети Mirror подтягивает IP
        NetworkManager.singleton.networkAddress = GetLocalIPAddress();

        HideMenu();
        NetworkManager.singleton.StartHost();
    }

    // КНОПКА "JOIN" (Подключение) -> Берет код/IP из поля ввода
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
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void OnCloseSettingsClicked()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OnQuitClicked()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // Генератор случайных рандомных кодов комнаты (без путающихся букв O/0, I/1)
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

    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch { }

        return "127.0.0.1";
    }
}