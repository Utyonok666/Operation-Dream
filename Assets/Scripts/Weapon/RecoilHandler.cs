using UnityEngine;

// ============================================================
// RecoilHandler
// Procedural procedural recoil and visual kickback handler for weapons.
// Процедурная отдача оружия и визуальный отброс (Kickback) при стрельбе.
// Smoothly interpolates weapon model rotation and position back to default state.
// Плавное возвращение позиции и вращения модели оружия в исходное состояние.
// ============================================================
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

    private void Start()
    {
        // Cache original position offset relative to parent transform
        // Кэшируем изначальное локальное положение относительно родительского объекта
        defaultPosition = transform.localPosition;
        currentPosition = defaultPosition;
        targetPosition = defaultPosition;
    }

    private void LateUpdate()
    {
        // Smoothly return target recoil rotation back to zero
        // Возвращаемся к нулю
        recoilRotation = Vector3.Lerp(
            recoilRotation,
            Vector3.zero,
            returnSpeed * Time.deltaTime);

        // Interpolate current rotation vector towards target recoil rotation
        // Догоняем цель быстро
        currentRotation = Vector3.Lerp(
            currentRotation,
            recoilRotation,
            recoilSpeed * Time.deltaTime);

        transform.localRotation = Quaternion.Euler(currentRotation);

        // Smoothly interpolate positional kickback offset back to default rest position
        // Плавный возврат смещения назад в исходное положение
        targetPosition = Vector3.Lerp(
            targetPosition,
            defaultPosition,
            kickReturnSpeed * Time.deltaTime);

        // Interpolate current mesh displacement towards target kickback position
        // Плавная интерполяция текущей позиции модели к целевой
        currentPosition = Vector3.Lerp(
            currentPosition,
            targetPosition,
            kickSpeed * Time.deltaTime);

        transform.localPosition = currentPosition;
    }

    // Applies camera/weapon pitch and horizontal sway recoil impulse
    // Применяет импульс отдачи (подброс вверх, случайное отклонение и визуальный отлет)
    public void AddRecoil(float vertical, float horizontal)
    {
        // Initial recoil rotation application and backward positional displacement
        // Первичное прибавление углов отдачи и смещения назад
        recoilRotation += new Vector3(
            -vertical,
            Random.Range(-horizontal, horizontal),
            0f
        );
        targetPosition += Vector3.back * kickBackAmount;

        // TODO: Temporary double-kick implementation for punchier feeling. Remove duplicate block later during fine-tuning.
        // TODO: Временный дубликат для более резкой анимации. Позже этот блок нужно убрать при рефакторинге/настройке параметров.
        // Добавь резкий толчок назад (визуальный Kick)
        // Оружие отлетает назад по Z и чуть вверх по X
        recoilRotation += new Vector3(-vertical, Random.Range(-horizontal, horizontal), 0f);
        targetPosition += new Vector3(0, 0, -kickBackAmount); // Отлетает назад
    }

    // Resets rotational and positional recoil offsets to baseline state
    // Сбрасывает все накапливаемые углы и смещения отдачи в ноль
    public void ResetRecoil()
    {
        currentRotation = Vector3.zero;
        recoilRotation = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}