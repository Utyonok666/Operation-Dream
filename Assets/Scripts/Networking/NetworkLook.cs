using Mirror;
using UnityEngine;

// ============================================================
// NetworkLook
// Synchronizes camera vertical pitch (X rotation) across the network.
// Синхронизирует вертикальный наклон камеры (X-вращение) по сети.
// Allows remote players to visually see where other players are aiming up/down.
// Позволяет другим игрокам видеть, куда по вертикали смотрит/целится игрок.
// ============================================================
public class NetworkLook : NetworkBehaviour
{
    [SerializeField] private Transform cameraHolder;

    // Network-synced vertical camera pitch angle
    // Синхронизируемый по сети угол наклона камеры по вертикали
    [SyncVar]
    private float lookX;

    // Call from local player control scripts to update pitch on server
    // Вызывать из локального скрипта управления для обновления угла на сервере
    public void SetLook(float xRotation)
    {
        if (!isLocalPlayer)
            return;

        CmdSetLook(xRotation);
    }

    // Sends vertical camera angle from local client owner to server
    // Передает вертикальный угол наклона камеры с локального клиента на сервер
    [Command]
    private void CmdSetLook(float xRotation)
    {
        lookX = xRotation;
    }

    private void LateUpdate()
    {
        // Local player drives camera rotation directly via local inputs
        // Локальный игрок вращает камеру напрямую через ввод, пропуская сетевую задержку
        if (isLocalPlayer)
            return;

        // Apply synced pitch rotation to remote client representations
        // Применяем синхронизированный угол наклона для отображения у сторонних клиентов
        Vector3 angles = cameraHolder.localEulerAngles;
        angles.x = lookX;
        cameraHolder.localEulerAngles = angles;
    }
}