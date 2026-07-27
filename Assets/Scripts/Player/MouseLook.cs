using UnityEngine;
using Mirror;

public class MouseLook : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerBody; // Ссылка на сам объект Player
    [SerializeField] private Camera playerCamera; // Ссылка на камеру (для считывания FOV)
    [SerializeField] private NetworkLook networkLook;

    [Header("Fallback Settings")]
    [SerializeField] private float defaultSensitivity = 100f; // На случай, если GameSettings нет на сцене

    private float _xRotation = 0f;

    private void Start()
    {
        // Если камера не перетащена в инспекторе — пробуем взять с этого же объекта
        if (playerCamera == null)
            playerCamera = GetComponent<Camera>();
    }

    // Mirror вызывает это ТОЛЬКО на объекте, которым управляет именно этот клиент
    public override void OnStartLocalPlayer()
    {
        // Курсор блокируем только у своего игрока
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void Rotate(Vector2 lookInput)
    {
        if (!isLocalPlayer)
            return;

            // ЕСЛИ ОТКРЫТО МЕНЮ ПАУЗЫ — ЗАПРЕЩАЕМ ВРАЩЕНИЕ КАМЕРЫ
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
            return;

        // 1. Находим компонент прицеливания у активного оружия
        WeaponADS currentADS = GetComponentInChildren<WeaponADS>();
        bool isAiming = currentADS != null && currentADS.IsAiming;

        // 2. Получаем текущий FOV камеры
        float currentFOV = playerCamera != null ? playerCamera.fieldOfView : 75f;

        // 3. Берем динамическую чувствительность из GameSettings
        float activeSensitivity = defaultSensitivity;

        if (GameSettings.Instance != null)
        {
            activeSensitivity = GameSettings.Instance.GetCurrentSensitivity(isAiming, currentFOV, baseFOV: 75f);
        }

        // 4. Расчёт поворота с учётом умной сенсы
        float mouseX = lookInput.x * activeSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * activeSensitivity * Time.deltaTime;

        // Вращаем камеру вверх-вниз (ось X)
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
        
        if (networkLook != null)
            networkLook.SetLook(_xRotation);

        transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        // Вращаем тело игрока влево-вправо (ось Y)
        if (playerBody != null)
            playerBody.Rotate(Vector3.up * mouseX);
    }
}