using UnityEngine;
using Mirror;

// ============================================================
// MouseLook
// Handles FPS first-person mouse camera rotation and body aiming logic.
// Управляет вращением камеры от первого лица (FPS) и поворотом тела игрока.
// Dynamically adjusts sensitivity based on aiming state (ADS) and current FOV.
// Динамически масштабирует чувствительность при прицеливании (ADS) и изменении FOV.
// ============================================================
public class MouseLook : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerBody; // Transform of the main player root object // Ссылка на сам объект Player
    [SerializeField] private Camera playerCamera; // First-person camera reference for FOV querying // Ссылка на камеру (для считывания FOV)
    [SerializeField] private NetworkLook networkLook;

    [Header("Fallback Settings")]
    [SerializeField] private float defaultSensitivity = 100f; // Fallback sensitivity if GameSettings singleton is absent // На случай, если GameSettings нет на сцене

    private float _xRotation = 0f;

    private void Start()
    {
        // Fallback: search for local Camera component if unassigned in Inspector
        // Если камера не перетащена в инспекторе — пробуем взять с этого же объекта
        if (playerCamera == null)
            playerCamera = GetComponent<Camera>();
    }

    // Mirror lifecycle hook called ONLY on the client controlling this specific player instance
    // Mirror вызывает это ТОЛЬКО на объекте, которым управляет именно этот клиент
    public override void OnStartLocalPlayer()
    {
        // Lock and hide system cursor for local player only
        // Курсор блокируем только у своего игрока
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Rotate(Vector2 lookInput)
    {
        if (!isLocalPlayer)
            return;

        // Block camera rotation processing when pause menu is active
        // Если открыто меню паузы — запрещаем вращение камеры
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
            return;

        // 1. Query aiming state (ADS) from currently equipped active weapon
        // 1. Находим компонент прицеливания у активного оружия
        WeaponADS currentADS = GetComponentInChildren<WeaponADS>();
        bool isAiming = currentADS != null && currentADS.IsAiming;

        // 2. Read current camera FOV value
        // 2. Получаем текущий FOV камеры
        float currentFOV = playerCamera != null ? playerCamera.fieldOfView : 75f;

        // 3. Obtain FOV-scaled dynamic sensitivity from central GameSettings
        // 3. Берем динамическую чувствительность из GameSettings
        float activeSensitivity = defaultSensitivity;

        if (GameSettings.Instance != null)
        {
            activeSensitivity = GameSettings.Instance.GetCurrentSensitivity(isAiming, currentFOV, baseFOV: 75f);
        }

        // 4. Calculate frame-rate independent rotation deltas using active sensitivity
        // 4. Расчёт поворота с учётом умной сенсы
        float mouseX = lookInput.x * activeSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * activeSensitivity * Time.deltaTime;

        // Pitch rotation (vertical look up/down clamped to 90 degrees)
        // Вращаем камеру вверх-вниз (ось X)
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
        
        // Broadcast vertical pitch angle to network representation
        // Передаем вертикальный угол наклона в сетевой компонент
        if (networkLook != null)
            networkLook.SetLook(_xRotation);

        transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Yaw rotation (horizontal look left/right rotates player body transform)
        // Вращаем тело игрока влево-вправо (ось Y)
        if (playerBody != null)
            playerBody.Rotate(Vector3.up * mouseX);
    }
}