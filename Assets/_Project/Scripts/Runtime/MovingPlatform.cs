using UnityEngine;

namespace Week2.Runtime
{
    /// <summary>
    /// Kinematic platform that shuttles between two points. Exposes the movement it applied this
    /// step so a rider can add the same delta: a kinematic body does not drag anything standing
    /// on it through friction, so without this the player is simply left behind.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MovingPlatform : MonoBehaviour
    {
        public Vector3 travel = new Vector3(6f, 0f, 0f);
        public float period = 6f;

        /// <summary>World movement applied during the most recent step.</summary>
        public Vector3 Delta { get; private set; }

        Rigidbody _rb;
        Vector3 _origin;
        float _t;

        void Awake() => EnsureInit();

        /// <summary>Idempotent: Awake does not run in edit mode, where the tests drive this.</summary>
        public void EnsureInit()
        {
            if (_rb != null) return;
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _origin = _rb.position;
        }

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>Exposed for the same reason PlayerController.Tick is: tests step it by hand.</summary>
        public void Step(float dt)
        {
            EnsureInit();
            _t += dt;
            // PingPong on a cosine gives smooth ends instead of an instant reversal, which would
            // otherwise shove the rider sideways on every turnaround.
            float k = (1f - Mathf.Cos(_t / Mathf.Max(period, 0.01f) * 2f * Mathf.PI)) * 0.5f;
            Vector3 next = _origin + travel * k;
            Delta = next - _rb.position;
            _rb.MovePosition(next);
        }
    }
}
