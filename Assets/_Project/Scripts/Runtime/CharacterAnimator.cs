using UnityEngine;

namespace Week2.Runtime
{
    /// <summary>
    /// Procedural walk cycle. The Kenney character is rigged as six separate parts with their
    /// pivots already at the joints, but ships no animation clips, so rather than author or
    /// download any, the limbs are driven straight from the simulated velocity.
    ///
    /// For a physics task this is arguably the more honest choice than a canned clip: stride
    /// rate is literally a function of how fast the Rigidbody is actually moving, so it can never
    /// drift out of sync with the motion the way a fixed-speed clip does.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        public PlayerController controller;

        [Header("Stride")]
        public float swingPerSpeed = 7f;      // degrees of limb swing per m/s
        public float maxSwing = 55f;
        public float stridePerSpeed = 1.5f;   // cycles per second per m/s
        public float settle = 12f;            // how fast the pose eases toward its target

        [Header("Body")]
        public float bobHeight = 0.045f;
        public float leanPerSpeed = 1.1f;     // degrees of forward lean per m/s
        public float maxLean = 14f;
        public float airTuck = 28f;           // legs tuck up while airborne

        Transform _legL, _legR, _armL, _armR, _torso;
        Vector3 _torsoHome;
        float _phase, _swing, _lean, _tuck;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PlayerController>();

            _legL = Find("leg-left");
            _legR = Find("leg-right");
            _armL = Find("arm-left");
            _armR = Find("arm-right");
            _torso = Find("torso");
            if (_torso != null) _torsoHome = _torso.localPosition;
        }

        Transform Find(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
            return null;   // parts are optional: a different character model just animates less
        }

        void LateUpdate()
        {
            if (controller == null) return;
            float dt = Time.deltaTime;

            float speed = controller.HorizontalVelocity.magnitude;
            bool grounded = controller.IsGrounded;

            // Advance the stride by distance travelled, not by wall-clock time, so the feet keep
            // pace with the body at any speed.
            if (grounded) _phase += speed * stridePerSpeed * dt * Mathf.PI * 2f;

            float wantSwing = grounded ? Mathf.Min(speed * swingPerSpeed, maxSwing) : 0f;
            float wantLean = Mathf.Min(speed * leanPerSpeed, maxLean);
            float wantTuck = grounded ? 0f : airTuck;

            float k = 1f - Mathf.Exp(-settle * dt);
            _swing = Mathf.Lerp(_swing, wantSwing, k);
            _lean = Mathf.Lerp(_lean, wantLean, k);
            _tuck = Mathf.Lerp(_tuck, wantTuck, k);

            float s = Mathf.Sin(_phase) * _swing;

            // Arms counter-swing against the legs, which is what makes a walk read as a walk.
            if (_legL != null) _legL.localRotation = Quaternion.Euler(s - _tuck, 0f, 0f);
            if (_legR != null) _legR.localRotation = Quaternion.Euler(-s - _tuck, 0f, 0f);
            if (_armL != null) _armL.localRotation = Quaternion.Euler(-s * 0.7f, 0f, 0f);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(s * 0.7f, 0f, 0f);

            if (_torso != null)
            {
                // Two bobs per stride: the body rises on each footfall, not once per full cycle.
                float bob = Mathf.Abs(Mathf.Cos(_phase)) * bobHeight * (_swing / Mathf.Max(maxSwing, 0.01f));
                _torso.localPosition = _torsoHome + Vector3.up * bob;
                _torso.localRotation = Quaternion.Euler(_lean, 0f, 0f);
            }
        }
    }
}
