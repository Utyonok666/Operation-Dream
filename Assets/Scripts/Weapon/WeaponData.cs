using UnityEngine;

// ============================================================
// WeaponData
// ScriptableObject asset defining all stats, behavior flags, FX, and audio configurations for a weapon.
// Файл настроек (ScriptableObject), содержащий характеристики, поведение, эффекты и звуки оружия.
// Allows creating and tweaking unique weapon presets directly in the Unity Inspector.
// Позволяет создавать и настраивать уникальные пресеты оружия прямо в инспекторе Unity.
// ============================================================
[CreateAssetMenu(fileName = "NewWeapon", menuName = "OperationDream/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Header("General Settings")]
    // Weapon display name / Отображаемое название оружия
    public string weaponName;
    
    // Total damage per shot (split across pellets if shotgun) / Суммарный урон за выстрел (делится на дробь для дробовика)
    public float damage;
    
    // Maximum raycast firing range in units / Максимальная дальность выстрела в юнитах
    public float range;
    
    // Distance where damage falloff begins / Дистанция, с которой начинается спад урона
    public float minDistance = 10f;
    
    // Distance where minimum damage multiplier threshold is reached / Дистанция максимального спада урона
    public float maxDistance = 50f;
    
    // Damage multiplier applied at max distance / Мультипликатор урона на максимальной дистанции
    public float minDamageMultiplier = 0.2f;

    [Header("ADS")]
    // Target camera Field of View when aiming down sights / Целевой угол обзора (FOV) камеры при прицеливании
    public float adsFOV = 75f;

    [Header("Ammo Settings")]
    // Maximum round capacity per magazine / Вместимость одного магазина
    public int magazineSize = 30;
    
    // Maximum reserve ammunition capacity (e.g. AK: 30/300, Shotgun: 8/56)
    // Максимальный запас патронов помимо магазина (например, АК: 30/300, Дробовик: 8/56)
    public int maxReserveAmmo = 300;
    
    // Full magazine reload duration in seconds / Время полной перезарядки магазина в секундах
    public float reloadTime = 2f;

    [Header("Per-Shell Reload (Shotguns)")]
    // Enables individual shell loading loop that can be interrupted by shooting
    // Поэтапная перезарядка по одной гильзе (можно прервать выстрелом)
    public bool perShellReload = false;
    
    // Insertion duration per individual shell (used only when perShellReload is true)
    // Время вставки ОДНОЙ гильзы (используется только при perShellReload = true)
    public float shellReloadTime = 0.5f;

    [Header("Spread and Recoil")]
    // Hip-fire bloom variance angle in degrees / Разброс при стрельбе от бедра (в градусах)
    public float bloomAngle = 2f;
    
    // Vertical camera kickback force applied per shot / Сила вертикального подброса отдачи за выстрел
    public float recoilForce = 0.1f;

    [Header("ADS Settings")]
    // Bloom multiplier when aiming down sights / Мультипликатор снижения разброса при прицеливании
    public float adsBloomMultiplier = 0.1f;
    
    // Recoil multiplier when aiming down sights / Мультипликатор снижения отдачи при прицеливании
    public float adsRecoilMultiplier = 0.5f;

    [Header("Shotgun Settings (Active if pelletCount > 1)")]
    // Number of pellets fired per single shot / Количество дробинок за один выстрел
    public int pelletCount = 1;
    
    // Spread cone angle for shotgun pellets / Угол разброса дроби
    public float spreadAngle = 5f;

    [Header("Fire Mode")]
    // True for automatic firing on hold, false for single semi-auto clicks
    // Автоматический режим (удержание ЛКМ) или одиночные выстрелы (клики)
    public bool isAutomatic = false;
    
    // Delay between consecutive shots in seconds / Задержка между выстрелами в секундах
    public float fireRate = 0.5f;

    [Header("Impact Effects")]
    // Surface hit impact particle prefab for static environment/walls / Эффект попадания по стенам и окружению
    public GameObject hitEffectWall;
    
    // Surface hit impact particle prefab for organic targets/enemies / Эффект попадания по врагам (кровь)
    public GameObject hitEffectEnemy;

    [Header("Muzzle & Shell Visual Effects")]
    // Muzzle flash particle effect prefab spawned at barrel end / Префаб вспышки выстрела на дульном срезе
    public GameObject muzzleFlashPrefab;
    
    // Ejected shell casing physics object prefab / Префаб вылетающей гильзы
    public GameObject shellPrefab;
    
    // Bullet tracer trail line renderer prefab / Префаб визуального трейсера пули
    public TrailRenderer tracerPrefab;

    [Header("Audio Settings")]
    // Array of gunshot audio clips picked randomly to avoid repetitive sounds
    // Массив звуков выстрела (случайный выбор для разнообразия звучания)
    public AudioClip[] shootSounds;
    
    // Reload sound clip / Звуковой клип перезарядки
    public AudioClip reloadSound;
    
    // Primary gunshot volume scalar / Громкость звука выстрела
    [Range(0f, 1f)] public float shootVolume = 1f;
    
    // Reload action volume scalar / Громкость звука перезарядки
    [Range(0f, 1f)] public float reloadVolume = 0.8f;
}