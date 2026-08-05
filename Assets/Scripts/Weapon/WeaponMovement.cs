using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
// WeaponMovement
// Handles procedural weapon motion: mouse sway and head bobbing during player movement.
// Управляет процедурной анимацией оружия: инерцией (sway) при поворотах мыши и раскачкой (bobbing) при ходьбе.
// Attach this script directly to the weapon transform container inside the camera hierarchy.
// Вешается непосредственно на контейнер оружия внутри иерархии камеры.
// ============================================================
public class WeaponMovement : MonoBehaviour
{
    [Header("Sway (Инерция мыши)")]
    // Intensity multiplier for weapon rotation reaction to mouse movement
    // Сила отклонения оружия при движении мыши
    [SerializeField] private float swayIntensity = 2f;

    // Smoothing speed factor for mouse sway rotation interpolation
    // Скорость сглаживания и возврата оружия при инерции мыши
    [SerializeField] private float swaySmooth = 10f;

    [Header("Bobbing (Раскачка при ходьбе)")]
    // Bobbing oscillation frequency/speed during movement / Частота/скорость вертикального покачивания при ходьбе
    [SerializeField] private float bobFrequency = 10f;

    // Maximum vertical offset amplitude for weapon bobbing / Амплитуда вертикального смещения оружия при покачивании
    [SerializeField] private float bobAmplitude = 0.02f;

    // Default rest position of the weapon container relative to camera / Исходное базовое положение оружия в локальных координатах
    private Vector3 startPosition;

    // Internal time accumulator for sine wave bobbing evaluation / Накопитель времени для вычисления синусоидальной раскачки
    private float timer;

    private void Start()
    {
        // Cache initial local transform position as standard rest reference / Запоминаем стартовое локальное положение для возврата
        startPosition = transform.localPosition;
    }

    private void Update()
    {
        // ----------------------------------------------------
        // 1. Mouse Sway (Поворот за мышкой)
        // ----------------------------------------------------
        // Read raw mouse movement delta vector from modern Input System
        // Считываем дельту перемещения мыши через новую систему ввода (Input System)
        Vector2 mouseInput = Mouse.current.delta.ReadValue();

        // Calculate target offset rotation angles based on mouse delta and intensity scalar
        // Вычисляем целевой угол поворота на основе перемещения мыши и коэффициента интенсивности
        Quaternion targetRotation = Quaternion.Euler(
            -mouseInput.y * swayIntensity,
            mouseInput.x * swayIntensity,
            0f);

        // Smoothly interpolate present local rotation toward target sway offset using Slerp/Lerp
        // Плавно интерполируем текущий поворот к целевому значению сглаживания
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, swaySmooth * Time.deltaTime);

        // ----------------------------------------------------
        // 2. Weapon Bobbing (Раскачка при ходьбе)
        // ----------------------------------------------------
        // Check WASD key bindings to detect horizontal movement input state
        // Проверяем нажатие клавиш WASD для определения движения игрока
        Vector2 moveInput = Keyboard.current.wKey.isPressed || Keyboard.current.sKey.isPressed || 
                            Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed ? new Vector2(1,1) : Vector2.zero;
        
        // Active movement detected: evaluate wave bob motion
        // Движение обнаружено: рассчитываем волновую раскачку
        if (moveInput.magnitude > 0)
        {
            // Accumulate wave step time scaling by bobbing frequency
            // Накапливаем шаг времени, помноженный на частоту покачивания
            timer += Time.deltaTime * bobFrequency;

            // Evaluate vertical Y offset using sine trigonometric function scaled by amplitude
            // Вычисляем вертикальное смещение Y с использованием функции синуса и амплитуды
            float yOffset = Mathf.Sin(timer) * bobAmplitude;

            // Interpolate toward offset bobbing target position / Плавно смещаем оружие в позицию с учетом покачивания
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPosition + new Vector3(0, yOffset, 0), Time.deltaTime * 5f);
        }
        // Idle/Stationary player state: reset motion / Игрок стоит на месте: сбрасываем раскачку
        else
        {
            // Reset sine timer counter / Сбрасываем таймер синусоиды
            timer = 0;

            // Smoothly lerp weapon local position back to reference rest position
            // Плавно возвращаем оружие в исходную дефолтную позицию
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPosition, Time.deltaTime * 5f);
        }
    }
}