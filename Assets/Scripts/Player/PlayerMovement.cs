using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

/// <summary>
/// FPS-контроллер движения в стиле CS/Source: скорость не задаётся напрямую,
/// а разгоняется/тормозится через Acceleration/Friction. Даёт инерцию,
/// воздушный контроль и задел под bhop/slide/dash в будущем.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    // ==================== GROUND MOVEMENT ====================
    [Header("Ground Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [Tooltip("Как быстро набираем целевую скорость на земле")]
    [SerializeField] private float groundAcceleration = 10f;
    [Tooltip("Торможение, когда input отпущен, но ещё есть скорость")]
    [SerializeField] private float groundDeceleration = 10f;
    [Tooltip("Трение, съедающее скорость каждый кадр на земле")]
    [SerializeField] private float groundFriction = 6f;
    [Tooltip("Ниже этого значения скорость просто обнуляется, чтобы не 'скользить' вечно")]
    [SerializeField] private float stopSpeedThreshold = 1f;

    // ==================== AIR MOVEMENT ====================
    [Header("Air Movement")]
    [Tooltip("Ускорение в воздухе (может быть выше ground - это даёт air-strafe контроль)")]
    [SerializeField] private float airAcceleration = 20f;
    [Tooltip("Максимальная скорость, которую можно НАБРАТЬ в воздухе за счёт wish-direction (классика Quake/CS air control)")]
    [SerializeField] private float airWishSpeedCap = 1.5f;
    [Tooltip("Множитель отзывчивости поворота в воздухе (0 = нет контроля, 1 = полный)")]
    [SerializeField, Range(0f, 1f)] private float airControl = 1f;

    // ==================== JUMP / GRAVITY ====================
    [Header("Jump & Gravity")]
    [SerializeField] private float jumpForce = 6.5f;
    [SerializeField] private float gravity = -20f;
    [Tooltip("Сколько секунд после схода с земли ещё можно прыгнуть")]
    [SerializeField] private float coyoteTime = 0.12f;
    [Tooltip("Сколько секунд запомненный прыжок ждёт приземления")]
    [SerializeField] private float jumpBufferTime = 0.12f;

    // ==================== CROUCH ====================
    [Header("Crouch Settings")]
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float standHeight = 2.0f;
    [SerializeField] private float crouchLerpSpeed = 10f;
    [SerializeField] private float crouchCamOffset = 0.5f;
    [SerializeField] private float ceilingCheckExtraOffset = 0.05f;
    [SerializeField] private LayerMask ceilingCheckMask = ~0;
    [SerializeField] private NetworkPlayerState networkPlayerState;

    // ==================== VISUAL BODY ====================
    [Header("Visual Body")]
    [SerializeField] private Transform bodyVisual;
    public Transform BodyVisual => bodyVisual;

    // ==================== HEAD BOB ====================
    [Header("Camera Shake (Head Bob)")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float bobSpeed = 14f;
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float runBobMultiplier = 1.5f;

    // ==================== REFERENCES ====================
    [Header("References")]
    [SerializeField] private MouseLook mouseLook;

    // ==================== COMPONENTS / INPUT ====================
    private CharacterController _controller;
    private PlayerInputActions _inputActions;

    // ==================== INPUT STATE ====================
    private Vector2 _moveInput;
    private bool _isRunning;
    private bool _isCrouching;      // хочет ли игрок присесть (зажата кнопка)
    private bool _isActuallyCrouching; // реально присел (в т.ч. вынужденно из-за потолка)

    // ==================== MOVEMENT STATE ====================
    private Vector3 _horizontalVelocity;  // скорость по XZ
    private float _verticalVelocity;      // скорость по Y (гравитация/прыжок)
    private bool _isGrounded;

    // ==================== JUMP TIMERS ====================
    private float _coyoteTimeCounter;
    private float _jumpBufferCounter;

    // ==================== HEAD BOB STATE ====================
    private float _bobTimer;
    private float _defaultCamY;
    private float _currentCamCrouchOffset;

    // ==================== CROUCH CACHE ====================
    private float _originalHeight;
    private float _originalCenterY;

    #region Unity Lifecycle

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        _originalHeight = _controller.height;
        _originalCenterY = _controller.center.y;

        _defaultCamY = cameraTransform.localPosition.y;

        _inputActions = new PlayerInputActions();

        _inputActions.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
        _inputActions.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

        _inputActions.Player.Look.performed += ctx => mouseLook.Rotate(ctx.ReadValue<Vector2>());

        // Прыжок не прыгает сразу — только "запоминается" на jumpBufferTime.
        // Реальный прыжок происходит в HandleJump(), когда есть право (земля/coyote).
        _inputActions.Player.Jump.performed += ctx => _jumpBufferCounter = jumpBufferTime;

        _inputActions.Player.Sprint.performed += ctx => _isRunning = true;
        _inputActions.Player.Sprint.canceled += ctx => _isRunning = false;

        _inputActions.Player.Crouch.performed += ctx =>
        {
            _isCrouching = true;

            if (networkPlayerState != null)
                networkPlayerState.SetCrouch(true);
        };

        _inputActions.Player.Crouch.canceled += ctx =>
        {
            _isCrouching = false;

            if (networkPlayerState != null)
                networkPlayerState.SetCrouch(false);
        };
    }

    private void Start()
    {
        // Камера и звук выключены у всех по умолчанию.
        // OnStartLocalPlayer() включит их только для ТВОЕГО игрока — иначе будешь видеть/слышать мир
        // глазами последнего заспавнившегося клиента, а не своими.
        SetCameraAndAudioEnabled(false);
    }

    // Mirror вызывает это ТОЛЬКО на объекте, которым управляет именно этот клиент
    public override void OnStartLocalPlayer()
    {
        _inputActions.Player.Enable();
        SetCameraAndAudioEnabled(true);
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            // У remote-игроков позицию двигает NetworkTransform, а не физика.
            // Локально нам нужно только докрутить визуал приседа.
            HandleCrouch();
            return;
        }

        ReadInput();
        UpdateGroundState();

        HandleCrouch();
        HandleJump();
        HandleGravity();

        if (_isGrounded)
            HandleGroundMovement();
        else
            HandleAirMovement();

        ApplyMovement();
        HandleHeadBob();
    }

    private void OnEnable()
    {
        // Симметрично OnDisable(): при смерти PlayerDeath делает enabled = false,
        // что выключает Input Actions карту через OnDisable(). Без этого OnEnable()
        // после респавна (enabled = true) карта так и оставалась выключенной навсегда,
        // и игрок терял управление после первой же смерти.
        // isLocalPlayer-проверка обязательна: иначе на remote-инстансах чужих игроков
        // тоже начнёт читаться ТВОЯ клавиатура/мышь, ломая их движение.
        if (_inputActions != null && isLocalPlayer)
            _inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
    }

    #endregion

    #region Input & Ground State

    private void ReadInput()
    {
        // Ввод уже приходит через Input Actions события (_moveInput, _isRunning, _isCrouching).
        // Здесь place для будущей логики (например, отмена спринта при приседе).
        if (_isActuallyCrouching)
            _isRunning = false;
    }

    private void UpdateGroundState()
    {
        _isGrounded = _controller.isGrounded;

        if (_isGrounded)
            _coyoteTimeCounter = coyoteTime;
        else
            _coyoteTimeCounter -= Time.deltaTime;

        if (_jumpBufferCounter > 0f)
            _jumpBufferCounter -= Time.deltaTime;
    }

    #endregion

    #region Ground / Air Movement

    private Vector3 GetWishDirection()
    {
        Vector3 wishDir =
            transform.right * _moveInput.x +
            transform.forward * _moveInput.y;

        wishDir.y = 0f;
        return wishDir.normalized;
    }

    private float GetTargetSpeed()
    {
        if (_isActuallyCrouching) return crouchSpeed;
        return _isRunning ? sprintSpeed : walkSpeed;
    }

    private void HandleGroundMovement()
    {
        ApplyFriction();

        Vector3 wishDir = GetWishDirection();
        float wishSpeed = GetTargetSpeed();

        Accelerate(wishDir, wishSpeed, groundAcceleration);
    }

    private void HandleAirMovement()
    {
        Vector3 wishDir = GetWishDirection();

        // В воздухе скорость не может расти сколько угодно — классический
        // air-strafe cap: чем меньше airWishSpeedCap, тем "честнее" физика,
        // чем больше — тем легче будет крутить strafe-jump в будущем.
        float wishSpeed = Mathf.Min(GetTargetSpeed(), airWishSpeedCap / Mathf.Max(airControl, 0.0001f));

        Accelerate(wishDir, wishSpeed, airAcceleration * airControl);
    }

    /// <summary>
    /// Классический Quake/Source-style accelerate: разгоняем _horizontalVelocity
    /// в сторону wishDir, но не даём превысить wishSpeed вдоль этого направления.
    /// </summary>
    private void Accelerate(Vector3 wishDir, float wishSpeed, float acceleration)
    {
        if (wishDir.sqrMagnitude < 0.0001f)
            return;

        float currentSpeedInWishDir = Vector3.Dot(_horizontalVelocity, wishDir);
        float addSpeed = wishSpeed - currentSpeedInWishDir;

        if (addSpeed <= 0f)
            return;

        float accelSpeed = acceleration * wishSpeed * Time.deltaTime;
        accelSpeed = Mathf.Min(accelSpeed, addSpeed);

        _horizontalVelocity += wishDir * accelSpeed;
    }

    private void ApplyFriction()
    {
        float speed = _horizontalVelocity.magnitude;

        if (speed < 0.0001f)
        {
            _horizontalVelocity = Vector3.zero;
            return;
        }

        // stopSpeedThreshold не даёт трению быть "линейным в ноль" бесконечно медленно —
        // на низких скоростях персонаж тормозит чуть резче, как в Source.
        float control = speed < stopSpeedThreshold ? stopSpeedThreshold : speed;
        float drop = control * groundFriction * Time.deltaTime;

        float newSpeed = Mathf.Max(speed - drop, 0f);
        newSpeed /= speed;

        _horizontalVelocity *= newSpeed;
    }

    private void ApplyMovement()
    {
        Vector3 fullVelocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
        CollisionFlags flags = _controller.Move(fullVelocity * Time.deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
            _verticalVelocity = 0f;
    }

    #endregion

    #region Jump & Gravity

    private void HandleJump()
    {
        bool canJump = _coyoteTimeCounter > 0f;
        bool wantsJump = _jumpBufferCounter > 0f;

        if (canJump && wantsJump)
        {
            _verticalVelocity = jumpForce;

            // Сбрасываем оба таймера, чтобы не спрыгнуть дважды с одного приземления
            _jumpBufferCounter = 0f;
            _coyoteTimeCounter = 0f;
        }
    }

    private void HandleGravity()
    {
        if (_isGrounded && _verticalVelocity < 0f)
        {
            // Небольшое отрицательное значение держит controller "прижатым" к земле,
            // иначе isGrounded будет дёргаться на неровностях.
            _verticalVelocity = -2f;
        }
        else
        {
            _verticalVelocity += gravity * Time.deltaTime;
        }
    }

    #endregion

    #region Crouch

    private void HandleCrouch()
    {
        bool wantsCrouch = _isCrouching;

        if (!wantsCrouch && !CanStandUp())
            wantsCrouch = true;

        _isActuallyCrouching = wantsCrouch;

        float targetHeight = wantsCrouch ? crouchHeight : standHeight;

        _controller.height = Mathf.Lerp(
            _controller.height,
            targetHeight,
            Time.deltaTime * crouchLerpSpeed);

        float bottomOffset = _originalCenterY - _originalHeight / 2f;

        _controller.center =
            new Vector3(0, _controller.height / 2f + bottomOffset, 0);

        if (bodyVisual != null)
        {
            float scaleRatio = _controller.height / _originalHeight;

            bodyVisual.localScale = new Vector3(
                bodyVisual.localScale.x,
                scaleRatio,
                bodyVisual.localScale.z);

            float halfHeight = scaleRatio * (_originalHeight / 2f);

            bodyVisual.localPosition = new Vector3(
                bodyVisual.localPosition.x,
                bottomOffset + halfHeight,
                bodyVisual.localPosition.z);
        }
    }

    private bool CanStandUp()
    {
        float bottomOffset = _originalCenterY - _originalHeight / 2f;
        float radius = _controller.radius;

        Vector3 bottomPoint =
            transform.position + Vector3.up * (bottomOffset + radius + 0.02f);

        Vector3 topPoint =
            transform.position + Vector3.up * (bottomOffset + standHeight - radius);

        float checkRadius = radius - ceilingCheckExtraOffset;

        return !Physics.CheckCapsule(
            bottomPoint,
            topPoint,
            checkRadius,
            ceilingCheckMask,
            QueryTriggerInteraction.Ignore);
    }

    /// <summary>Вызывается NetworkPlayerState на remote-клиентах при синхронизации crouch-флага.</summary>
    public void SetNetworkCrouch(bool state)
    {
        _isCrouching = state;

        if (!isLocalPlayer)
        {
            ApplyCrouchVisual(state);
        }
    }

    private void ApplyCrouchVisual(bool crouching)
    {
        float targetHeight = crouching ? crouchHeight : standHeight;

        _controller.height = targetHeight;

        float bottomOffset = _originalCenterY - _originalHeight / 2f;

        _controller.center =
            new Vector3(0, _controller.height / 2f + bottomOffset, 0);

        if (bodyVisual != null)
        {
            float scaleRatio = _controller.height / _originalHeight;

            bodyVisual.localScale = new Vector3(
                bodyVisual.localScale.x,
                scaleRatio,
                bodyVisual.localScale.z);

            float halfHeight = scaleRatio * (_originalHeight / 2f);

            bodyVisual.localPosition = new Vector3(
                bodyVisual.localPosition.x,
                bottomOffset + halfHeight,
                bodyVisual.localPosition.z);
        }
    }

    #endregion

    #region Head Bob

    private void HandleHeadBob()
    {
        float targetOffset = _isActuallyCrouching ? -crouchCamOffset : 0f;

        _currentCamCrouchOffset = Mathf.Lerp(
            _currentCamCrouchOffset,
            targetOffset,
            Time.deltaTime * crouchLerpSpeed);

        float baseY = _defaultCamY + _currentCamCrouchOffset;

        // Боб теперь завязан на реальную горизонтальную скорость, а не на raw input —
        // с инерцией это выглядит естественнее (не дёргается на старте/торможении).
        float speedRatio = _horizontalVelocity.magnitude / Mathf.Max(walkSpeed, 0.001f);

        if (_isGrounded && speedRatio > 0.15f)
        {
            float speedMultiplier = _isRunning ? runBobMultiplier : 1f;

            _bobTimer += Time.deltaTime * bobSpeed * speedMultiplier;

            float yOffset = Mathf.Sin(_bobTimer) * bobAmount * speedMultiplier;

            cameraTransform.localPosition = new Vector3(0, baseY + yOffset, 0);
        }
        else
        {
            _bobTimer = 0f;

            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                new Vector3(0, baseY, 0),
                Time.deltaTime * 10f);
        }
    }

    #endregion

    #region Helpers

    private void SetCameraAndAudioEnabled(bool enabled)
    {
        if (cameraTransform == null) return;

        Camera cam = cameraTransform.GetComponent<Camera>();
        if (cam != null) cam.enabled = enabled;

        AudioListener listener = cameraTransform.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = enabled;
    }

    #endregion
}