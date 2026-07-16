using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponController : MonoBehaviour
{
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
        // Оружие убрали (переключили класс/слот) пока шёл релоад.
        // Unity сама остановит корутину, но isReloading останется true навсегда — сбрасываем вручную,
        // иначе при повторном доставании оружие "залипнет" и не будет ни стрелять, ни перезаряжаться.
        if (isReloading)
        {
            StopAllCoroutines();
            isReloading = false;
        }
    }

    private void Update()
    {
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
        {
            // Патронов в магазине нет — можно повесить сюда звук "клак" на сухой щелчок
            return;
        }

        lastShotTime = Time.time;
        currentAmmo--;

        // Отдача камеры
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

        // --- СПАВН ВСПЫШКИ ---
        if (weaponSettings.muzzleFlashPrefab != null && muzzlePoint != null)
        {
            GameObject flash = Instantiate(weaponSettings.muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation, muzzlePoint);
            Destroy(flash, 0.1f);
        }

        // --- ВЫБРОС ГИЛЬЗЫ ---
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

        int pellets = Mathf.Max(1, weaponSettings.pelletCount);
        float damagePerPellet = weaponSettings.damage / pellets;

        for (int i = 0; i < pellets; i++)
            FirePellet(damagePerPellet);
    }

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
                weaponSettings.range,
                ~LayerMask.GetMask("Player")))
        {
            float distance = Vector3.Distance(
                playerCamera.transform.position,
                hit.point);

            IDamageable target = hit.collider.GetComponent<IDamageable>();

            float damage = CalculateDamage(distance, damagePerPellet);

            if (target != null)
            {
                // Пока оставляем для одиночной игры.
                target.TakeDamage(damage);

                // Потом NetworkWeapon подпишется на это событие.
                OnPlayerHit?.Invoke(hit, damage);
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

            // --- СПАВН ТРЕЙСЕРА ---
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

        return baseDamage *
               Mathf.Lerp(
                   1f,
                   weaponSettings.minDamageMultiplier,
                   t);
    }

    private System.Collections.IEnumerator Reload()
    {
        isReloading = true;

        yield return new WaitForSeconds(
            weaponSettings.reloadTime);

        // Сколько патронов реально не хватает в магазине
        int needed = weaponSettings.magazineSize - currentAmmo;

        // Не берём из резерва больше, чем там есть
        int amountToReload = Mathf.Min(needed, currentReserveAmmo);

        currentAmmo += amountToReload;
        currentReserveAmmo -= amountToReload;

        isReloading = false;
    }

    public void SetWeapon(WeaponData newWeapon)
    {
        weaponSettings = newWeapon;

        if (weaponSettings == null)
            return;

        currentAmmo = weaponSettings.magazineSize;
        currentReserveAmmo = weaponSettings.maxReserveAmmo;
        isReloading = false;
        lastShotTime = -999f;

        if (recoilHandler != null)
            recoilHandler.ResetRecoil();

        Debug.Log($"Weapon switched to {weaponSettings.weaponName}");
    }

    // --- Добавить патронов в резерв (для будущих пикапов патронов) ---
    public void AddReserveAmmo(int amount)
    {
        if (weaponSettings == null) return;
        currentReserveAmmo = Mathf.Clamp(currentReserveAmmo + amount, 0, weaponSettings.maxReserveAmmo);
    }

    // --- КОРУТИНА ДЛЯ ПОЛЕТА ТРЕЙСЕРА ---
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