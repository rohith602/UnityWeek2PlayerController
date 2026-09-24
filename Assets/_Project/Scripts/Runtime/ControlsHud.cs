using UnityEngine;

namespace Week2.Runtime
{
    /// <summary>
    /// On screen controls list and a live physics readout. Uses OnGUI deliberately: it needs no
    /// Canvas, no UI package and no font asset, which keeps the project's dependencies at zero
    /// for what is only a debug overlay.
    /// </summary>
    public class ControlsHud : MonoBehaviour
    {
        public bool show = true;
        PlayerController _player;
        GUIStyle _style;

        void Awake() => _player = GetComponent<PlayerController>();

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) show = !show;
        }

        void OnGUI()
        {
            if (!show) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14, richText = false };

            GUILayout.BeginArea(new Rect(12f, 12f, 320f, 210f), GUI.skin.box);
            GUILayout.Label("WASD / arrows  move", _style);
            GUILayout.Label("Space  jump   (hold for height)", _style);
            GUILayout.Label("Shift  sprint", _style);
            GUILayout.Label("Mouse  look        F1  hide", _style);

            if (_player != null)
            {
                GUILayout.Space(8f);
                Vector3 v = _player.Velocity;
                GUILayout.Label($"speed    {_player.HorizontalVelocity.magnitude,6:0.00} m/s", _style);
                GUILayout.Label($"vertical {v.y,6:0.00} m/s", _style);
                GUILayout.Label($"grounded {_player.IsGrounded}", _style);
                GUILayout.Label($"slope    {Vector3.Angle(_player.GroundNormal, Vector3.up),6:0.0} deg", _style);
            }
            GUILayout.EndArea();
        }
    }
}
