using System.Collections;
using UnityEngine;
using Mirror;

// ============================================================
// PlayerDeath
// Full death/respawn pipeline: ragdoll-style fall, weapon drop, death camera,
// death menu, respawn with chosen loadout.
// Полный пайплайн смерти/респавна: падение тела, выброс оружия, камера смерти,
// меню смерти, респавн с выбранным лоадаутом.
//
// Flow / Поток:
// Health.OnDeath -> ServerDie() [Server] -> RpcDie() [ClientRpc, all clients]
//   -> DeathRoutine() coroutine: disable controls/colliders, drop weapon,
//      camera follows corpse, corpse falls and lands.
// DeathMenuController.OnRespawnClicked -> CmdRequestRespawn() [Command, client->server]
//   -> RpcOnRespawn() [ClientRpc]: restore camera/body/weapons, teleport to spawn point.
// ============================================================
public class PlayerDeath : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private MouseLook mouseLook;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private WeaponSwitcher weaponSwitcher;
    [Tooltip("Та же камера, что стоит в PlayerMovement -> Camera Transform")]
    [SerializeField] private Transform cameraTransform;

    [Header("Death Animation")]
    [SerializeField] private float fallDuration = 0.45f;
    [SerializeField] private float corpseTime = 2f;
    [SerializeField] private float sinkDuration = 1.5f; // Unused directly here, kept for SinkIntoGround() // Не используется напрямую тут, для SinkIntoGround()
    [SerializeField] private float sinkDistance = 1.3f;

    [Header("Fall Settings")]
    [Tooltip("На сколько тело 'проскальзывает' по направлению пули, пока падает")]
    [SerializeField] private float fallSlideDistance = 0.6f;
    [Tooltip("Доп. зазор поверх толщины тела (радиуса капсулы), чтобы труп не проваливался в землю")]
    [SerializeField] private float corpseGroundOffset = 0.05f;
    [Tooltip("Слои, которые считаются 'землёй' для приземления трупа")]
    [SerializeField] private LayerMask groundMask = ~0; // ~0 = "Everything" by default // ~0 = "Everything" по умолчанию
    [SerializeField] private float groundCheckDistance = 5f;

    [Header("Death Camera")]
    [Tooltip("Скорость доворота камеры на упавшее тело")]
    [SerializeField] private float cameraLookSmoothing = 8f;
    [Tooltip("Скорость следования камеры за телом")]
    [SerializeField] private float cameraFollowSmoothing = 10f;
    [Tooltip("Высота камеры над телом, когда оно уже легло")]
    [SerializeField] private float cameraHeightAboveBody = 0.08f;
    [Tooltip("Сдвиг камеры вперёд от головы тела в режиме смерти")]
    [SerializeField] private float cameraForwardOffset = 0.02f;

    [Header("Weapon Drop")]
    [SerializeField] private float weaponDropImpulse = 0.9f;
    [SerializeField] private float weaponDropTorque = 1.6f;

    private bool isDead;
    private GameObject droppedWeaponInstance;
    
    // Cached transform state so the dropped weapon can be returned exactly where it was
    // Закэшированное состояние transform, чтобы вернуть выброшенное оружие точно на место
    private Transform weaponOriginalParent;
    private Vector3 weaponOriginalLocalPosition;
    private Quaternion weaponOriginalLocalRotation;

    // Body's original pose before falling — needed to restore a standing pose on respawn
    // (otherwise the character would remain lying down forever after respawn).
    // Исходная поза тела до падения — нужна, чтобы вернуть труп в стоячую позу при
    // респавне (иначе персонаж навсегда остаётся лежать).
    private Vector3 bodyOriginalLocalPosition;
    private Quaternion bodyOriginalLocalRotation;
    private Vector3 bodyOriginalLocalScale;
    private bool bodyOriginalCached;

    private void Awake()
    {
        // Auto-fill references if not wired in Inspector // Автозаполнение ссылок, если не назначены в инспекторе
        if (health == null)
            health = GetComponent<Health>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (mouseLook == null)
            mouseLook = GetComponent<MouseLook>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (weaponSwitcher == null)
            weaponSwitcher = GetComponentInChildren<WeaponSwitcher>(true);

        CacheBodyOriginalPoseIfNeeded();
    }

    // Caches the body's starting pose once, on first Awake — used later to un-ragdoll it
    // Кэширует стартовую позу тела один раз, при первом Awake — используется, чтобы вернуть позу
    private void CacheBodyOriginalPoseIfNeeded()
    {
        if (bodyOriginalCached)
            return;

        Transform body = playerMovement != null ? playerMovement.BodyVisual : null;
        if (body == null)
            return;

        bodyOriginalLocalPosition = body.localPosition;
        bodyOriginalLocalRotation = body.localRotation;
        bodyOriginalLocalScale = body.localScale;
        bodyOriginalCached = true;
    }

    private void RestoreBodyPose()
    {
        Transform body = playerMovement != null ? playerMovement.BodyVisual : null;
        if (body == null || !bodyOriginalCached)
            return;

        body.localPosition = bodyOriginalLocalPosition;
        body.localRotation = bodyOriginalLocalRotation;
        body.localScale = bodyOriginalLocalScale;
    }

    // ==================== DEATH ENTRY POINT (SERVER) ====================
    // ==================== ТОЧКА ВХОДА СМЕРТИ (СЕРВЕР) ====================

    // [Server] attribute: this method may only run on the server; Mirror logs a
    // warning and no-ops if it's somehow called on a client.
    // Атрибут [Server]: метод может выполняться только на сервере; Mirror
    // предупредит и ничего не сделает, если вызвать его с клиента.
    [Server]
    public void ServerDie(Vector3 hitDirection = default)
    {
        // If no direction was explicitly passed, use the last one stored in Health
        // (valid only on server, since TakeDamage only runs there).
        // Если направление явно не передали - берём последнее сохранённое в Health
        // (валидно именно на сервере, т.к. TakeDamage выполняется только там).
        if (hitDirection == Vector3.zero && health != null)
            hitDirection = health.LastHitDirection;

        RpcDie(hitDirection);
    }

    [Header("References")]
    [SerializeField] private DeathMenuController deathMenuController;

    // [ClientRpc]: called by the server, executes on every connected client (including host).
    // This is where all the visual/local death behavior actually happens.
    // [ClientRpc]: вызывается сервером, выполняется на КАЖДОМ клиенте (включая хост).
    // Именно тут происходит вся визуальная/локальная логика смерти.
    [ClientRpc]
    private void RpcDie(Vector3 hitDirection)
    {
        if (isDead) return; // Guard against double-trigger // Защита от повторного срабатывания
        isDead = true;

        if (health != null)
            health.SetHealth(0);

        if (deathMenuController != null)
            deathMenuController.ShowDeathMenu();

        StartCoroutine(DeathRoutine(hitDirection));
    }

    // Orchestrates the whole death sequence, step by step
    // Оркеструет всю последовательность смерти, шаг за шагом
    private IEnumerator DeathRoutine(Vector3 hitDirection)
    {
        // Disable player control // Отключаем управление
        playerMovement.enabled = false;
        mouseLook.enabled = false;

        if (characterController != null)
            characterController.enabled = false;

        WeaponController droppedWeapon = DisableWeaponsAndFindActive();
        DisableBodyColliders(droppedWeapon);

        Transform body = playerMovement.BodyVisual;

        if (body == null)
            yield break;

        Vector3 fallDirXZ = GetFallDirectionXZ(hitDirection, body);

        if (droppedWeapon != null)
            DropWeapon(droppedWeapon, fallDirXZ);

        // Camera is parented directly to the body so it falls/rotates together with it
        // Камера привязывается к телу напрямую, чтобы падать и поворачиваться вместе с ним
        Coroutine cameraRoutine = StartCoroutine(CameraFollowBody(body));

        yield return StartCoroutine(FallToGround(body, fallDirXZ));

        yield return new WaitForSeconds(corpseTime); // Corpse lies visible for a while before disappearing on respawn // Труп лежит какое-то время до исчезновения при респавне
    }

    // ==================== WEAPON DISABLING ====================
    // ==================== ОТКЛЮЧЕНИЕ ОРУЖИЯ ====================

    // Disables all colliders/rigidbodies on the player, except the weapon being dropped
    // (that one needs physics ON to fall naturally to the ground).
    // Отключает все коллайдеры/rigidbody на игроке, кроме выбрасываемого оружия
    // (у него физика ДОЛЖНА остаться включённой, чтобы естественно упасть).
    private void DisableBodyColliders(WeaponController droppedWeapon)
    {
        Transform excludedRoot = droppedWeapon != null ? droppedWeapon.transform : null;

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider == null)
                continue;

            if (excludedRoot != null && collider.transform.IsChildOf(excludedRoot))
                continue; // Skip the weapon being dropped // Пропускаем выбрасываемое оружие

            collider.enabled = false;
        }

        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null)
                continue;

            if (excludedRoot != null && rb.transform.IsChildOf(excludedRoot))
                continue;

            rb.isKinematic = true; // Freeze physics on everything else // Замораживаем физику на всём остальном
        }
    }

    // Disables all weapon scripts, figures out which weapon was actively held
    // (that's the one that will be visually dropped; the rest are simply hidden).
    // Отключает все скрипты оружия, определяет, какое оружие было в руках
    // (именно оно будет визуально выброшено; остальные просто прячутся).
    private WeaponController DisableWeaponsAndFindActive()
    {
        WeaponController activeWeapon = null;

        if (weaponSwitcher != null && weaponSwitcher.ActiveWeapon != null)
            activeWeapon = weaponSwitcher.ActiveWeapon;

        foreach (WeaponController wc in GetComponentsInChildren<WeaponController>(true))
        {
            if (wc == null)
                continue;

            if (activeWeapon == null && weaponSwitcher != null && wc.gameObject == weaponSwitcher.PistolWeapon)
                continue; // Skip pistol as a fallback guess if nothing else matched yet // Пропускаем пистолет как запасной вариант, если ещё ничего не подошло

            if (activeWeapon == null && wc.gameObject.activeInHierarchy)
                activeWeapon = wc;

            wc.enabled = false;
        }

        if (activeWeapon == null)
        {
            // Fallback scan if the above didn't find anything // Запасной проход, если выше ничего не нашли
            foreach (WeaponController wc in GetComponentsInChildren<WeaponController>(true))
            {
                if (wc == null)
                    continue;

                if (wc.gameObject.activeInHierarchy)
                {
                    activeWeapon = wc;
                    break;
                }
            }
        }

        foreach (WeaponMovement wm in GetComponentsInChildren<WeaponMovement>(true))
            if (wm != null)
                wm.enabled = false;

        foreach (WeaponADS ads in GetComponentsInChildren<WeaponADS>(true))
            if (ads != null)
                ads.enabled = false;

        foreach (RecoilHandler rh in GetComponentsInChildren<RecoilHandler>(true))
            if (rh != null)
                rh.enabled = false;

        // Hide every weapon except the one being dropped — otherwise a second slot
        // (e.g. pistol) stays SetActive(true) and appears in the corpse's hands
        // as if the player pulled it out themselves.
        // Прячем всё оружие, кроме выбрасываемого - иначе второй слот
        // (например, пистолет) остаётся активным и виден в руках трупа,
        // будто игрок его "достал" сам.
        foreach (WeaponController wc in GetComponentsInChildren<WeaponController>(true))
        {
            if (wc == null)
                continue;

            if (activeWeapon != null && wc.gameObject == activeWeapon.gameObject)
                continue;

            wc.gameObject.SetActive(false);
        }

        return activeWeapon;
    }

    // ==================== BODY FALL ====================
    // ==================== ПАДЕНИЕ ТЕЛА ====================

    // Determines which horizontal direction the body should fall towards
    // Определяет, в каком горизонтальном направлении должно упасть тело
    private Vector3 GetFallDirectionXZ(Vector3 hitDirection, Transform body)
    {
        // Default: fall backwards, opposite of where the body was looking
        // По умолчанию: падаем назад, противоположно направлению взгляда тела
        Vector3 fallbackDir = -body.forward;

        if (hitDirection != Vector3.zero)
        {
            // Flatten the hit direction onto the horizontal plane (ignore vertical component)
            // Проецируем направление попадания на горизонтальную плоскость (игнорируем вертикаль)
            Vector3 dir = Vector3.ProjectOnPlane(hitDirection, Vector3.up);
            if (dir.sqrMagnitude > 0.01f)
                fallbackDir = -dir; // Fall away from where the bullet came from // Падаем в сторону от прилетевшей пули
        }

        return fallbackDir.normalized;
    }

    // Animates the body smoothly from standing to lying-down over fallDuration seconds
    // Плавно анимирует тело из стоячего положения в лежачее за fallDuration секунд
    private IEnumerator FallToGround(Transform body, Vector3 fallDirXZ)
    {
        Vector3 startPos = body.position;
        Quaternion startRot = body.rotation;

        Vector3 endPos = CalculateLandingPosition(startPos, fallDirXZ);
        Quaternion endRot = CalculateLandingRotation(startRot, fallDirXZ);

        float time = 0f;

        while (time < fallDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(0f, 1f, time / fallDuration); // Ease-in/out curve, not linear // Плавная кривая, не линейная

            body.position = Vector3.Lerp(startPos, endPos, t);
            body.rotation = Quaternion.Slerp(startRot, endRot, t);

            yield return null;
        }

        // Snap to exact final values to avoid any floating point drift // Довыставляем точные значения, чтобы избежать погрешности float
        body.position = endPos;
        body.rotation = endRot;
    }

    // Raycasts down to find the actual ground height under the corpse's landing spot
    // Рейкастом вниз ищет реальную высоту земли в точке приземления трупа
    private Vector3 CalculateLandingPosition(Vector3 startPos, Vector3 fallDirXZ)
    {
        Vector3 slidPos = startPos + fallDirXZ * fallSlideDistance;

        float groundY = startPos.y; // Fallback if raycast finds nothing // Заглушка, если рейкаст ничего не найдёт

        Vector3 rayOrigin = slidPos + Vector3.up * 1f;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance + 1f,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
        }
        else
        {
            // If this fires, groundMask in the Inspector is almost certainly set to
            // "Nothing" (a common Unity trap: a new field on an old component doesn't
            // pick up the C# default ~0, it gets serialized as 0/Nothing).
            // Если это сработало - почти наверняка groundMask в инспекторе стоит
            // на "Nothing" (частая ловушка Unity: новое поле на старом компоненте
            // не подхватывает C#-дефолт ~0, сериализуется как 0/Nothing).
            Debug.LogWarning(
                "[PlayerDeath] Рейкаст в землю не нашёл коллайдер под трупом. " +
                "Проверь Ground Mask в инспекторе PlayerDeath - должен быть выставлен слой земли, а не 'Nothing'.",
                this);
        }

        slidPos.y = groundY + GetCorpseRestHeight();
        return slidPos;
    }

    // Height offset so the corpse's pivot rests correctly relative to the ground
    // Отступ по высоте, чтобы пивот трупа корректно лежал относительно земли
    private float GetCorpseRestHeight()
    {
        // When the body lies flat, its pivot (usually mesh center) ends up roughly at
        // the CharacterController's radius above the ground — not almost touching it,
        // like the old fixed small offset did (corpse used to sink into the floor).
        // Когда тело ложится плашмя, его пивот оказывается примерно на высоте
        // радиуса капсулы над землёй - а не почти вплотную, как раньше с фиксированным
        // маленьким отступом (труп проваливался в пол).
        float baseRadius = characterController != null ? characterController.radius : 0.4f;
        return baseRadius + corpseGroundOffset;
    }

    // Computes the "toppled over" rotation for the corpse
    // Считает "заваленный" поворот для трупа
    private Quaternion CalculateLandingRotation(Quaternion startRot, Vector3 fallDirXZ)
    {
        // Topple the body around an axis perpendicular to the fall direction — like a
        // felled tree, starting from its current rotation. Previously the rotation was
        // rebuilt from scratch via LookRotation, which made Slerp spin the body through
        // an extra yaw turn (looked like a corkscrew).
        //
        // If the model topples "the wrong way" (e.g. back-first instead of face-first),
        // flip the angle sign below from 90f to -90f.
        //
        // Заваливаем тело вокруг оси, перпендикулярной направлению падения -
        // как срубленное дерево, от текущего поворота. Раньше поворот
        // строился с нуля через LookRotation, из-за чего Slerp прокручивал
        // тело через лишний разворот по yaw (выглядело как штопор).
        //
        // Если модель заваливается "не в ту сторону" - смени знак угла ниже
        // с 90f на -90f.
        Vector3 toppleAxis = Vector3.Cross(Vector3.up, fallDirXZ);

        if (toppleAxis.sqrMagnitude < 0.0001f)
            toppleAxis = Vector3.right; // Fallback for edge case where fallDir is parallel to up // Заглушка на случай, если направление падения параллельно "вверх"

        toppleAxis.Normalize();

        Quaternion toppleRotation = Quaternion.AngleAxis(90f, toppleAxis);

        return toppleRotation * startRot;
    }

    // Sinks the corpse into the ground — currently unused in DeathRoutine but kept
    // available for later (e.g. a "corpse cleanup" effect before respawn timer).
    // Погружает труп под землю — сейчас не используется в DeathRoutine, но
    // оставлено доступным на будущее (напр. эффект "исчезновения" трупа).
    private IEnumerator SinkIntoGround(Transform body)
    {
        Vector3 startPos = body.position;
        Vector3 endPos = startPos + Vector3.down * sinkDistance;

        float time = 0f;

        while (time < sinkDuration)
        {
            time += Time.deltaTime;

            float t = Mathf.SmoothStep(0f, 1f, time / sinkDuration);

            body.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }

        body.position = endPos;
    }

    // ==================== DEATH CAMERA ====================
    // ==================== КАМЕРА СМЕРТИ ====================

    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPosition;
    private Quaternion originalCameraLocalRotation;
    private bool isCameraFollowingBody;

    // Reparents the camera onto the corpse so it falls/looks together with it,
    // then smoothly settles into a fixed offset once the body has landed.
    // Перепривязывает камеру к трупу, чтобы она падала/смотрела вместе с ним,
    // затем плавно устанавливается в фиксированное положение после приземления.
    private IEnumerator CameraFollowBody(Transform body)
    {
        if (cameraTransform == null || body == null)
            yield break;

        // Cache original camera attachment so it can be restored on respawn
        // Кэшируем исходное крепление камеры, чтобы вернуть его при респавне
        originalCameraParent = cameraTransform.parent;
        originalCameraLocalPosition = cameraTransform.localPosition;
        originalCameraLocalRotation = cameraTransform.localRotation;
        isCameraFollowingBody = true;

        cameraTransform.SetParent(body, true);
        cameraTransform.localPosition = new Vector3(0f, cameraHeightAboveBody, cameraForwardOffset);
        cameraTransform.localRotation = Quaternion.identity;

        while (body != null && body.gameObject.activeInHierarchy)
        {
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                new Vector3(0f, cameraHeightAboveBody, cameraForwardOffset),
                Time.deltaTime * cameraFollowSmoothing);

            cameraTransform.localRotation = Quaternion.Slerp(
                cameraTransform.localRotation,
                Quaternion.identity,
                Time.deltaTime * cameraLookSmoothing);

            yield return null;
        }

        if (cameraTransform != null && originalCameraParent != null)
        {
            cameraTransform.SetParent(originalCameraParent, true);
        }

        isCameraFollowingBody = false;
    }

    // Ensures dropped weapon colliders are enabled and solid (not triggers)
    // Убеждается, что коллайдеры выброшенного оружия включены и не триггеры
    private void EnsureDroppedWeaponColliders(Transform weaponTransform)
    {
        foreach (Collider collider in weaponTransform.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null)
                continue;

            collider.enabled = true;
            collider.isTrigger = false;
        }
    }

    // ==================== RESPAWN (SERVER) ====================
    // ==================== РЕСПАВН (СЕРВЕР) ====================

    // [Command]: called from the OWNING client (via DeathMenuController's Respawn button),
    // but only ever actually runs on the server. This is how the client "requests"
    // something without being able to directly manipulate authoritative game state.
    // [Command]: вызывается с клиента-владельца (через кнопку Respawn в
    // DeathMenuController), но фактически выполняется только на сервере. Так
    // клиент "просит" о чём-то, не имея прав напрямую менять авторитетное состояние.
    [Command]
    public void CmdRequestRespawn(int loadoutIndex)
    {
        if (health != null)
            health.Respawn();

        // Mirror keeps track of spawn points itself (NetworkStartPosition component
        // on scene objects) and can hand them out randomly/round-robin depending on
        // NetworkManager -> Player Spawn Method. We reuse the same system here for
        // respawns, not just the first match entry.
        // Mirror сам ведёт список точек спавна (компонент NetworkStartPosition
        // на объектах в сцене) и умеет отдавать их случайно/по кругу в зависимости
        // от NetworkManager -> Player Spawn Method. Переиспользуем ту же систему
        // для респавна, а не только для первого захода в матч.
        Transform spawnPoint = NetworkManager.singleton != null
            ? NetworkManager.singleton.GetStartPosition()
            : null;

        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        RpcOnRespawn(loadoutIndex, spawnPosition, spawnRotation);
    }

    // Runs on every client (including host): actually resets everything visually/locally
    // Выполняется на каждом клиенте (включая хост): реально сбрасывает всё визуально/локально
    [ClientRpc]
    private void RpcOnRespawn(int loadoutIndex, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        RestoreCameraAfterDeath();
        RestoreBodyPose();

        if (deathMenuController != null)
            deathMenuController.HideDeathMenu();

        // CharacterController is still disabled since death — safe to move transform
        // directly here without fighting physics/collisions.
        // CharacterController всё ещё выключен с момента смерти - можно спокойно
        // переставить transform напрямую, не борясь с физикой/коллизиями.
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (playerMovement != null) playerMovement.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;
        if (characterController != null) characterController.enabled = true;

        // 1. FIRST, return the dropped weapon back into the player's hierarchy
        // 1. СНАЧАЛА возвращаем выброшенное оружие обратно в иерархию игрока
        if (droppedWeaponInstance != null)
        {
            droppedWeaponInstance.transform.SetParent(weaponOriginalParent, true);
            droppedWeaponInstance.transform.localPosition = weaponOriginalLocalPosition;
            droppedWeaponInstance.transform.localRotation = weaponOriginalLocalRotation;

            // Remove the Rigidbody we added when it fell // Убираем физику (Rigidbody), которую накинули при падении
            Rigidbody rb = droppedWeaponInstance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Destroy(rb);
            }

            // Disable colliders // Отключаем коллайдеры
            foreach (Collider col in droppedWeaponInstance.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            droppedWeaponInstance = null;
        }

        // 2. NOW re-enable all scripts and reset every weapon (including the one just returned)
        // 2. ТЕПЕРЬ включаем все скрипты и сбрасываем ВСЁ оружие (включая только что вернувшееся)
        foreach (WeaponController wc in GetComponentsInChildren<WeaponController>(true))
        {
            if (wc != null)
            {
                wc.enabled = true;
                wc.ResetWeapon();
            }
        }

        foreach (WeaponMovement wm in GetComponentsInChildren<WeaponMovement>(true))
            if (wm != null) wm.enabled = true;

        foreach (WeaponADS ads in GetComponentsInChildren<WeaponADS>(true))
            if (ads != null) ads.enabled = true;

        foreach (RecoilHandler rh in GetComponentsInChildren<RecoilHandler>(true))
            if (rh != null) rh.enabled = true;

        // 3. FINALLY set up the chosen class/loadout // 3. В КОНЦЕ настраиваем выбранный класс/лоадаут
        if (weaponSwitcher != null)
        {
            if (loadoutIndex >= 0)
                weaponSwitcher.SetClass(loadoutIndex); // Player explicitly picked a class // Игрок явно выбрал класс
            else
                weaponSwitcher.ReapplyCurrentLoadout(); // Keep whatever was equipped before // Оставляем то, что было раньше
        }

        isDead = false;
    }

    public void RestoreCameraAfterDeath()
    {
        if (cameraTransform == null || !isCameraFollowingBody)
            return;

        if (originalCameraParent != null)
        {
            cameraTransform.SetParent(originalCameraParent, true);
            cameraTransform.localPosition = originalCameraLocalPosition;
            cameraTransform.localRotation = originalCameraLocalRotation;
        }

        isCameraFollowingBody = false;
    }

    // ==================== WEAPON DROP ====================
    // ==================== ВЫБРОС ОРУЖИЯ ====================

    // Detaches the weapon from the player, gives it physics, and launches it slightly
    // Отвязывает оружие от игрока, даёт ему физику и слегка подбрасывает
    private void DropWeapon(WeaponController weapon, Vector3 fallDirXZ)
    {
        Transform weaponTransform = weapon.transform;

        // Remember original parent/position so it can be restored exactly on respawn
        // Запоминаем оригинального родителя и позицию, чтобы точно вернуть при респавне
        weaponOriginalParent = weaponTransform.parent;
        weaponOriginalLocalPosition = weaponTransform.localPosition;
        weaponOriginalLocalRotation = weaponTransform.localRotation;

        // Detach from the player, keeping world position/rotation — the weapon stays
        // exactly where it was in the hand.
        // Отвязываем от игрока, сохраняя мировую позицию/поворот - оружие
        // остаётся лежать там же, где было в руке.
        weaponTransform.SetParent(null, true);

        Rigidbody rb = weaponTransform.GetComponent<Rigidbody>();
        if (rb == null)
            rb = weaponTransform.gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearDamping = 1.2f;   // Slows linear movement over time, like air resistance // Гасит линейное движение со временем, как сопротивление воздуха
        rb.angularDamping = 2.5f;  // Slows spin over time // Гасит вращение со временем
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Smooths visual motion between physics steps // Сглаживает визуальное движение между физическими шагами
        rb.maxAngularVelocity = 4f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Prevents tunneling through thin colliders at speed // Предотвращает пролёт сквозь тонкие коллайдеры на скорости

        weaponTransform.position += Vector3.up * 0.04f;
        weaponTransform.position += fallDirXZ * 0.03f; // Small nudge away from the body to avoid instantly re-colliding // Маленький сдвиг от тела, чтобы не столкнуться сразу же

        EnsureDroppedWeaponColliders(weaponTransform);
        EnsureFittedCollider(weaponTransform);
        IgnoreCollisionsWithPlayer(weaponTransform);

        // Impulse is modest with little upward force — previously the weapon got too
        // strong a vertical kick and flew off like a rocket.
        // Импульс скромный и без большого "вверх" - раньше пушка получала
        // слишком сильный вертикальный толчок и улетала как ракета.
        Vector3 dropImpulse = fallDirXZ * weaponDropImpulse + Vector3.up * (weaponDropImpulse * 0.12f);

        rb.AddForce(dropImpulse, ForceMode.Impulse);
        rb.AddTorque(new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)).normalized * weaponDropTorque,
            ForceMode.Impulse); // Random spin so it doesn't always tumble the same way // Случайное вращение, чтобы падало не всегда одинаково

        // The weapon disappears in sync with the corpse (fall + lying + despawn on
        // respawn). Previously it was destroyed on a hard timer (fallDuration +
        // corpseTime + sinkDuration) regardless of whether the player had respawned
        // yet — it could vanish right while you were sitting in the death menu.
        // Now it stays put until an actual respawn happens (see RpcOnRespawn).
        // Оружие исчезает синхронно с трупом (падение + лежание + уход при
        // респавне). Раньше оно удалялось по жёсткому таймеру независимо от
        // того, зареспавнился игрок или нет - могло исчезнуть прямо во время
        // сидения в меню смерти. Теперь лежит до реального респавна (см. RpcOnRespawn).
        droppedWeaponInstance = weaponTransform.gameObject;
    }

    // Fits a BoxCollider tightly around the weapon's actual mesh bounds
    // Подгоняет BoxCollider точно под реальные границы меша оружия
    private void EnsureFittedCollider(Transform weaponTransform)
    {
        if (weaponTransform.GetComponent<Collider>() != null)
            return; // Already has one // Уже есть

        Renderer[] renderers = weaponTransform.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            // No meshes found (unusual model) — use a small fallback collider so
            // physics doesn't behave unpredictably.
            // Мешей не нашли (странная модель) - ставим скромный коллайдер,
            // чтобы физика вообще не улетела в бесконечность.
            BoxCollider fallback = weaponTransform.gameObject.AddComponent<BoxCollider>();
            fallback.size = Vector3.one * 0.2f;
            return;
        }

        // Previously this always added a default 1x1x1 BoxCollider — if the weapon
        // model was smaller/larger than a meter, the collider didn't match the mesh,
        // and physics behaved erratically (clipped through geometry, pushed weirdly, etc).
        // Раньше тут всегда добавлялся дефолтный BoxCollider 1x1x1 -
        // если модель меньше/больше метра, коллайдер не совпадал с мешем,
        // и физика вела себя дико.
        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds); // Grow bounds to cover all sub-meshes // Расширяем границы, чтобы охватить все под-меши

        Vector3 lossyScale = weaponTransform.lossyScale;

        BoxCollider box = weaponTransform.gameObject.AddComponent<BoxCollider>();
        box.center = weaponTransform.InverseTransformPoint(bounds.center);

        // Convert world-space bounds size back into local scale space
        // Переводим размер границ из мирового пространства обратно в локальное
        Vector3 fittedSize = new Vector3(
            lossyScale.x != 0f ? bounds.size.x / lossyScale.x : bounds.size.x,
            lossyScale.y != 0f ? bounds.size.y / lossyScale.y : bounds.size.y,
            lossyScale.z != 0f ? bounds.size.z / lossyScale.z : bounds.size.z);

        // Slight inward margin (95%) — a collider flush with the mesh easily gets
        // stuck at the seam with the floor/other colliders right at spawn.
        // Небольшой запас внутрь (95%) - коллайдер впритык к мешу легко
        // застревает в стыке с полом/другими коллайдерами в момент спавна.
        box.size = fittedSize * 0.95f;
    }

    // Prevents the dropped weapon from colliding with the player's own body colliders
    // Не даёт выброшенному оружию сталкиваться с коллайдерами тела самого игрока
    private void IgnoreCollisionsWithPlayer(Transform weaponTransform)
    {
        Collider weaponCollider = weaponTransform.GetComponent<Collider>();
        if (weaponCollider == null)
            return;

        // The weapon spawns right against the player's body (Health hitboxes etc) —
        // without this, physics could "catch" it between its own collider and the
        // floor and it would just hang in place instead of falling.
        // Оружие спавнится вплотную к телу игрока (хитбоксы Health и т.п.) -
        // без этого физика может "поймать" его между своим же коллайдером
        // и полом, и оно просто зависнет вместо падения.
        foreach (Collider playerCollider in GetComponentsInChildren<Collider>(true))
        {
            if (playerCollider != null)
                Physics.IgnoreCollision(weaponCollider, playerCollider, true);
        }
    }
}