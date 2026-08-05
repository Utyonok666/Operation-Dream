using System.Collections;
using TMPro;
using UnityEngine;
using Mirror;

// ============================================================
// RoomCodeDisplay
// Shows the room code in the player HUD, copies it to clipboard.
// Показывает код комнаты в HUD игрока, умеет копировать в буфер.
// ============================================================
public class RoomCodeDisplay : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text copyButtonText; // Text INSIDE the copy button // Текст ВНУТРИ кнопки копировать

    // Mirror callback: fires only for the LOCAL player's own object
    // Колбэк Mirror: срабатывает только для СВОЕГО игрока
    public override void OnStartLocalPlayer()
    {
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        if (roomCodeText != null)
        {
            string code = MainMenuController.CurrentRoomCode;

            if (string.IsNullOrEmpty(code))
                code = "LOCAL"; // Fallback if launched without menu flow // Заглушка, если запущено без меню

            roomCodeText.text = $"Code: {code}";
        }
    }

    // Wired to the copy button's OnClick() // Привязано к OnClick() кнопки
    public void CopyCodeToClipboard()
    {
        string code = MainMenuController.CurrentRoomCode;
        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code; // Write to OS clipboard // Пишем в системный буфер обмена
            Debug.Log($"[HUD] Copied to clipboard: {code}");

            if (copyButtonText != null)
            {
                StopAllCoroutines(); // Prevent overlap on rapid clicks // Защита от нескольких быстрых кликов
                StartCoroutine(ShowCopiedFeedbackRoutine());
            }
        }
    }

    // Shows "Copied!" for 1.5s then restores original text
    // Показывает "Copied!" на 1.5 сек, потом возвращает исходный текст
    private IEnumerator ShowCopiedFeedbackRoutine()
    {
        string originalText = copyButtonText.text;
        copyButtonText.text = "Copied!";
        yield return new WaitForSeconds(1.5f);
        copyButtonText.text = originalText;
    }
}