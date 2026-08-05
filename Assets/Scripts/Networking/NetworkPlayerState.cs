using Mirror;
using UnityEngine;

// ============================================================
// NetworkPlayerState
// Syncs player posture states (e.g. crouching) across the network using Mirror.
// Синхронизирует состояния игрока (например, приседание) по сети через Mirror.
// Notifies remote PlayerMovement components to visually align remote character models.
// Уведомляет компонент PlayerMovement удаленных игроков для визуальной синхронизации мобов.
// ============================================================
public class NetworkPlayerState : NetworkBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;

    // Network-synced crouching state; triggers hook on remote clients when updated
    // Синхронизируемый по сети статус приседания; вызывает хук на сторонних клиентах при изменении
    [SyncVar(hook = nameof(OnCrouchChanged))]
    private bool isCrouching;

    public override void OnStartClient()
    {
        Debug.Log("NetworkPlayerState started. ID: " + netId);
    }

    // Call from local input handlers to request crouch state synchronization
    // Вызывать из локального контроллера ввода для запроса синхронизации приседания
    public void SetCrouch(bool state)
    {
        if (!isLocalPlayer)
            return;

        Debug.Log("Sending crouch: " + state);

        CmdSetCrouch(state);
    }

    // Sends local client crouch state request to the authoritative server
    // Передает статус приседания с локального клиента на авторитетный сервер
    [Command]
    private void CmdSetCrouch(bool state)
    {
        Debug.Log("Server received crouch: " + state);

        isCrouching = state;
    }

    // Mirror hook triggered when isCrouching SyncVar changes across the network
    // Хук Mirror, вызываемый при изменении значения SyncVar isCrouching по сети
    private void OnCrouchChanged(bool oldValue, bool newValue)
    {
        Debug.Log("Crouch sync received: " + newValue);

        // Local player already handled crouch state locally; skip to prevent input conflict
        // Локальный игрок уже обработал приседание у себя, пропускаем во избежание конфликта ввода
        if (isLocalPlayer)
            return;

        // Apply synced crouch visual state on remote client representations
        // Применяем синхронизированное состояние приседания для отображения на сторонних клиентах
        if (playerMovement != null)
            playerMovement.SetNetworkCrouch(newValue);
    }
}