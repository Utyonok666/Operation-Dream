using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponADS : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform aimPoint;
    [SerializeField] private WeaponController weaponController;

    [Header("Settings")]
    [SerializeField] private float adsSpeed = 10f;
    [SerializeField] private float baseFOV = 75f; // Добавь эту строку

    private Vector3 idlePosition;
    private Quaternion idleRotation;

    private Vector3 adsPosition;
    private Quaternion adsRotation;

    private void Start()
    {
        idlePosition = transform.localPosition;
        idleRotation = transform.localRotation;

        CalculateADSPosition();
    }

    private void CalculateADSPosition()
    {
        // Насколько AimPoint смещён относительно WeaponRoot
        Vector3 offset = transform.position - aimPoint.position;

        // Куда нужно поставить WeaponRoot,
        // чтобы AimPoint оказался в центре камеры
        adsPosition = transform.parent.InverseTransformPoint(
            playerCamera.transform.position + offset);

        adsRotation = idleRotation;
    }

    private void Update()
    {
        if (weaponController == null || weaponController.WeaponSettings == null)
            return;

        bool ads = Mouse.current.rightButton.isPressed;

        Vector3 targetPos = ads ? adsPosition : idlePosition;
        Quaternion targetRot = ads ? adsRotation : idleRotation;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            adsSpeed * Time.deltaTime);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot,
            adsSpeed * Time.deltaTime);

        float targetFOV = ads
            ? weaponController.WeaponSettings.adsFOV
            : baseFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            adsSpeed * Time.deltaTime);

    if (Time.frameCount % 60 == 0) 
        {
            // Теперь лог покажет ИМЯ файла, который сейчас в руках
            Debug.Log($"Файл: {weaponController.WeaponSettings.name} | Текущий FOV: {playerCamera.fieldOfView} | Целевой: {targetFOV}");
        }
    }
}