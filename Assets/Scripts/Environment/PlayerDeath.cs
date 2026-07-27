using System.Collections;
using UnityEngine;
using Mirror;

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
    [SerializeField] private float sinkDuration = 1.5f;
    [SerializeField] private float sinkDistance = 1.3f;

    [Header("Fall Settings")]
    [Tooltip("На сколько тело 'проскальзывает' по направлению пули, пока падает")]
    [SerializeField] private float fallSlideDistance = 0.6f;
    [Tooltip("Доп. зазор поверх толщины тела (радиуса капсулы), чтобы труп не проваливался в землю")]
    [SerializeField] private float corpseGroundOffset = 0.05f;
    [Tooltip("Слои, которые считаются 'землёй' для приземления трупа")]
    [SerializeField] private LayerMask groundMask = ~0;
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

    // Исходная поза body (bodyVisual) до падения - нужна, чтобы вернуть труп
    // в нормальную стоячую позу при респавне (иначе персонаж навсегда остаётся лежать).
    private Vector3 bodyOriginalLocalPosition;
    private Quaternion bodyOriginalLocalRotation;
    private Vector3 bodyOriginalLocalScale;
    private bool bodyOriginalCached;

    private void Awake()
    {
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

    [Server]
    public void ServerDie(Vector3 hitDirection = default)
    {
        // Если направление явно не передали - берём последнее сохранённое в Health
        // (оно валидно именно на сервере, т.к. TakeDamage выполняется только там).
        if (hitDirection == Vector3.zero && health != null)
            hitDirection = health.LastHitDirection;

        RpcDie(hitDirection);
    }

    [Header("References")]
    [SerializeField] private DeathMenuController deathMenuController;

    [ClientRpc]
    private void RpcDie(Vector3 hitDirection)
    {
        if (isDead) return;
        isDead = true;

        if (health != null)
            health.SetHealth(0);

        if (deathMenuController != null)
            deathMenuController.ShowDeathMenu();

        StartCoroutine(DeathRoutine(hitDirection));
    }

    private IEnumerator DeathRoutine(Vector3 hitDirection)
    {
        // Отключаем управление
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

        // Камера привязывается к телу напрямую, чтобы падать и поворачиваться вместе с ним.
        Coroutine cameraRoutine = StartCoroutine(CameraFollowBody(body));

        yield return StartCoroutine(FallToGround(body, fallDirXZ));

        yield return new WaitForSeconds(corpseTime);
    }

    // ==================== ОТКЛЮЧЕНИЕ ОРУЖИЯ ====================

    private void DisableBodyColliders(WeaponController droppedWeapon)
    {
        Transform excludedRoot = droppedWeapon != null ? droppedWeapon.transform : null;

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider == null)
                continue;

            if (excludedRoot != null && collider.transform.IsChildOf(excludedRoot))
                continue;

            collider.enabled = false;
        }

        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb == null)
                continue;

            if (excludedRoot != null && rb.transform.IsChildOf(excludedRoot))
                continue;

            rb.isKinematic = true;
        }
    }

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
                continue;

            if (activeWeapon == null && wc.gameObject.activeInHierarchy)
                activeWeapon = wc;

            wc.enabled = false;
        }

        if (activeWeapon == null)
        {
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

        // Прячем всё оружие, кроме того, что будет выброшено (DropWeapon) -
        // иначе второй слот (например, пистолет) остаётся SetActive(true)
        // и виден в руках трупа, будто игрок его "достал" сам.
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

    // ==================== ПАДЕНИЕ ТЕЛА ====================

    private Vector3 GetFallDirectionXZ(Vector3 hitDirection, Transform body)
    {
        // Всегда падаем на спину: используем направление, противоположное взгляду тела.
        Vector3 fallbackDir = -body.forward;

        if (hitDirection != Vector3.zero)
        {
            Vector3 dir = Vector3.ProjectOnPlane(hitDirection, Vector3.up);
            if (dir.sqrMagnitude > 0.01f)
                fallbackDir = -dir;
        }

        return fallbackDir.normalized;
    }

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

            float t = Mathf.SmoothStep(0f, 1f, time / fallDuration);

            body.position = Vector3.Lerp(startPos, endPos, t);
            body.rotation = Quaternion.Slerp(startRot, endRot, t);

            yield return null;
        }

        body.position = endPos;
        body.rotation = endRot;
    }

    private Vector3 CalculateLandingPosition(Vector3 startPos, Vector3 fallDirXZ)
    {
        Vector3 slidPos = startPos + fallDirXZ * fallSlideDistance;

        float groundY = startPos.y;

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

    private float GetCorpseRestHeight()
    {
        // Когда тело ложится плашмя, его пивот (обычно центр меша) оказывается
        // примерно на высоте радиуса капсулы над землёй - а не почти вплотную к ней,
        // как было раньше с фиксированным маленьким отступом (труп проваливался в пол).
        float baseRadius = characterController != null ? characterController.radius : 0.4f;
        return baseRadius + corpseGroundOffset;
    }

    private Quaternion CalculateLandingRotation(Quaternion startRot, Vector3 fallDirXZ)
    {
        // Заваливаем тело вокруг оси, перпендикулярной направлению падения -
        // как срубленное дерево, прямо от текущего поворота. Раньше поворот
        // строился с нуля через LookRotation, из-за чего Slerp прокручивал
        // тело через лишний разворот по yaw (выглядело как штопор).
        //
        // Если модель заваливается "не в ту сторону" (например, спиной вперёд
        // вместо лицом) - смени знак угла ниже с 90f на -90f.
        Vector3 toppleAxis = Vector3.Cross(Vector3.up, fallDirXZ);

        if (toppleAxis.sqrMagnitude < 0.0001f)
            toppleAxis = Vector3.right;

        toppleAxis.Normalize();

        Quaternion toppleRotation = Quaternion.AngleAxis(90f, toppleAxis);

        return toppleRotation * startRot;
    }

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

    // ==================== КАМЕРА СМЕРТИ ====================

    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPosition;
    private Quaternion originalCameraLocalRotation;
    private bool isCameraFollowingBody;

    private IEnumerator CameraFollowBody(Transform body)
    {
        if (cameraTransform == null || body == null)
            yield break;

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

    [Command]
    public void CmdRequestRespawn(int loadoutIndex)
    {
        if (health != null)
            health.Respawn();

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

    [ClientRpc]
    private void RpcOnRespawn(int loadoutIndex, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        RestoreCameraAfterDeath();
        RestoreBodyPose();

        if (deathMenuController != null)
            deathMenuController.HideDeathMenu();

        // CharacterController всё ещё выключен с момента смерти - можно спокойно
        // переставить transform напрямую, не борясь с физикой/коллизиями.
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (playerMovement != null) playerMovement.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;
        if (characterController != null) characterController.enabled = true;

        foreach (WeaponController wc in GetComponentsInChildren<WeaponController>(true))
            if (wc != null) wc.enabled = true;

        foreach (WeaponMovement wm in GetComponentsInChildren<WeaponMovement>(true))
            if (wm != null) wm.enabled = true;

        foreach (WeaponADS ads in GetComponentsInChildren<WeaponADS>(true))
            if (ads != null) ads.enabled = true;

        foreach (RecoilHandler rh in GetComponentsInChildren<RecoilHandler>(true))
            if (rh != null) rh.enabled = true;

        if (weaponSwitcher != null)
        {
            // loadoutIndex >= 0 - игрок выбрал класс в меню смерти (ChangeLodautMenu).
            // -1 - ничего не выбирал, просто восстанавливаем тот же лоадаут, что был.
            if (loadoutIndex >= 0)
                weaponSwitcher.SetClass(loadoutIndex);
            else
                weaponSwitcher.ReapplyCurrentLoadout();
        }

        if (droppedWeaponInstance != null)
        {
            Destroy(droppedWeaponInstance);
            droppedWeaponInstance = null;
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

    // ==================== ОРУЖИЕ ====================

    private void DropWeapon(WeaponController weapon, Vector3 fallDirXZ)
    {
        Transform weaponTransform = weapon.transform;

        // Отвязываем от игрока, сохраняя мировую позицию/поворот -
        // оружие остаётся лежать там же, где было в руке.
        weaponTransform.SetParent(null, true);

        Rigidbody rb = weaponTransform.GetComponent<Rigidbody>();
        if (rb == null)
            rb = weaponTransform.gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearDamping = 1.2f;
        rb.angularDamping = 2.5f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxAngularVelocity = 4f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        weaponTransform.position += Vector3.up * 0.04f;
        weaponTransform.position += fallDirXZ * 0.03f;

        EnsureDroppedWeaponColliders(weaponTransform);
        EnsureFittedCollider(weaponTransform);
        IgnoreCollisionsWithPlayer(weaponTransform);

        // Импульс скромный и без большого "вверх" - раньше пушка получала
        // слишком сильный вертикальный толчок и улетала как ракета
        Vector3 dropImpulse = fallDirXZ * weaponDropImpulse + Vector3.up * (weaponDropImpulse * 0.12f);

        rb.AddForce(dropImpulse, ForceMode.Impulse);
        rb.AddTorque(new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)).normalized * weaponDropTorque,
            ForceMode.Impulse);

        // Оружие исчезает синхронно с трупом (падение + лежание + уход под землю)
        // Раньше оружие удалялось по жёсткому таймеру (fallDuration + corpseTime + sinkDuration)
        // независимо от того, зареспавнился игрок или нет - могло исчезнуть прямо
        // во время того, как ты сидишь в меню смерти. Теперь оно лежит до тех пор,
        // пока не произойдёт реальный респавн (см. RpcOnRespawn).
        droppedWeaponInstance = weaponTransform.gameObject;
    }

    private void EnsureFittedCollider(Transform weaponTransform)
    {
        if (weaponTransform.GetComponent<Collider>() != null)
            return;

        Renderer[] renderers = weaponTransform.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            // Мешей не нашли (странная модель) - ставим скромный коллайдер,
            // чтобы физика вообще не улетела в бесконечность
            BoxCollider fallback = weaponTransform.gameObject.AddComponent<BoxCollider>();
            fallback.size = Vector3.one * 0.2f;
            return;
        }

        // Раньше тут всегда добавлялся дефолтный BoxCollider 1x1x1 -
        // если модель оружия меньше/больше метра, коллайдер не совпадал
        // с мешем, и физика вела себя дико (пробивало геометрию, отталкивало и т.п.)
        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 lossyScale = weaponTransform.lossyScale;

        BoxCollider box = weaponTransform.gameObject.AddComponent<BoxCollider>();
        box.center = weaponTransform.InverseTransformPoint(bounds.center);

        Vector3 fittedSize = new Vector3(
            lossyScale.x != 0f ? bounds.size.x / lossyScale.x : bounds.size.x,
            lossyScale.y != 0f ? bounds.size.y / lossyScale.y : bounds.size.y,
            lossyScale.z != 0f ? bounds.size.z / lossyScale.z : bounds.size.z);

        // Небольшой запас внутрь (95%) - коллайдер впритык к мешу легко
        // застревает в стыке с полом/другими коллайдерами в момент спавна.
        box.size = fittedSize * 0.95f;
    }

    private void IgnoreCollisionsWithPlayer(Transform weaponTransform)
    {
        Collider weaponCollider = weaponTransform.GetComponent<Collider>();
        if (weaponCollider == null)
            return;

        // Оружие спавнится вплотную к телу игрока (хитбоксы Health и т.п.) -
        // без этого физика может "поймать" его между своим же коллайдером
        // и полом и оно просто зависает на месте вместо падения.
        foreach (Collider playerCollider in GetComponentsInChildren<Collider>(true))
        {
            if (playerCollider != null)
                Physics.IgnoreCollision(weaponCollider, playerCollider, true);
        }
    }
}