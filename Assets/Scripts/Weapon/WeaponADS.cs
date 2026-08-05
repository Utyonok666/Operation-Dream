using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

// ============================================================
// WeaponADS
// Aim Down Sights (ADS) system handler for networked weapon controllers.
// Управление режимом прицеливания (ADS) для сетевого контроллера оружия.
// Dynamically calculates align offsets relative to camera and dynamically interpolates FOV.
// Динамически рассчитывает выравнивание прицела относительно камеры и интерполирует FOV.
// ============================================================
public class WeaponADS : MonoBehaviour
{
    [Header("Networking")]
    [SerializeField] private NetworkIdentity networkIdentity;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform aimPoint;
    [SerializeField] private WeaponController weaponController;

    [Header("Settings")]
    [SerializeField] private float adsSpeed = 10f;
    [SerializeField] private float baseFOV = 75f;

    private Vector3 idlePosition;
    private Quaternion idleRotation;

    private Vector3 adsPosition;
    private Quaternion adsRotation;

    // Public aiming state flag queried by mouse look and sensitivity processors
    // Публичный флаг состояния прицеливания для мыши и расчетов чувствительности
    public bool IsAiming { get; private set; }

    private void Start()
    {
        // Auto-resolve missing NetworkIdentity reference in parent hierarchy
        // Автоматический поиск отсутствующего компонента NetworkIdentity в родителе
        if (networkIdentity == null)
            networkIdentity = GetComponentInParent<NetworkIdentity>();

        // Cache baseline hip-fire position and rotation transforms
        // Сохраняем базовые локальные координаты и поворот в режиме от бедра (Hip-Fire)
        idlePosition = transform.localPosition;
        idleRotation = transform.localRotation;

        CalculateADSPosition();
    }

    // Calculates precise local offset required to align weapon aim point directly with player camera
    // Вычисляет смещение оружия, чтобы точка прицеливания (AimPoint) стала ровно по центру камеры
    private void CalculateADSPosition()
    {
        if (aimPoint == null || playerCamera == null) return;

        // Vector displacement from weapon sight center to camera origin
        // Вектор смещения от центра прицела оружия до позиции камеры
        Vector3 offset = transform.position - aimPoint.position;

        // Convert world camera position with offset into local space of weapon parent transform
        // Перевод координат позиции камеры со смещением в локальное пространство родителя
        adsPosition = transform.parent.InverseTransformPoint(
            playerCamera.transform.position + offset);

        adsRotation = idleRotation;
    }

    private void Update()
    {
        // Freeze ADS interpolation when pause menu overlay is active
        // Блокировка движения оружия при активном меню паузы
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
            return;

        // Ensure required weapon components and data definitions are valid
        // Проверка наличия контроллера оружия и файла настроек
        if (weaponController == null || weaponController.WeaponSettings == null)
            return;

        // Restrict execution to local authority on network entity
        // Проверка локального владения сетевым объектом (выполняем только для владельца)
        if (networkIdentity != null && !networkIdentity.isOwned)
            return;

        // Poll RMB hold status from Unity Input System
        // Считывание удержания правой кнопки мыши из New Input System
        IsAiming = Mouse.current.rightButton.isPressed;

        // Determine target position and rotation depending on aim state
        // Определение целевой позиции и поворота в зависимости от режима прицеливания
        Vector3 targetPos = IsAiming ? adsPosition : idlePosition;
        Quaternion targetRot = IsAiming ? adsRotation : idleRotation;

        // Lerp local position towards active mode offset
        // Плавная интерполяция локальной позиции к целевой
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            adsSpeed * Time.deltaTime);

        // Slerp local rotation towards target orientation
        // Плавная сферическая интерполяция поворота
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot,
            adsSpeed * Time.deltaTime);

        // Determine field of view based on active weapon scriptable object settings
        // Определение целевого угла обзора (FOV) из настроек оружия в ScriptableObject
        float targetFOV = IsAiming
            ? weaponController.WeaponSettings.adsFOV
            : baseFOV;

        // Interpolate camera FOV for zoom effect
        // Плавное изменение FOV камеры для эффекта приближения
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            adsSpeed * Time.deltaTime);
    }
}