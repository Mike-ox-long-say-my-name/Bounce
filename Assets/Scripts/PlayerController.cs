using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Velocity properties")]
    [SerializeField] private bool gravityEnabled = true;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float minVerticalVelocity = -20f;
    [SerializeField, Min(0)] private float speed = 4f;
    [SerializeField, Range(0, 1)] private float airAccelerationFactor = 0.6f;

    [Header("Jump properties")]
    [SerializeField] private bool jumpEnabled = true;
    [SerializeField, Min(0)] private float jumpStartSpeed = 5;
    [SerializeField, Range(0, 1)] private float jumpAbortFactor = 0.6f;
    [SerializeField, Range(0, 1)] private float jumpBufferTime = 0.15f;
    [SerializeField, Range(0, 0.3f)] private float coyoteTime = 0.08f;

    [Space]
    [Header("Ground/Ceil/Wall checking properties")]
    [SerializeField] private LayerMask contactCheckMask;
    [SerializeField, Range(0, 1)] private float minContactAngle = 0.7f;
    [SerializeField, Range(0, 0.2f)] private float contactRaycastSizeAndDistance = 0.05f;
    [SerializeField, Range(0, 0.2f)] private float contactPositionEpsilon = 0.025f;
    [SerializeField, Min(1)] private int contactsBufferSize = 6;

    [Space]
    [Header("Camera settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private CameraFollowMode cameraFollowMode;
    [SerializeField, Range(0, 1)] private float followSmoothness;

    [Space]
    [Header("Player components")]
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private CircleCollider2D playerCollider;

    [Space]
    [Header("Other")]
    [SerializeField] private bool isSpriteInitiallyFlipped;
    [SerializeField] private bool debugFeaturesEnabled = true;

    private bool _isGrounded;
    private float _moveInput;
    private Vector2 _velocity;
    private RaycastHit2D[] _contacts;
    private Vector2 _awakePosition;

    public bool IsGrounded => _isGrounded;

    public bool GravityEnabled
    {
        get => gravityEnabled;
        set => gravityEnabled = value;
    }

    public float Gravity
    {
        get => gravity;
        set => gravity = value;
    }

    public bool JumpEnabled
    {
        get => jumpEnabled;
        set => jumpEnabled = value;
    }

    private readonly TimedTrigger _jumpWaitTrigger = new TimedTrigger();
    private readonly Trigger _jumpAbortTrigger = new Trigger();
    private readonly TimedTrigger _coyoteTimeTrigger = new TimedTrigger();

    private enum CameraFollowMode
    {
        Raw, Lerp, DoNotFollow
    }

    private enum ContactCheckDirection
    {
        Up, Down
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        var input = context.ReadValue<Vector2>();
        _moveInput = input.x;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!jumpEnabled)
        {
            return;
        }
        if (context.action.IsPressed())
        {
            InitiateJump();
        }
        else
        {
            AbortJump();
        }
    }

    private Vector2 GetWorldMousePosition()
    {
        var screenPosition = Mouse.current.position.ReadValue();
        return playerCamera.ScreenToWorldPoint(screenPosition);
    }

    private Bounds GetActualColliderBounds()
    {
        var rawBounds = playerCollider.bounds;
        return rawBounds;
    }

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        _awakePosition = playerRigidbody.position;
        _contacts = new RaycastHit2D[contactsBufferSize];
    }

    private void FixedUpdate()
    {
        HandledDebugging();

        HandleBoundsContacts();
        ApplyMoveInput();
        ApplyGravity();
        HandleJumping();
        ApplyVelocityToRigidbody();

        if (cameraFollowMode == CameraFollowMode.Lerp)
        {
            MoveCamera();
        }

        _jumpWaitTrigger.Step(Time.fixedDeltaTime);
        _coyoteTimeTrigger.Step(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        if (cameraFollowMode != CameraFollowMode.Lerp)
        {
            MoveCamera();
        }
    }

    private void HandledDebugging()
    {
        if (!debugFeaturesEnabled)
        {
            return;
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            playerRigidbody.position = _awakePosition;
        }

#if UNITY_EDITOR
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
#endif
    }

    private void ApplyMoveInput()
    {
        Walk(_moveInput);
    }

    private bool CheckBoundsContact(ContactCheckDirection direction)
    {
        var bounds = GetActualColliderBounds();
        var castOrigin = direction == ContactCheckDirection.Down ?
            new Vector2(bounds.min.x + bounds.extents.x, bounds.min.y) :
            new Vector2(bounds.max.x - bounds.extents.x, bounds.max.y);
        var castDirection = direction == ContactCheckDirection.Down ? Vector2.down : Vector2.up;

        var contactCount = Physics2D.BoxCastNonAlloc(castOrigin,
            new Vector2(bounds.size.x, contactRaycastSizeAndDistance),
            0, castDirection,
            _contacts, contactRaycastSizeAndDistance, contactCheckMask);
        return _contacts
            .Take(contactCount)
            .Where(contact =>
            (contact.normal.y >= minContactAngle && direction == ContactCheckDirection.Down) ||
            (contact.normal.y <= -minContactAngle && direction == ContactCheckDirection.Up))
            .Any(contact => Mathf.Abs(contact.point.y - castOrigin.y) < contactPositionEpsilon);
    }

    private void HandleBoundsContacts()
    {
        if (_velocity.y < 0)
        {
            var hasContact = CheckBoundsContact(ContactCheckDirection.Down);
            if (_isGrounded && !hasContact)
            {
                _coyoteTimeTrigger.SetFor(coyoteTime);
            }

            _isGrounded = hasContact;
            if (_isGrounded)
            {
                _velocity.y = 0;
            }
        }

        if (_velocity.y < 0)
        {
            return;
        }

        if (CheckBoundsContact(ContactCheckDirection.Up))
        {
            _velocity.y = 0;
        }
    }

    private void OnDrawGizmos()
    {
        if (playerCollider == null)
        {
            return;
        }
        Gizmos.color = _isGrounded ? Color.red : Color.green;
        Gizmos.DrawCube(new Vector3(playerRigidbody.position.x, GetActualColliderBounds().min.y, 0), new Vector3(0.3f, 0.05f, 0));
    }

    private void ApplyGravity()
    {
        if (!gravityEnabled)
        {
            return;
        }
        _velocity.y += gravity * Time.fixedDeltaTime;
    }

    private void MoveCamera()
    {
        var player = playerRigidbody.position;
        var cameraTransform = playerCamera.transform;
        var cameraPosition = cameraTransform.position;
        var target = new Vector3(player.x, player.y, cameraPosition.z);

        cameraTransform.position = cameraFollowMode switch
        {
            CameraFollowMode.Raw => target,
            CameraFollowMode.Lerp => Vector3.Lerp(cameraPosition, target, followSmoothness),
            CameraFollowMode.DoNotFollow => cameraPosition,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private void ApplyVelocityToRigidbody()
    {
        if (_velocity.y < minVerticalVelocity)
        {
            _velocity.y = minVerticalVelocity;
        }
        playerRigidbody.velocity = _velocity;
    }

    private void HandleJumping()
    {
        if (_jumpWaitTrigger.IsFree)
        {
            return;
        }

        if (!_isGrounded && _coyoteTimeTrigger.IsFree)
        {
            return;
        }

        _jumpWaitTrigger.Reset();
        _coyoteTimeTrigger.Reset();

        Jump();
        if (_jumpAbortTrigger.CheckAndReset())
        {
            AbortJump();
        }
    }

    private void InitiateJump()
    {
        _jumpWaitTrigger.SetFor(jumpBufferTime);
        _jumpAbortTrigger.Reset();
    }

    private void AbortJump()
    {
        if (_velocity.y > 0)
        {
            _velocity.y *= jumpAbortFactor;
            _jumpWaitTrigger.Reset();
        }
        else
        {
            _jumpAbortTrigger.Set();
        }
    }

    private void Jump()
    {
        _velocity.y = jumpStartSpeed;
        _isGrounded = false;
    }

    private void Walk(float direction)
    {
        _velocity.x = speed * direction;
        if (!_isGrounded)
        {
            _velocity.x *= airAccelerationFactor;
        }

        if (Mathf.Abs(direction) > 0)
        {
            LookTo(direction);
        }
    }

    private void LookTo(float direction)
    {
        Vector3 euler = transform.eulerAngles;
        euler.y = (direction > 0) != isSpriteInitiallyFlipped ? 180 : 0;
        transform.eulerAngles = euler;
    }
}