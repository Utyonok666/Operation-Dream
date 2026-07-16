using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "OperationDream/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public float damage;
    public float range;
    public float minDistance = 10f;
    public float maxDistance = 50f;
    public float minDamageMultiplier = 0.2f;

    [Header("ADS")]
    public float adsFOV = 75f;

    [Header("Патроны")]
    public int magazineSize = 30;
    public int maxReserveAmmo = 300; // Запас патронов помимо магазина. Настраивается вручную под каждую пушку (АК: 30/300, дробовик: 8/56 и т.д.)
    public float reloadTime = 2f;

    [Header("Разброс и отдача")]
    public float bloomAngle = 2f; // Разброс для одиночных (в градусах)
    public float recoilForce = 0.1f; // Насколько камера дергается вверх

    [Header("ADS Settings")]
    public float adsBloomMultiplier = 0.1f; // Например, снижаем разброс в 10 раз
    public float adsRecoilMultiplier = 0.5f; // Снижаем отдачу в 2 раза

    [Header("Дробовик (используется только если pelletCount > 1)")]
    public int pelletCount = 1;
    public float spreadAngle = 5f;

    [Header("Режим стрельбы")]
    public bool isAutomatic = false;
    public float fireRate = 0.5f;

    [Header("Эффекты")]
    public GameObject hitEffectWall;
    public GameObject hitEffectEnemy;

    [Header("Эффекты выстрела (Muzzle & Shell)")]
    public GameObject muzzleFlashPrefab; // Сюда закинешь вспышку, когда появится
    public GameObject shellPrefab;       // Сюда закидывай свой префаб патрона
    public TrailRenderer tracerPrefab;  // Сюда закидывай свой префаб трассера
}