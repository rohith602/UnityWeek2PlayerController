using UnityEngine;

namespace Week2.Runtime
{
    /// <summary>
    /// Third person orbit camera. Runs in LateUpdate so it reads the player's final position for
    /// the frame, and damps its follow so the Rigidbody's physics stepping does not show through
    /// as camera jitter.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 pivotOffset = new Vector3(0f, 1.4f, 0f);
        public float distance = 7f;
        public float minPitch = -25f;
        public float maxPitch = 65f;
        public float mouseSensitivity = 220f;
        public float followDamping = 14f;
        public float startYaw = 0f;
        public float startPitch = 18f;
        public LayerMask obstructionMask = ~0;

        float _yaw, _pitch;
        Vector3 _pos;

        void Awake()
        {
            _yaw = startYaw;
            _pitch = startPitch;
            if (target != null) _pos = target.position + pivotOffset;
        }

        void LateUpdate()
        {
            if (target == null) return;

            _yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity * Time.deltaTime;
            _pitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivity * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Vector3 pivot = target.position + pivotOffset;
            _pos = Vector3.Lerp(_pos, pivot, 1f - Mathf.Exp(-followDamping * Time.deltaTime));

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 wanted = _pos - rot * Vector3.forward * distance;

            // Pull in if a wall is between the camera and the player, otherwise the view clips
            // through level geometry whenever the player backs into a corner.
            if (Physics.Linecast(_pos, wanted, out RaycastHit hit, obstructionMask, QueryTriggerInteraction.Ignore))
                wanted = hit.point + hit.normal * 0.2f;

            transform.SetPositionAndRotation(wanted, rot);
        }
    }
}
