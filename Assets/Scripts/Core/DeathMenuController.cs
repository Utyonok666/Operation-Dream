using Mirror;
using UnityEngine;

// ============================================================
// DeathMenuController
// Death-screen UI: Respawn / ChangeLoadout / LeaveMatch.
// UI экрана смерти: Respawn / ChangeLoadout / LeaveMatch.
// Shown/hidden externally by PlayerDeath — this only handles visuals + input.
// Показ/скрытие вызывается извне из PlayerDeath — тут только визуал + ввод.
// ============================================================
public class DeathMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerDeath playerDeath;
    [SerializeField] private CanvasGroup canvasGroup; // Controls fade + click-blocking at once // Контролирует прозрачность + блок кликов разом

    [Header("Submenu")]
    [Tooltip("Объект 'ChangeLodautMenu' - подменю с кнопками AssaultButton/SMGButton/ShotgunButton/SniperButton")]
    [SerializeField] private GameObject changeLoadoutMenu;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.35f;

    private float targetAlpha;   // Where alpha is animating to // Куда стремится прозрачность
    private float currentAlpha;  // Current alpha this frame // Текущая прозрачность в этом кадре

    // -1 = respawn with last used class. 0..3 = explicit class chosen before respawn.
    // -1 = респавн с прошлым классом. 0..3 = класс выбран явно перед респавном.
    private int pendingLoadoutIndex = -1;

    private void Awake()
    {
        // Fallback lookups if not wired in Inspector // Автопоиск, если не назначено в инспекторе
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (playerDeath == null)
            playerDeath = GetComponentInParent<PlayerDeath>();

        currentAlpha = 0f;
        targetAlpha = 0f;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        ApplyCanvasState(interactable: false); // Hidden and non-clickable at start // Скрыто и некликабельно на старте
        SetSubmenuOpen(false);
    }

    private void Update()
    {
        if (Mathf.Abs(currentAlpha - targetAlpha) < 0.001f)
            return; // Already at target, skip work // Уже у цели, пропускаем работу

        // Framerate-independent fade over fadeDuration seconds
        // Затухание/появление за fadeDuration сек, независимо от FPS
        float step = Time.deltaTime / fadeDuration;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, step);

        if (canvasGroup != null)
            canvasGroup.alpha = currentAlpha;
    }

    // Called externally by PlayerDeath // Вызывается извне из PlayerDeath
    public void ShowDeathMenu()
    {
        targetAlpha = 1f;
        ApplyCanvasState(interactable: true);
        SetSubmenuOpen(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void HideDeathMenu()
    {
        targetAlpha = 0f;
        ApplyCanvasState(interactable: false);
        SetSubmenuOpen(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        pendingLoadoutIndex = -1; // Reset so it doesn't reapply on next death // Сброс, чтоб не сработало при следующей смерти
    }

    private void ApplyCanvasState(bool interactable)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.blocksRaycasts = interactable; // Blocks mouse clicks // Перехватывает клики мыши
        canvasGroup.interactable = interactable;    // Buttons respond or not // Кнопки активны или нет
    }

    private void SetSubmenuOpen(bool open)
    {
        if (changeLoadoutMenu != null)
            changeLoadoutMenu.SetActive(open);
    }

    // Respawn button // Кнопка Respawn
    public void OnRespawnClicked()
    {
        // Command: called from client, executes on server (authoritative)
        // Command: вызывается с клиента, выполняется на сервере (авторитетно)
        if (playerDeath != null)
            playerDeath.CmdRequestRespawn(pendingLoadoutIndex);
    }

    // ChangeLoadout button - opens/closes weapon-class submenu // Кнопка ChangeLoadout - открывает/закрывает подменю
    public void OnChangeLoadoutClicked()
    {
        if (changeLoadoutMenu == null)
            return;

        SetSubmenuOpen(!changeLoadoutMenu.activeSelf); // Simple toggle // Простой переключатель
    }

    // LeaveMatch button // Кнопка LeaveMatch
    public void OnLeaveMatchClicked()
    {
        if (NetworkServer.active && NetworkClient.active)
        {
            NetworkManager.singleton.StopHost(); // We're host: stop everything // Мы хост: останавливаем всё
        }
        else if (NetworkClient.active)
        {
            NetworkManager.singleton.StopClient(); // We're a plain client // Мы обычный клиент
        }

        // Manual menu display if scenes aren't split; harmless no-op otherwise
        // Ручной показ меню, если сцены не разделены; иначе безвредный no-op
        if (MainMenuController.Instance != null)
            MainMenuController.Instance.ShowMenu();
    }

    // Submenu buttons: only remember choice, don't switch weapon yet
    // Кнопки подменю: только запоминаем выбор, оружие пока не переключаем
    // (avoids weapon "popping" into a corpse's hands before respawn)
    // (иначе оружие "проявилось" бы в руках трупа до респавна)
    public void OnAssaultSelected() => SelectLoadout(0);
    public void OnSMGSelected() => SelectLoadout(1);
    public void OnShotgunSelected() => SelectLoadout(2);
    public void OnSniperSelected() => SelectLoadout(3);

    private void SelectLoadout(int classIndex)
    {
        pendingLoadoutIndex = classIndex;
        SetSubmenuOpen(false);
    }
}