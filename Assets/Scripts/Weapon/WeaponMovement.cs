using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponMovement : MonoBehaviour
{
    [Header("Sway (Инерция мыши)")]
    [SerializeField] private float swayIntensity = 2f;
    [SerializeField] private float swaySmooth = 10f;

    [Header("Bobbing (Раскачка при ходьбе)")]
    [SerializeField] private float bobFrequency = 10f;
    [SerializeField] private float bobAmplitude = 0.02f;

    private Vector3 startPosition;
    private float timer;

    private void Start()
    {
        startPosition = transform.localPosition;
    }

    private void Update()
    {
        // 1. Sway (поворот за мышкой)
        Vector2 mouseInput = Mouse.current.delta.ReadValue();
        Quaternion targetRotation = Quaternion.Euler(
            -mouseInput.y * swayIntensity,
            mouseInput.x * swayIntensity,
            0f);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, swaySmooth * Time.deltaTime);

        // 2. Bobbing (раскачка при ходьбе)
        Vector2 moveInput = Keyboard.current.wKey.isPressed || Keyboard.current.sKey.isPressed || 
                           Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed ? new Vector2(1,1) : Vector2.zero;
        
        if (moveInput.magnitude > 0)
        {
            timer += Time.deltaTime * bobFrequency;
            float yOffset = Mathf.Sin(timer) * bobAmplitude;
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPosition + new Vector3(0, yOffset, 0), Time.deltaTime * 5f);
        }
        else
        {
            timer = 0;
            transform.localPosition = Vector3.Lerp(transform.localPosition, startPosition, Time.deltaTime * 5f);
        }
    }
}