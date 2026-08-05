using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

// ============================================================
// WeaponController
// Core client-side weapon shooting, spread, damage, and ammo management script.
// Основной скрипт управления стрельбой, разбросом, уроном и боезапасом на клиенте.
// Handles local raycasting, tracers, audio cues, and Mirror network hit events.
// Обрабатывает локальные рейкасты, трейсеры, звуки и сетевые события попаданий Mirror.
// ============================================================
public class WeaponController : MonoBehaviour
{
    [SerializeField] private NetworkIdentity networkIdentity;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponData weaponSettings;
    [SerializeField] private RecoilHandler recoilHandler;
    
    [Header("Spawn Points")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Transform shellEjectPoint;

    [Header("Audio")]
    [Tooltip("3D AudioSource на модели оружия (spatialBlend=1). Один на выстрел+перезарядку, PlayOneShot не блокирует друг друга.")]
    [SerializeField] private AudioSource weaponAudioSource;

    // Public getter to access active ScriptableObject config
    // Публичный геттер для доступа к текущим настройкам ScriptableObject
    public WeaponData WeaponSettings => weaponSettings;

    // --- Public Getters for UI (Ammo counter, reload state, etc.) ---
    // --- Публичные геттеры для UI (счётчик патронов, статус перезарядки) ---
    public int CurrentAmmo => currentAmmo;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public int MagazineSize => weaponSettings != null ? weaponSettings.magazineSize : 0;
    public bool IsReloading => isReloading;

    private int currentAmmo;
    private int currentReserveAmmo;
    private bool isReloading;
    private bool isADS;
    private float lastShotTime = -999f;
    private int _lastShootSoundIndex = -1;
    private Coroutine _reloadCoroutine;

    // Event raised for NetworkWeapon to handle server-side hit validation and damage
    // Событие, передаваемое в NetworkWeapon для валидации попаданий и нанесения урона на сервере
    public event System.Action<RaycastHit, float> OnPlayerHit;

    // Extended event carrying shot direction vector for ragdoll death impulse calculations
    // Расширенное событие с вектором направления пули для импульса смерти регдолла
    public event System.Action<RaycastHit, float, Vector3> OnPlayerHitDirectional;
    
    // Event triggered to notify network FX synchronizer (NetworkWeaponEffects)
    // Событие выстрела для дублирования визуальных эффектов другим игрокам
    public event System.Action OnWeaponFired;

    // Event triggered when reload sequence initiates for remote audio/animation sync
    // Событие начала перезарядки для синхронизации анимации и звука у других клиентов
    public event System.Action OnWeaponReloadStarted;

    private void Awake()
    {
        // Auto-assign NetworkIdentity from parent if missing in inspector field
        // Страховка: автопоиск NetworkIdentity в родителе, если ссылка не задана в инспекторе
        if (networkIdentity == null)
            networkIdentity = GetComponentInParent<NetworkIdentity>();
    }

    private void Start()
    {
        // Initialize magazine and reserve ammo capacities from settings asset
        // Инициализация патронов в магазине и запасе из файла настроек
        if (weaponSettings != null)
        {
            currentAmmo = weaponSettings.magazineSize;
            currentReserveAmmo = weaponSettings.maxReserveAmmo;
        }
    }

    private void OnDisable()
    {
        // Cancel active reload routine if weapon script component is disabled
        // Отмена корутины перезарядки при отключении скрипта или смене оружия
        if (isReloading)
        {
            StopAllCoroutines();
            isReloading = false;
            _reloadCoroutine = null;
        }
    }

    private void Update()
    {
        // Fail-closed safety check: verify local player authority
        // Проверка локального игрока: запрещаем ввод, если нет прав владения объектом
        if (networkIdentity == null || !networkIdentity.isLocalPlayer)
            return;

        // Block input processing if Pause Menu interface is active
        // Блокировка ввода при открытом меню паузы
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused)
            return;

        if (weaponSettings == null)
            return;

        // Check if Right Mouse Button is currently held down
        // Проверка считывания удержания правой кнопки мыши (ADS)
        isADS = Mouse.current.rightButton.isPressed;

        // Evaluate primary trigger condition based on weapon firemode (automatic vs semi-auto)
        // Проверка нажатия стрельбы с учетом режима (автоматический или одиночный)
        bool shouldShoot = weaponSettings.isAutomatic
            ? Mouse.current.leftButton.isPressed
            : Mouse.current.leftButton.wasPressedThisFrame;

        if (shouldShoot)
        {
            // Shotgun interrupt logic: shooting cancels individual shell reloads
            // Прерывание поэтапной дозарядки дробовика при нажатии на выстрел
            if (isReloading && weaponSettings.perShellReload && currentAmmo > 0)
                InterruptReload();

            Shoot();
        }

        // Handle reload keypress (R) when conditions are met
        // Обработка перезарядки по кнопке R
        if (Keyboard.current.rKey.wasPressedThisFrame &&
            !isReloading &&
            currentAmmo < weaponSettings.magazineSize &&
            currentReserveAmmo > 0)
        {
            _reloadCoroutine = StartCoroutine(Reload());
        }
    }

    private void Shoot()
    {
        if (isReloading)
            return;

        // Rate of fire cadence check
        // Проверка задержки между выстрелами
        if (Time.time < lastShotTime + weaponSettings.fireRate)
            return;

        if (currentAmmo <= 0)
            return;

        lastShotTime = Time.time;
        currentAmmo--;

        // Apply visual procedural recoil to camera/weapon model
        // Расчет и применение процедуры отдачи с учетом мультипликатора ADS
        if (recoilHandler != null)
        {
            float recoilMultiplier = isADS
                ? weaponSettings.adsRecoilMultiplier
                : 1f;

            recoilHandler.AddRecoil(
                weaponSettings.recoilForce * recoilMultiplier,
                weaponSettings.recoilForce * 0.35f * recoilMultiplier
            );
        }

        // --- Local Visual Effects ---
        // --- Локальные визуальные эффекты ---
        PlayVisualEffectsOnly();
        PlayShootAudio();

        // --- Network Event Signal ---
        // --- Отправка сетевого сигнала выстрела ---
        OnWeaponFired?.Invoke();

        // Calculate spread and fire individual pellets or single round
        // Расчет урона и разброса на каждую дробинку/пулю
        int pellets = Mathf.Max(1, weaponSettings.pelletCount);
        float damagePerPellet = weaponSettings.damage / pellets;

        for (int i = 0; i < pellets; i++)
            FirePellet(damagePerPellet);
    }

    // Instantiates muzzle flash and shell eject particle prefabs
    // Спавн вспышки выстрела и гильзы с приданием физического импульса
    public void PlayVisualEffectsOnly()
    {
        if (weaponSettings == null) return;

        if (weaponSettings.muzzleFlashPrefab != null && muzzlePoint != null)
        {
            GameObject flash = Instantiate(weaponSettings.muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
            Destroy(flash, 0.1f);
        }

        if (weaponSettings.shellPrefab != null && shellEjectPoint != null)
        {
            GameObject shell = Instantiate(weaponSettings.shellPrefab, shellEjectPoint.position, shellEjectPoint.rotation);
            Rigidbody shellRb = shell.GetComponent<Rigidbody>();
            if (shellRb != null)
            {
                Vector3 ejectDirection = shellEjectPoint.right + (shellEjectPoint.up * 0.5f);
                shellRb.AddForce(ejectDirection * Random.Range(3f, 5f), ForceMode.Impulse);
                shellRb.AddTorque(Random.insideUnitSphere * Random.Range(10f, 50f));
            }
            Destroy(shell, 3f);
        }
    }

    // Plays random non-repeating gunshot sound effect using OneShot
    // Воспроизведение случайного звука выстрела без повторения предыдущего
    public void PlayShootAudio()
    {
        if (weaponAudioSource == null || weaponSettings == null) return;
        if (weaponSettings.shootSounds == null || weaponSettings.shootSounds.Length == 0) return;

        AudioClip clip = PickShootClip();
        if (clip == null) return;

        weaponAudioSource.PlayOneShot(clip, weaponSettings.shootVolume);
    }

    // Plays reload audio clip
    // Воспроизведение звука перезарядки
    public void PlayReloadAudio()
    {
        if (weaponAudioSource == null || weaponSettings == null) return;
        if (weaponSettings.reloadSound == null) return;

        weaponAudioSource.PlayOneShot(weaponSettings.reloadSound, weaponSettings.reloadVolume);
    }

    private AudioClip PickShootClip()
    {
        AudioClip[] clips = weaponSettings.shootSounds;

        if (clips.Length == 1)
            return clips[0];

        int index;
        do
        {
            index = Random.Range(0, clips.Length);
        } while (index == _lastShootSoundIndex);

        _lastShootSoundIndex = index;
        return clips[index];
    }

    // =========================================================================
    // REMOTE SIMULATION METHODS (Visuals only, zero damage calculation)
    // РЕЖИМ СИМУЛЯЦИИ ВЫСТРЕЛА ДЛЯ ДРУГИХ КЛИЕНТОВ (Только визуальные эффекты, без урона)
    // =========================================================================
    public void PlayRemoteVisuals()
    {
        if (weaponSettings == null) return;

        // 1. Muzzle flash and shell physics
        // 1. Вспышка и гильза
        PlayVisualEffectsOnly();
        PlayShootAudio();

        // 2. Tracers and surface impact particles per pellet
        // 2. Трейсеры и эффекты попадания для каждого патрона/дробинки
        int pellets = Mathf.Max(1, weaponSettings.pelletCount);
        for (int i = 0; i < pellets; i++)
        {
            FireRemotePellet();
        }
    }

    private void FireRemotePellet()
    {
        // Remote clients use default spread calculation without ADS state
        // Базовый разброс для сторонних клиентов
        float spread = weaponSettings.pelletCount > 1
            ? weaponSettings.spreadAngle
            : weaponSettings.bloomAngle;

        Vector3 direction = playerCamera.transform.forward;
        direction = Quaternion.Euler(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0f) * direction;

        // Visual Raycast
        // Визуальный Рейкаст
        if (Physics.Raycast(playerCamera.transform.position, direction, out RaycastHit hit, weaponSettings.range))
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            
            // Select impact particle prefab (blood or surface metal/stone impact)
            // Выбор эффекта попадания (кровь или стены)
            GameObject effect = target != null
                ? weaponSettings.hitEffectEnemy
                : weaponSettings.hitEffectWall;

            if (effect != null)
            {
                Destroy(Instantiate(effect, hit.point, Quaternion.LookRotation(hit.normal)), 1f);
            }

            // Spawn visual bullet tracer line
            // Спавн трейсера пули
            if (weaponSettings.tracerPrefab != null && muzzlePoint != null)
            {
                TrailRenderer tracer = Instantiate(weaponSettings.tracerPrefab, muzzlePoint.position, Quaternion.identity);
                tracer.AddPosition(muzzlePoint.position);
                StartCoroutine(SpawnTracer(tracer, hit.point));
            }
        }
    }
    // =========================================================================

    // Performs actual raycast logic, hit detection, damage falloff calculation and triggers events
    // Выполнение фактического рейкаста, расчет разброса, дистанции урона и вызов событий
    private void FirePellet(float damagePerPellet)
    {
        float spread = weaponSettings.pelletCount > 1
            ? weaponSettings.spreadAngle
            : weaponSettings.bloomAngle *
            (isADS ? weaponSettings.adsBloomMultiplier : 1f);

        Vector3 direction = playerCamera.transform.forward;

        direction = Quaternion.Euler(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0f) * direction;

        if (Physics.Raycast(
                playerCamera.transform.position,
                direction,
                out RaycastHit hit,
                weaponSettings.range))
        {
            float distance = Vector3.Distance(
                playerCamera.transform.position,
                hit.point);

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();

            float damage = CalculateDamage(distance, damagePerPellet);

            if (target != null)
            {
                OnPlayerHit?.Invoke(hit, damage);
                OnPlayerHitDirectional?.Invoke(hit, damage, direction);
            }

            GameObject effect = target != null
                ? weaponSettings.hitEffectEnemy
                : weaponSettings.hitEffectWall;

            if (effect != null)
            {
                Destroy(
                    Instantiate(
                        effect,
                        hit.point,
                        Quaternion.LookRotation(hit.normal)),
                    1f);
            }

            if (weaponSettings.tracerPrefab != null && muzzlePoint != null)
            {
                TrailRenderer tracer = Instantiate(
                    weaponSettings.tracerPrefab,
                    muzzlePoint.position,
                    Quaternion.identity);

                tracer.AddPosition(muzzlePoint.position);

                StartCoroutine(SpawnTracer(tracer, hit.point));
            }
        }
    }

    // Calculates damage attenuation based on distance falloff curve values
    // Расчет спада урона в зависимости от расстояния до цели
    private float CalculateDamage(float distance, float baseDamage)
    {
        float t = Mathf.Clamp01(
            (distance - weaponSettings.minDistance) /
            (weaponSettings.maxDistance - weaponSettings.minDistance));

        return baseDamage * Mathf.Lerp(1f, weaponSettings.minDamageMultiplier, t);
    }

    // Handles full reload routine or individual shell insertion loop
    // Корутина перезарядки (обойма целиком или поочередная вставка патронов)
    private System.Collections.IEnumerator Reload()
    {
        isReloading = true;

        if (weaponSettings.perShellReload)
        {
            // Per-shell loop: plays audio per round, can be interrupted by shooting input
            // Поочередная перезарядка: проигрывает звук на каждую гильзу, прерывается выстрелом
            while (currentAmmo < weaponSettings.magazineSize && currentReserveAmmo > 0)
            {
                yield return new WaitForSeconds(weaponSettings.shellReloadTime);

                PlayReloadAudio();
                OnWeaponReloadStarted?.Invoke();

                currentAmmo++;
                currentReserveAmmo--;
            }
        }
        else
        {
            PlayReloadAudio();
            OnWeaponReloadStarted?.Invoke();

            yield return new WaitForSeconds(weaponSettings.reloadTime);

            int needed = weaponSettings.magazineSize - currentAmmo;
            int amountToReload = Mathf.Min(needed, currentReserveAmmo);
            currentAmmo += amountToReload;
            currentReserveAmmo -= amountToReload;
        }

        isReloading = false;
        _reloadCoroutine = null;
    }

    /// <summary>
    /// Interrupts individual shell insertion early while retaining loaded ammo.
    /// Прерывает дозарядку гильз досрочно (например, выстрел на 5 из 8) - оставляет уже заряженные патроны.
    /// </summary>
    private void InterruptReload()
    {
        if (_reloadCoroutine != null)
        {
            StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = null;
        }

        isReloading = false;
    }

    // Assigns new ScriptableObject data and resets ammo state
    // Смена активного оружия и сброс текущего состояния
    public void SetWeapon(WeaponData newWeapon)
    {
        weaponSettings = newWeapon;
        if (weaponSettings == null) return;
        currentAmmo = weaponSettings.magazineSize;
        currentReserveAmmo = weaponSettings.maxReserveAmmo;
        isReloading = false;
        lastShotTime = -999f;
        if (recoilHandler != null) recoilHandler.ResetRecoil();
    }

    // Refills reserve ammo count up to maximum capacity limit
    // Пополнение запасных патронов с ограничением по максимальному количеству
    public void AddReserveAmmo(int amount)
    {
        if (weaponSettings == null) return;
        currentReserveAmmo = Mathf.Clamp(currentReserveAmmo + amount, 0, weaponSettings.maxReserveAmmo);
    }

    // Animates bullet tracer particle moving from muzzle point to impact point
    // Анимация полета трейсера от дула к точке попадания
    private System.Collections.IEnumerator SpawnTracer(TrailRenderer tracer, Vector3 hitPoint)
    {
        float time = 0;
        Vector3 startPosition = tracer.transform.position;

        while (time < 1)
        {
            tracer.transform.position = Vector3.Lerp(startPosition, hitPoint, time);
            time += Time.deltaTime / 0.05f; 
            yield return null;
        }

        tracer.transform.position = hitPoint;
        Destroy(tracer.gameObject, tracer.time);
    }

    private void OnEnable()
    {
        // Ensure reload status is cleared whenever game object gets enabled
        // При любом включении объекта принудительно снимаем флаг перезарядки
        isReloading = false;
        _reloadCoroutine = null;
    }

    // Force-resets reload routines and restores full magazine/reserve capacity on respawn
    // Принудительный сброс состояний и восполнение патронов (при респавне)
    public void ResetWeapon()
    {
        StopAllCoroutines();
        isReloading = false;
        _reloadCoroutine = null;

        if (weaponSettings != null)
        {
            currentAmmo = weaponSettings.magazineSize;
            currentReserveAmmo = weaponSettings.maxReserveAmmo;
        }

        lastShotTime = -999f;
    }
}