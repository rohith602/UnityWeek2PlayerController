using UnityEngine;

namespace Week2.Runtime
{
    /// <summary>
    /// Rigidbody based third person character controller.
    ///
    /// Deliberately a dynamic Rigidbody rather than Unity's CharacterController component:
    /// CharacterController bypasses the physics engine entirely, so it cannot push rigidbodies,
    /// be pushed by them, or resolve contacts the way the rest of the simulation does.
    ///
    /// All movement lives in <see cref="Tick"/> rather than directly in FixedUpdate, because
    /// Physics.Simulate() steps the solver but does NOT invoke FixedUpdate. Exposing Tick is the
    /// only way the headless physics tests can drive this controller at all.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 5f;
        public float sprintSpeed = 8.5f;
        public float groundAcceleration = 55f;
        public float groundDeceleration = 65f;
        public float airAcceleration = 16f;
        public float turnSpeed = 900f;          // degrees per second the model turns to face travel

        [Header("Jump")]
        public float jumpHeight = 1.7f;
        public float coyoteTime = 0.12f;        // grace after walking off a ledge
        public float jumpBufferTime = 0.12f;    // grace for pressing jump just before landing
        public float fallGravityMultiplier = 2.2f;
        public float lowJumpMultiplier = 2.6f;  // released early, so cut the arc short

        [Header("Ground")]
        public float groundCheckDistance = 0.30f;
        public float slopeLimit = 45f;
        public float groundStick = 12f;         // pushes into slopes so we do not skip off them
        public LayerMask groundMask = ~0;

        [Header("Physics interaction")]
        public float pushForce = 3.2f;

        public bool IsGrounded { get; private set; }
        public bool OnSteepSlope { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        /// <summary>Seconds of coyote grace left. Exposed so tests and the HUD can show it.</summary>
        public float CoyoteRemaining => _coyoteLeft;
        public Vector3 Velocity => _rb.linearVelocity;
        public Vector3 HorizontalVelocity { get { var v = _rb.linearVelocity; v.y = 0f; return v; } }

        /// <summary>Swap this for a ScriptedInputSource to drive the player from a test.</summary>
        public IInputSource Source { get; set; }

        /// <summary>If set, input is interpreted relative to this transform's yaw.</summary>
        public Transform cameraSpace;

        Rigidbody _rb;
        CapsuleCollider _capsule;
        readonly RaycastHit[] _hits = new RaycastHit[8];
        float _coyoteLeft, _bufferLeft, _jumpLock;
        MovingPlatform _platform;

        void Awake() => EnsureInit();

        /// <summary>
        /// Idempotent setup. Separate from Awake because Awake never runs in edit mode, and the
        /// headless physics tests drive this controller without ever entering play mode.
        /// </summary>
        public void EnsureInit()
        {
            if (_rb != null) return;

            _rb = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();

            _rb.freezeRotation = true;                  // we steer the model ourselves
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Source ??= new KeyboardInputSource();
        }

        void Update() => Source.Poll();

        void FixedUpdate() => Tick(Source.Read(), Time.fixedDeltaTime);

        /// <summary>One physics step of movement. Call this, then step the physics engine.</summary>
        public void Tick(MoveInput input, float dt)
        {
            EnsureInit();
            _jumpLock = Mathf.Max(0f, _jumpLock - dt);
            UpdateGround();

            _coyoteLeft = IsGrounded ? coyoteTime : Mathf.Max(0f, _coyoteLeft - dt);
            _bufferLeft = input.JumpPressed ? jumpBufferTime : Mathf.Max(0f, _bufferLeft - dt);

            Vector3 v = _rb.linearVelocity;

            // --- horizontal ---------------------------------------------------------------
            Vector3 wish = ToWorld(input.Move);
            float targetSpeed = (input.Sprint ? sprintSpeed : walkSpeed) * Mathf.Clamp01(input.Move.magnitude);
            Vector3 targetVel = wish * targetSpeed;

            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float accel = IsGrounded
                ? (targetSpeed > 0.01f ? groundAcceleration : groundDeceleration)
                : airAcceleration;
            flat = Vector3.MoveTowards(flat, targetVel, accel * dt);
            v.x = flat.x;
            v.z = flat.z;

            // --- jump ---------------------------------------------------------------------
            bool jumped = false;
            if (_bufferLeft > 0f && _coyoteLeft > 0f)
            {
                // v = sqrt(2gh) gives exactly the requested apex, so jumpHeight is in metres
                // rather than being an arbitrary impulse number that has to be tuned by feel.
                v.y = Mathf.Sqrt(2f * jumpHeight * -Physics.gravity.y);
                _bufferLeft = _coyoteLeft = 0f;
                IsGrounded = false;
                // Without this the next step is still inside groundCheckDistance, re-grounds,
                // and the ground-stick branch below overwrites the upward velocity we just set.
                _jumpLock = 0.12f;
                jumped = true;
            }

            // --- slopes and ground stick ---------------------------------------------------
            // v.y > 0 means we are rising, so do not re-project onto the ground plane.
            if (IsGrounded && !jumped && v.y <= 0.01f)
            {
                // Re-aim horizontal motion along the slope so speed is constant up and down hill,
                // instead of launching off the top of ramps or dragging going up them.
                Vector3 alongSlope = Vector3.ProjectOnPlane(new Vector3(v.x, 0f, v.z), GroundNormal);
                if (alongSlope.sqrMagnitude > 1e-6f)
                    alongSlope = alongSlope.normalized * flat.magnitude;
                v = alongSlope - GroundNormal * (groundStick * dt);
            }
            else
            {
                // Heavier falling and a clipped rise when jump is released early. Real gravity
                // alone reads as floaty; this is a game feel choice, documented in the README.
                if (v.y < 0f)
                    v += Physics.gravity * ((fallGravityMultiplier - 1f) * dt);
                else if (v.y > 0f && !input.JumpHeld)
                    v += Physics.gravity * ((lowJumpMultiplier - 1f) * dt);
            }

            // Sliding off anything steeper than slopeLimit, rather than sticking to walls.
            if (OnSteepSlope && !IsGrounded)
            {
                Vector3 down = Vector3.ProjectOnPlane(Physics.gravity, GroundNormal);
                v += down * dt;
            }

            _rb.linearVelocity = v;

            // --- ride moving platforms ------------------------------------------------------
            if (IsGrounded && _platform != null)
                _rb.position += _platform.Delta;

            // --- face travel direction ------------------------------------------------------
            if (wish.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(wish, Vector3.up);
                _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, want, turnSpeed * dt));
            }
        }

        Vector3 ToWorld(Vector2 move)
        {
            Vector3 raw = new Vector3(move.x, 0f, move.y);
            if (raw.sqrMagnitude > 1f) raw.Normalize();
            if (cameraSpace == null) return raw;

            // Flatten the camera basis so looking up or down never changes walking speed.
            Vector3 fwd = Vector3.ProjectOnPlane(cameraSpace.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cameraSpace.right, Vector3.up).normalized;
            return fwd * raw.z + right * raw.x;
        }

        void UpdateGround()
        {
            float half = Mathf.Max(_capsule.height * 0.5f, _capsule.radius);
            Vector3 bottom = transform.position + _capsule.center - Vector3.up * (half - _capsule.radius);
            float radius = _capsule.radius * 0.92f;
            Vector3 origin = bottom + Vector3.up * 0.05f;

            IsGrounded = false;
            OnSteepSlope = false;
            GroundNormal = Vector3.up;
            _platform = null;
            if (_jumpLock > 0f) return;   // just jumped: refuse to re-ground for a moment

            // SphereCastNonAlloc rather than SphereCast: the cast starts inside our own capsule,
            // so the nearest hit is frequently ourselves and a single-result cast would return it.
            int n = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _hits,
                                               groundCheckDistance + 0.05f, groundMask,
                                               QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance >= best) continue;

                best = h.distance;
                GroundNormal = h.normal;
                float angle = Vector3.Angle(h.normal, Vector3.up);
                IsGrounded = angle <= slopeLimit;
                OnSteepSlope = angle > slopeLimit;
                _platform = IsGrounded ? h.collider.GetComponentInParent<MovingPlatform>() : null;
            }
        }

        // Dynamic rigidbodies do get shoved by our capsule on their own, but a velocity driven
        // controller barely transfers momentum, so crates feel immovable without this nudge.
        void OnCollisionStay(Collision c)
        {
            var other = c.rigidbody;
            if (other == null || other.isKinematic) return;

            Vector3 dir = c.contacts[0].point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;

            other.AddForceAtPosition(dir.normalized * pushForce, c.contacts[0].point, ForceMode.Impulse);
        }
    }
}
