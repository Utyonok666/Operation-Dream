using UnityEngine;

public class RecoilHandler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float recoilSpeed = 25f;
    [SerializeField] private float returnSpeed = 12f;

    [SerializeField] private float kickBackAmount = 0.04f;
    [SerializeField] private float kickSpeed = 18f;
    [SerializeField] private float kickReturnSpeed = 12f;

    private Vector3 defaultPosition;
    private Vector3 currentPosition;
    private Vector3 targetPosition;

    private Vector3 currentRotation;
    private Vector3 recoilRotation;

    private void LateUpdate()
    {
        // Возвращаемся к нулю
        recoilRotation = Vector3.Lerp(
            recoilRotation,
            Vector3.zero,
            returnSpeed * Time.deltaTime);

        // Догоняем цель быстро
        currentRotation = Vector3.Lerp(
            currentRotation,
            recoilRotation,
            recoilSpeed * Time.deltaTime);

        transform.localRotation = Quaternion.Euler(currentRotation);

        targetPosition = Vector3.Lerp(
            targetPosition,
            defaultPosition,
            kickReturnSpeed * Time.deltaTime);

        currentPosition = Vector3.Lerp(
            currentPosition,
            targetPosition,
            kickSpeed * Time.deltaTime);

        transform.localPosition = currentPosition;
    }

    private void Start()
    {
        defaultPosition = transform.localPosition;
        currentPosition = defaultPosition;
        targetPosition = defaultPosition;
    }

    public void AddRecoil(float vertical, float horizontal)
    {
        recoilRotation += new Vector3(
            -vertical,
            Random.Range(-horizontal, horizontal),
            0f
        );
        targetPosition += Vector3.back * kickBackAmount;

        // Добавь резкий толчок назад (визуальный Kick)
        // Оружие отлетает назад по Z и чуть вверх по X
        recoilRotation += new Vector3(-vertical, Random.Range(-horizontal, horizontal), 0f);
        targetPosition += new Vector3(0, 0, -kickBackAmount); // Отлетает назад
    }

    public void ResetRecoil()
    {
        currentRotation = Vector3.zero;
        recoilRotation = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}