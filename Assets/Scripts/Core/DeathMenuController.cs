using Mirror;
using UnityEngine;

/// <summary>
/// Управляет UI экрана смерти: Respawn / ChangeLoadout (+подменю классов) / LeavMatch.
/// Показ/скрытие вызывается снаружи из PlayerDeath (ShowDeathMenu / HideDeathMenu).
/// </summary>
public class DeathMenuController : MonoBehaviour
{
    // ==================== REFERENCES ====================

    [Header("References")]
    [SerializeField] private PlayerDeath playerDeath;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Submenu")]
    [Tooltip("Объект 'ChangeLodautMenu' - подменю с кнопками AssaultButton/SMGButton/ShotgunButton/SniperButton")]
    [SerializeField] private GameObject changeLoadoutMenu;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.35f;

    // ==================== STATE ====================

    private float targetAlpha;
    private float currentAlpha;

    // -1 = респавниться с тем классом, что уже был выбран/использован ранее.
    // 0..3 = игрок явно выбрал класс в ChangeLodautMenu перед респавном.
    private int pendingLoadoutIndex = -1;

    // ==================== LIFECYCLE ====================

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (playerDeath == null)
            playerDeath = GetComponentInParent<PlayerDeath>();

        currentAlpha = 0f;
        targetAlpha = 0f;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        ApplyCanvasState(interactable: false);
        SetSubmenuOpen(false);
    }

    private void Update()
    {
        if (Mathf.Abs(currentAlpha - targetAlpha) < 0.001f)
            return;

        float step = Time.deltaTime / fadeDuration;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, step);

        if (canvasGroup != null)
            canvasGroup.alpha = currentAlpha;
    }

    // ==================== ПОКАЗ / СКРЫТИЕ (вызывается из PlayerDeath) ====================

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

        pendingLoadoutIndex = -1;
    }

    private void ApplyCanvasState(bool interactable)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.blocksRaycasts = interactable;
        canvasGroup.interactable = interactable;
    }

    private void SetSubmenuOpen(bool open)
    {
        if (changeLoadoutMenu != null)
            changeLoadoutMenu.SetActive(open);
    }

    // ==================== КНОПКИ: ОСНОВНОЕ МЕНЮ ====================

    // Кнопка "Respawn"
    public void OnRespawnClicked()
    {
        if (playerDeath != null)
            playerDeath.CmdRequestRespawn(pendingLoadoutIndex);
    }

    // Кнопка "ChangeLoadout" - открывает/закрывает подменю с классами оружия
    public void OnChangeLoadoutClicked()
    {
        if (changeLoadoutMenu == null)
            return;

        SetSubmenuOpen(!changeLoadoutMenu.activeSelf);
    }

    // Кнопка "LeavMatch" - выход из матча
    public void OnLeaveMatchClicked()
    {
        if (NetworkServer.active && NetworkClient.active)
        {
            // Мы хост (сервер + клиент в одном) - останавливаем всё.
            NetworkManager.singleton.StopHost();
        }
        else if (NetworkClient.active)
        {
            // Мы обычный клиент.
            NetworkManager.singleton.StopClient();
        }

        // Если меню и игра в одной сцене (Offline/Online Scene в NetworkManager
        // не заданы) - показываем Canvas меню обратно. Если сцены разделены,
        // Mirror сам перекинет на Offline Scene, и MainMenuController.Instance
        // там просто пересоздастся сам - этот вызов тогда безвреден (null-check).
        if (MainMenuController.Instance != null)
            MainMenuController.Instance.ShowMenu();
    }

    // ==================== КНОПКИ: ПОДМЕНЮ ChangeLodautMenu ====================
    // Тут НЕ переключаем оружие сразу через WeaponSwitcher - иначе оно "проявится"
    // в руках у трупа, пока игрок ещё не заспавнился. Просто запоминаем выбор,
    // а PlayerDeath применит его через CmdRequestRespawn(pendingLoadoutIndex).

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