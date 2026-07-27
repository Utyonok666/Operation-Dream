using System.Collections;
using TMPro;
using UnityEngine;
using Mirror;

public class RoomCodeDisplay : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text copyButtonText; // Перетащи сюда текст ВНУТРИ кнопки скопировать

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
                code = "LOCAL";

            roomCodeText.text = $"Code: {code}";
        }
    }

    // Метод для OnClick() кнопки скопировать
    public void CopyCodeToClipboard()
    {
        string code = MainMenuController.CurrentRoomCode;
        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"[HUD] Copied to clipboard: {code}");

            if (copyButtonText != null)
            {
                StopAllCoroutines();
                StartCoroutine(ShowCopiedFeedbackRoutine());
            }
        }
    }

    private IEnumerator ShowCopiedFeedbackRoutine()
    {
        string originalText = copyButtonText.text;
        copyButtonText.text = "Copied!";
        yield return new WaitForSeconds(1.5f);
        copyButtonText.text = originalText;
    }
}