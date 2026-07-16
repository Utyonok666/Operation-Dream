using UnityEngine;

public class TargetDummy : MonoBehaviour, IDamageable
{
    public void TakeDamage(float amount)
    {
        Debug.Log($"Манекен получил урон: {amount}");
        // Здесь можно добавить визуальный эффект попадания (Particle System)
    }
}