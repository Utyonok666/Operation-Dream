using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

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

    // Публичный флаг прицеливания для Mouse Look
    public bool IsAiming { get; private set; }

    private void Start()
    {
        if (networkIdentity == null)
            networkIdentity = GetComponentInParent<NetworkIdentity>();

        idlePosition = transform.localPosition;
        idleRotation = transform.localRotation;

        CalculateADSPosition();
    }

    private void CalculateADSPosition()
    {
        if (aimPoint == null || playerCamera == null) return;

        Vector3 offset = transform.position - aimPoint.position;

        adsPosition = transform.parent.InverseTransformPoint(
            playerCamera.transform.position + offset);

        adsRotation = idleRotation;
    }

    private void Update()
    {
        // Если игра на паузе — пушка не "елозит"
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
            return;

        if (weaponController == null || weaponController.WeaponSettings == null)
            return;

        if (networkIdentity != null && !networkIdentity.isOwned)
            return;

        IsAiming = Mouse.current.rightButton.isPressed;

        Vector3 targetPos = IsAiming ? adsPosition : idlePosition;
        Quaternion targetRot = IsAiming ? adsRotation : idleRotation;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            targetPos,
            adsSpeed * Time.deltaTime);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRot,
            adsSpeed * Time.deltaTime);

        float targetFOV = IsAiming
            ? weaponController.WeaponSettings.adsFOV
            : baseFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            adsSpeed * Time.deltaTime);
    }
}