using UnityEngine;

// ============================================================
// TargetDummy
// Target dummy script implementing the IDamageable interface for testing weapon damage mechanics.
// Скрипт тренировочного манекена, реализующий интерфейс IDamageable для проверки механик урона.
// Logs incoming damage values to the Unity Console and acts as a target for hit registration.
// Выводит значения полученного урона в консоль Unity и служит мишенью для регистрации попаданий.
// ============================================================
public class TargetDummy : MonoBehaviour, IDamageable
{
    // Implementation of the IDamageable interface method called when hit by projectiles/rays
    // Реализация метода интерфейса IDamageable, вызываемого при попадании снарядов или лучей (Raycast)
    public void TakeDamage(float amount)
    {
        // Output registered damage float value to Unity Console
        // Вывод полученного урона в консоль Unity
        Debug.Log($"Манекен получил урон: {amount}");

        // Placeholder for future hit registration visual effects (e.g., blood/dust particle instantiation)
        // Место для добавления будущих визуальных эффектов (например, спавна частиц попадания)
    }
}