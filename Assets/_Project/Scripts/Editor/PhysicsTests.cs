using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Week2.Runtime;

namespace Week2.EditorTools
{
    /// <summary>
    /// Headless behavioural tests for the player controller.
    ///
    /// A screenshot cannot prove that movement, jumping or collision work, so this drives the
    /// controller with a ScriptedInputSource and asserts on measured position and velocity.
    /// Every case prints the numbers it measured, so a near miss is visible rather than just red.
    ///
    /// Physics.Simulate steps the solver but does NOT call FixedUpdate, which is exactly why
    /// PlayerController.Tick is public: the loop below is Tick, then Simulate, by hand.
    ///
    ///   Unity.exe -projectPath . -batchmode -quit \
    ///     -executeMethod Week2.EditorTools.PhysicsTests.RunAll -logFile tests.log
    /// </summary>
    public static class PhysicsTests
    {
        const float DT = 0.02f;                    // matches the default fixed timestep
        static PlayerController _player;
        static Rigidbody _rb;
        static ScriptedInputSource _input;
        static MovingPlatform[] _platforms;
        static int _pass, _fail;

        [MenuItem("Tools/Week 2/Run Physics Tests")]
        public static void RunAll()
        {
            _pass = _fail = 0;
            var previousMode = Physics.simulationMode;

            try
            {
                EditorSceneManager.OpenScene(CourseBuilder.ScenePath);
                _player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (_player == null) { Debug.LogError("PHYSICS_TESTS_FAILED no PlayerController"); return; }

                _rb = _player.GetComponent<Rigidbody>();
                _input = new ScriptedInputSource();
                _player.Source = _input;
                _player.EnsureInit();
                _platforms = UnityEngine.Object.FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None);

                // Take manual control of the solver; otherwise nothing steps in edit mode.
                Physics.simulationMode = SimulationMode.Script;

                GravitySettles();
                WalkSpeedMatchesConfig();
                SprintIsFaster();
                WallBlocksMovement();
                JumpReachesConfiguredHeight();
                CoyoteTimeAllowsLateJump();
                WalksUpShallowSlope();
                DoesNotClimbSteepSlope();
                PushesCrate();
                SurvivesLongFallWithoutTunnelling();
                RidesMovingPlatform();

                string verdict = _fail == 0 ? "PHYSICS_TESTS_PASSED" : "PHYSICS_TESTS_FAILED";
                Debug.Log($"{verdict} {_pass}/{_pass + _fail}");
            }
            catch (Exception e)
            {
                Debug.LogError("PHYSICS_TESTS_FAILED exception " + e);
            }
            finally
            {
                Physics.simulationMode = previousMode;
            }
        }

        // ------------------------------------------------------------------ harness

        static void Reset(Vector3 pos)
        {
            _player.EnsureInit();
            _player.transform.SetPositionAndRotation(pos, Quaternion.identity);
            _rb.position = pos;
            _rb.rotation = Quaternion.identity;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _input.Next = MoveInput.None;
            Physics.SyncTransforms();
        }

        /// <summary>Tick the controller then step the solver, N times. onStep runs before each tick.</summary>
        static void Run(int steps, Action<int> onStep = null)
        {
            for (int i = 0; i < steps; i++)
            {
                onStep?.Invoke(i);
                _player.Tick(_input.Read(), DT);
                foreach (var p in _platforms) p.Step(DT);
                Physics.Simulate(DT);
            }
        }

        static void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; Debug.Log($"TEST PASS  {name,-34} {detail}"); }
            else { _fail++; Debug.LogError($"TEST FAIL  {name,-34} {detail}"); }
        }

        static Vector3 Pos => _rb.position;

        // ------------------------------------------------------------------ cases

        static void GravitySettles()
        {
            Reset(CourseBuilder.SpawnPoint + Vector3.up * 4f);
            Run(200);
            bool ok = _player.IsGrounded && Mathf.Abs(_rb.linearVelocity.y) < 0.6f && Pos.y > -0.5f && Pos.y < 1.5f;
            Check("gravity settles on ground", ok, $"y={Pos.y:0.000} vy={_rb.linearVelocity.y:0.000} grounded={_player.IsGrounded}");
        }

        static void WalkSpeedMatchesConfig()
        {
            Reset(CourseBuilder.SpawnPoint);
            _input.Next = MoveInput.Forward;
            Run(100);                                   // reach steady state
            Vector3 a = Pos;
            Run(50);                                    // measure over exactly 1.0 s
            float speed = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(Pos.x, 0, Pos.z)) / (50 * DT);
            bool ok = Mathf.Abs(speed - _player.walkSpeed) < _player.walkSpeed * 0.15f;
            Check("walk speed matches config", ok, $"measured={speed:0.00} expected={_player.walkSpeed:0.00} m/s");
        }

        static void SprintIsFaster()
        {
            Reset(CourseBuilder.SpawnPoint);
            _input.Next = new MoveInput { Move = new Vector2(0f, 1f), Sprint = true };
            Run(100);
            Vector3 a = Pos;
            Run(50);
            float speed = Vector3.Distance(new Vector3(a.x, 0, a.z), new Vector3(Pos.x, 0, Pos.z)) / (50 * DT);
            bool ok = speed > _player.walkSpeed * 1.2f;
            Check("sprint is faster than walk", ok, $"sprint={speed:0.00} walk={_player.walkSpeed:0.00} m/s");
        }

        static void WallBlocksMovement()
        {
            // Wall_Blocker sits at z = -4 and is 1 deep, so its near face is z = -4.5.
            Reset(new Vector3(12f, 1.3f, -9f));
            _input.Next = MoveInput.Forward;
            Run(200);
            bool ok = Pos.z < -4.4f && Pos.z > -7f;
            Check("wall blocks movement", ok, $"z={Pos.z:0.000} (wall face at -4.5, start -9)");
        }

        static void JumpReachesConfiguredHeight()
        {
            Reset(CourseBuilder.SpawnPoint);
            Run(40);                                    // settle first
            float ground = Pos.y;
            float peak = ground;

            _input.PressJump();
            Run(120, _ => { if (Pos.y > peak) peak = Pos.y; });

            float height = peak - ground;
            bool ok = height > _player.jumpHeight * 0.75f && height < _player.jumpHeight * 1.3f;
            Check("jump reaches configured height", ok, $"peak={height:0.000} m target={_player.jumpHeight:0.000} m");
        }

        static void CoyoteTimeAllowsLateJump()
        {
            // Walk off the edge of Jump_PlatformA (top y = 2, far edge z = 9), then jump while
            // already airborne. Without coyote time this jump is simply ignored, so a pass here
            // genuinely exercises the grace window rather than ordinary grounded jumping.
            const int stepsLate = 3;                 // 0.06 s after leaving the ledge
            Reset(new Vector3(0f, 2.3f, 7.5f));
            Run(30);
            _input.Next = MoveInput.Forward;

            bool left = false, jumped = false;
            int sinceLeft = 0;
            float coyoteAtJump = -1f, peakVy = float.MinValue;

            Run(140, _ =>
            {
                if (!_player.IsGrounded)
                {
                    if (!left) { left = true; sinceLeft = 0; }
                    else if (!jumped && ++sinceLeft >= stepsLate)
                    {
                        coyoteAtJump = _player.CoyoteRemaining;
                        _input.PressJump();
                        jumped = true;
                    }
                }
                if (jumped) peakVy = Mathf.Max(peakVy, _rb.linearVelocity.y);
            });

            bool ok = left && jumped && peakVy > 2f;
            Check("coyote time allows late jump", ok,
                  $"airborne={left} jumpedAfter={stepsLate * DT:0.00}s coyoteLeft={coyoteAtJump:0.000}s peakVy={peakVy:0.00} m/s");
        }

        static void WalksUpShallowSlope()
        {
            Reset(new Vector3(-4f, 0.4f, -19.5f));      // base of the 30 degree ramp
            float y0 = Pos.y, peak = y0;
            _input.Next = MoveInput.Forward;
            Run(220, _ => peak = Mathf.Max(peak, Pos.y));
            float gained = peak - y0;
            bool ok = gained > 1.0f;
            Check("walks up 30 degree slope", ok, $"peak height gained={gained:0.000} m");
        }

        static void DoesNotClimbSteepSlope()
        {
            Reset(new Vector3(12f, 0.4f, -19.5f));      // base of the 55 degree ramp
            float y0 = Pos.y, peak = y0;
            _input.Next = MoveInput.Forward;
            Run(220, _ => peak = Mathf.Max(peak, Pos.y));
            float gained = peak - y0;
            bool ok = gained < 1.0f;
            Check("does NOT climb 55 degree slope", ok, $"peak height gained={gained:0.000} m (limit {_player.slopeLimit:0} deg)");
        }

        static void PushesCrate()
        {
            var crate = GameObject.Find("Crate_1kg");
            if (crate == null) { Check("pushes crate", false, "Crate_1kg not found"); return; }
            var crb = crate.GetComponent<Rigidbody>();
            Vector3 before = crb.position;

            Reset(new Vector3(-14f, 1.3f, 4.5f));
            _input.Next = MoveInput.Forward;
            Run(220);

            float moved = Vector3.Distance(new Vector3(before.x, 0, before.z),
                                           new Vector3(crb.position.x, 0, crb.position.z));
            bool ok = moved > 0.3f;
            Check("pushes crate", ok, $"crate moved={moved:0.000} m (mass {crb.mass:0} kg)");
        }

        static void SurvivesLongFallWithoutTunnelling()
        {
            Reset(new Vector3(0f, 45f, -24f));
            float fastest = 0f;
            Run(400, _ => fastest = Mathf.Min(fastest, _rb.linearVelocity.y));
            bool ok = Pos.y > -0.5f && _player.IsGrounded;
            Check("long fall does not tunnel", ok, $"final y={Pos.y:0.000} peak fall speed={fastest:0.0} m/s grounded={_player.IsGrounded}");
        }

        static void RidesMovingPlatform()
        {
            var plat = GameObject.Find("MovingPlatform");
            if (plat == null) { Check("rides moving platform", false, "MovingPlatform not found"); return; }

            Reset(plat.transform.position + Vector3.up * 1.2f);
            Run(30);                                   // land on it
            Vector3 p0 = Pos, q0 = plat.transform.position;
            Run(120);                                  // no input at all
            float playerMoved = Pos.z - p0.z;
            float platMoved = plat.transform.position.z - q0.z;

            bool ok = Mathf.Abs(platMoved) > 0.5f && Mathf.Abs(playerMoved - platMoved) < 0.6f;
            Check("rides moving platform", ok, $"player dz={playerMoved:0.000} platform dz={platMoved:0.000}");
        }

        // ------------------------------------------------------------------ trajectory capture

        /// <summary>
        /// Samples a real jump and drops a marker at each physics step, so the arc in the
        /// screenshot is measured data rather than an illustration.
        /// </summary>
        [MenuItem("Tools/Week 2/Visualise Jump Arc")]
        public static void VisualiseJumpArc()
        {
            var previousMode = Physics.simulationMode;
            try
            {
                EditorSceneManager.OpenScene(CourseBuilder.ScenePath);
                _player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                _rb = _player.GetComponent<Rigidbody>();
                _input = new ScriptedInputSource();
                _player.Source = _input;
                _player.EnsureInit();
                _platforms = UnityEngine.Object.FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None);
                Physics.simulationMode = SimulationMode.Script;

                Reset(new Vector3(-24f, 1.3f, -31f));
                Run(30);
                var samples = new List<Vector3>();
                _input.Next = new MoveInput { Move = new Vector2(0f, 1f), Sprint = true };
                _input.PressJump();
                Run(110, _ => samples.Add(Pos));

                var root = new GameObject("JumpArcSamples");
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Goal.mat");
                for (int i = 0; i < samples.Count; i += 2)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    s.name = $"t_{i * DT:0.00}s";
                    s.transform.SetParent(root.transform);
                    s.transform.position = samples[i] + Vector3.up * (CourseBuilder.PlayerHeight * 0.5f);
                    s.transform.localScale = Vector3.one * 0.14f;
                    UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
                    if (mat != null) s.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }

                float peak = 0f;
                foreach (var s in samples) peak = Mathf.Max(peak, s.y);
                Debug.Log($"JUMPARC samples={samples.Count} peak={peak:0.000} span={(samples[samples.Count - 1] - samples[0]).magnitude:0.00} m");
                // Deliberately NOT saved: the markers are for the screenshot only and must not
                // end up in the scene the evaluator opens.
                Debug.Log("JUMPARC_SUCCESS");
            }
            finally { Physics.simulationMode = previousMode; }
        }
    }
}
