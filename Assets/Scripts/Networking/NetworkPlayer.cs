using Mirror;
using UnityEngine;

// ============================================================
// NetworkPlayer
// Toggles local player components (camera, audio listener, movement, HUD) based on ownership.
// Включает/отключает компоненты локального игрока (камеру, слушатель звука, движение, HUD) по правам владения.
// Prevents input bleeding, split-screen sound mixing, and multi-camera rendering conflicts.
// Предотвращает конфликт ввода, наложение звуков от нескольких игроков и спам камер на сцене.
// ============================================================
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private MouseLook mouseLook;
    [SerializeField] private PlayerHUD playerHUD;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;

    // Called on local client when network authority is assigned over this player prefab
    // Вызывается на локальном клиенте при получении авторитета (прав владения) над объектом
    public override void OnStartAuthority()
    {
        EnableLocalPlayer(true);
    }

    // Called when remote player representation initializes on other clients
    // Вызывается при инициализации объекта чужого игрока на удаленных клиентах
    public override void OnStartClient()
    {
        // Disable controls, cameras, and UI if this client doesn't own this player instance
        // Отключаем управление, камеру и интерфейс, если объект принадлежит чужому игроку
        if (!isOwned)
            EnableLocalPlayer(false);
    }

    // Centralizes enabling/disabling of local-only components
    // Централизованно включает или отключает компоненты, предназначенные только для локального игрока
    private void EnableLocalPlayer(bool value)
    {
        playerMovement.enabled = value;
        mouseLook.enabled = value;

        if (playerHUD != null)
            playerHUD.gameObject.SetActive(value);

        if (playerCamera != null)
            playerCamera.enabled = value;

        if (audioListener != null)
            audioListener.enabled = value;
    }
}