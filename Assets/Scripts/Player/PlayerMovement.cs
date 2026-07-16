using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float jumpForce = 6.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Crouch Settings")]
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float standHeight = 2.0f;
    [SerializeField] private float crouchLerpSpeed = 10f;
    [SerializeField] private float crouchCamOffset = 0.5f;
    [SerializeField] private float ceilingCheckExtraOffset = 0.05f;
    [SerializeField] private LayerMask ceilingCheckMask = ~0;
    [SerializeField] private NetworkPlayerState networkPlayerState;

    [Header("Visual Body")]
    [SerializeField] private Transform bodyVisual;

    [Header("Camera Shake (Head Bob)")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float bobSpeed = 14f;
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float runBobMultiplier = 1.5f;

    [Header("References")]
    [SerializeField] private MouseLook mouseLook;

    private CharacterController _controller;
    private PlayerInputActions _inputActions;

    private Vector2 _moveInput;
    private Vector3 _velocity;

    private bool _isCrouching;
    private bool _isRunning;
    private bool _isActuallyCrouching;

    private float _timer;
    private float _defaultCamY;
    private float _currentCamCrouchOffset;

    private float _originalHeight;
    private float _originalCenterY;

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

        _inputActions.Player.Jump.performed += ctx => Jump();

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

    private void SetCameraAndAudioEnabled(bool enabled)
    {
        if (cameraTransform == null) return;

        Camera cam = cameraTransform.GetComponent<Camera>();
        if (cam != null) cam.enabled = enabled;

        AudioListener listener = cameraTransform.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = enabled;
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            HandleCrouch();
            return;
        }

        if (_controller.isGrounded && _velocity.y < 0)
            _velocity.y = -2f;

        HandleCrouch();
        Move();
        HeadBob();
        ApplyGravity();
    }

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

    private void Move()
    {
        float currentSpeed =
            _isActuallyCrouching
                ? crouchSpeed
                : (_isRunning ? runSpeed : walkSpeed);

        Vector3 moveDirection =
            transform.right * _moveInput.x +
            transform.forward * _moveInput.y;

        _controller.Move(moveDirection * currentSpeed * Time.deltaTime);
    }

    private void HeadBob()
    {
        float targetOffset =
            _isActuallyCrouching ? -crouchCamOffset : 0f;

        _currentCamCrouchOffset = Mathf.Lerp(
            _currentCamCrouchOffset,
            targetOffset,
            Time.deltaTime * crouchLerpSpeed);

        float baseY = _defaultCamY + _currentCamCrouchOffset;

        if (_moveInput.magnitude > 0.1f)
        {
            float speedMultiplier = _isRunning ? runBobMultiplier : 1f;

            _timer += Time.deltaTime * bobSpeed * speedMultiplier;

            float yOffset =
                Mathf.Sin(_timer) * bobAmount * speedMultiplier;

            cameraTransform.localPosition =
                new Vector3(0, baseY + yOffset, 0);
        }
        else
        {
            _timer = 0;

            cameraTransform.localPosition =
                Vector3.Lerp(
                    cameraTransform.localPosition,
                    new Vector3(0, baseY, 0),
                    Time.deltaTime * 10f);
        }
    }

    private void Jump()
    {
        if (!isLocalPlayer) return;

        if (_controller.isGrounded)
        {
            _velocity.y = jumpForce;
        }
    }

    private void ApplyGravity()
    {
        _velocity.y += gravity * Time.deltaTime;

        CollisionFlags flags = _controller.Move(_velocity * Time.deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0)
        {
            _velocity.y = 0f;
        }
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
    }

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
            new Vector3(
                0,
                _controller.height / 2f + bottomOffset,
                0);


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
}