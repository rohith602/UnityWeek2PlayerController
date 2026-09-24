using UnityEngine;

namespace Week2.Runtime
{
    /// <summary>One frame of player intent, independent of where it came from.</summary>
    public struct MoveInput
    {
        public Vector2 Move;       // x = strafe, y = forward, each in [-1, 1]
        public bool JumpPressed;   // edge: true only on the frame the key went down
        public bool JumpHeld;      // level: true while held, drives variable jump height
        public bool Sprint;

        public static MoveInput None => new MoveInput();
        public static MoveInput Forward => new MoveInput { Move = new Vector2(0f, 1f) };
    }

    /// <summary>
    /// Where a <see cref="MoveInput"/> comes from. The whole point of this interface is that the
    /// physics tests can substitute a scripted source and drive the controller with no keyboard.
    /// </summary>
    public interface IInputSource
    {
        /// <summary>
        /// Called from Update. Edge-triggered input (GetButtonDown) is only true for a single
        /// render frame, but the controller runs in FixedUpdate, which may not run that frame at
        /// all, or may run twice. Polling in Update and latching here is what stops jumps being
        /// dropped or double counted.
        /// </summary>
        void Poll();

        /// <summary>Called from FixedUpdate. Consumes any latched edge input.</summary>
        MoveInput Read();
    }

    public sealed class KeyboardInputSource : IInputSource
    {
        bool _jumpLatched;

        public void Poll()
        {
            if (Input.GetButtonDown("Jump")) _jumpLatched = true;
        }

        public MoveInput Read()
        {
            var m = new MoveInput
            {
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                JumpPressed = _jumpLatched,
                JumpHeld = Input.GetButton("Jump"),
                Sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
            };
            _jumpLatched = false;
            return m;
        }
    }

    /// <summary>Test double: the harness sets <see cref="Next"/> and steps the controller by hand.</summary>
    public sealed class ScriptedInputSource : IInputSource
    {
        public MoveInput Next;

        public void Poll() { }

        public MoveInput Read()
        {
            var m = Next;
            Next.JumpPressed = false;   // an edge fires once, same as the real thing
            return m;
        }

        public void PressJump() => Next.JumpPressed = Next.JumpHeld = true;
        public void ReleaseJump() => Next.JumpHeld = false;
    }
}
