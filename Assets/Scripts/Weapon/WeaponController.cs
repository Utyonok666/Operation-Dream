using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

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

    public WeaponData WeaponSettings => weaponSettings;

    // --- Публичные геттеры для UI (счётчик патронов и т.д.) ---
    public int CurrentAmmo => currentAmmo;
    public int CurrentReserveAmmo => currentReserveAmmo;
    public int MagazineSize => weaponSettings != null ? weaponSettings.magazineSize : 0;
    public bool IsReloading => isReloading;

    private int currentAmmo;
    private int currentReserveAmmo;
    private bool isReloading;
    private bool isADS;
    private float lastShotTime = -999f;

    // Событие, которое будет использовать NetworkWeapon
    public event System.Action<RaycastHit, float> OnPlayerHit;

    // Новое событие: то же самое, но + направление, в котором реально летела пуля
    // (с учётом разброса). Нужно, чтобы при смерти тело падало в сторону выстрела.
    // Подпишись на него там, где сейчас вызывается health.TakeDamage(damage) —
    // и передай damage-direction вторым аргументом в новый оверлоад TakeDamage(damage, direction).
    public event System.Action<RaycastHit, float, Vector3> OnPlayerHitDirectional;
    
    // Событие для дублёра визуальных эффектов (NetworkWeaponEffects)
    public event System.Action OnWeaponFired;

    private void Awake()
    {
        // Страховка: если ссылку забыли перетянуть в инспекторе на префабе
        // (что и произошло - у всех 5 WeaponController networkIdentity был пуст),
        // достаём её из родителя автоматически, чтобы проверка isLocalPlayer
        // в Update() не отключалась молча.
        if (networkIdentity == null)
            networkIdentity = GetComponentInParent<NetworkIdentity>();
    }

    private void Start()
    {
        if (weaponSettings != null)
        {
            currentAmmo = weaponSettings.magazineSize;
            currentReserveAmmo = weaponSettings.maxReserveAmmo;
        }
    }

    private void OnDisable()
    {
        if (isReloading)
        {
            StopAllCoroutines();
            isReloading = false;
        }
    }

    private void Update()
    {
        // Fail-closed: если по какой-то причине networkIdentity так и не
        // нашёлся - НЕ обрабатываем ввод, вместо того чтобы молча
        // разрешить стрельбу всем подряд (как было раньше при пустой ссылке).
        if (networkIdentity == null || !networkIdentity.isLocalPlayer)
            return;

        if (weaponSettings == null)
            return;

        if (weaponSettings == null)
            return;

        isADS = Mouse.current.rightButton.isPressed;

        bool shouldShoot = weaponSettings.isAutomatic
            ? Mouse.current.leftButton.isPressed
            : Mouse.current.leftButton.wasPressedThisFrame;

        if (shouldShoot)
            Shoot();

        if (Keyboard.current.rKey.wasPressedThisFrame &&
            !isReloading &&
            currentAmmo < weaponSettings.magazineSize &&
            currentReserveAmmo > 0)
        {
            StartCoroutine(Reload());
        }
    }

    private void Shoot()
    {
        if (isReloading)
            return;

        if (Time.time < lastShotTime + weaponSettings.fireRate)
            return;

        if (currentAmmo <= 0)
            return;

        lastShotTime = Time.time;
        currentAmmo--;

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

        // --- ЛОКАЛЬНЫЕ ВИЗУАЛЬНЫЕ ЭФФЕКТЫ ---
        PlayVisualEffectsOnly();

        // --- СИГНАЛ В СЕТЬ ДЛЯ ДРУГИХ ИГРОКОВ ---
        OnWeaponFired?.Invoke();

        int pellets = Mathf.Max(1, weaponSettings.pelletCount);
        float damagePerPellet = weaponSettings.damage / pellets;

        for (int i = 0; i < pellets; i++)
            FirePellet(damagePerPellet);
    }

    // Метод, который проигрывает только вспышку и гильзу
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

    // =========================================================================
    // НОВЫЕ МЕТОДЫ: Имитация выстрела для других игроков (БЕЗ УРОНА)
    // =========================================================================
    public void PlayRemoteVisuals()
    {
        if (weaponSettings == null) return;

        // 1. Вспышка и гильза
        PlayVisualEffectsOnly();

        // 2. Трассеры и эффекты попадания для каждого патрона/дробинки
        int pellets = Mathf.Max(1, weaponSettings.pelletCount);
        for (int i = 0; i < pellets; i++)
        {
            FireRemotePellet();
        }
    }

    private void FireRemotePellet()
    {
        // У других клиентов нет информации, в прицеле мы или нет, поэтому используем базовый разброс
        float spread = weaponSettings.pelletCount > 1
            ? weaponSettings.spreadAngle
            : weaponSettings.bloomAngle;

        Vector3 direction = playerCamera.transform.forward;
        direction = Quaternion.Euler(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0f) * direction;

        // Визуальный Raycast
        if (Physics.Raycast(playerCamera.transform.position, direction, out RaycastHit hit, weaponSettings.range))
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            
            // Выбираем эффект (кровь или искры)
            GameObject effect = target != null
                ? weaponSettings.hitEffectEnemy
                : weaponSettings.hitEffectWall;

            if (effect != null)
            {
                Destroy(Instantiate(effect, hit.point, Quaternion.LookRotation(hit.normal)), 1f);
            }

            // Спавним трейсер
            if (weaponSettings.tracerPrefab != null && muzzlePoint != null)
            {
                TrailRenderer tracer = Instantiate(weaponSettings.tracerPrefab, muzzlePoint.position, Quaternion.identity);
                tracer.AddPosition(muzzlePoint.position);
                StartCoroutine(SpawnTracer(tracer, hit.point));
            }
        }
    }
    // =========================================================================

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

    private float CalculateDamage(float distance, float baseDamage)
    {
        float t = Mathf.Clamp01(
            (distance - weaponSettings.minDistance) /
            (weaponSettings.maxDistance - weaponSettings.minDistance));

        return baseDamage * Mathf.Lerp(1f, weaponSettings.minDamageMultiplier, t);
    }

    private System.Collections.IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(weaponSettings.reloadTime);
        int needed = weaponSettings.magazineSize - currentAmmo;
        int amountToReload = Mathf.Min(needed, currentReserveAmmo);
        currentAmmo += amountToReload;
        currentReserveAmmo -= amountToReload;
        isReloading = false;
    }

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

    public void AddReserveAmmo(int amount)
    {
        if (weaponSettings == null) return;
        currentReserveAmmo = Mathf.Clamp(currentReserveAmmo + amount, 0, weaponSettings.maxReserveAmmo);
    }

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
}