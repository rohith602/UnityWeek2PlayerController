using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Week2.Runtime;

namespace Week2.EditorTools
{
    /// <summary>
    /// Builds the physics test course from scratch, deterministically.
    /// Tools menu, or headless via -executeMethod Week2.EditorTools.CourseBuilder.Build
    /// </summary>
    public static class CourseBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/PlayerCourse.unity";
        const string MatDir = "Assets/_Project/Art/Materials";
        const string CharacterFbx = "Assets/ThirdParty/Kenney_BlockyCharacters/Models/character-a.fbx";
        const string ShotDir = "Docs/screenshots";

        // The model is 2.70 units tall as authored; scale it to a 1.8 m person.
        const float CharacterNativeHeight = 2.70f;
        public const float PlayerHeight = 1.8f;
        public const float PlayerRadius = 0.38f;
        public static readonly Vector3 SpawnPoint = new Vector3(0f, 1.3f, -24f);

        static Material _matGround, _matRampOk, _matRampSteep, _matStep, _matWall, _matCrate, _matPlatform, _matGoal;

        [MenuItem("Tools/Week 2/Build Player Course")]
        public static void Build()
        {
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                BuildMaterials();
                BuildLighting();
                BuildGround();
                BuildRamps();
                BuildStairsAndCeiling();
                BuildGapJump();
                BuildCrates();
                BuildMovingPlatform();
                var player = BuildPlayer();
                BuildCamera(player.transform);
                player.AddComponent<ControlsHud>();

                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"COURSEBUILD_SUCCESS objects={Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length}");
            }
            catch (System.Exception e)
            {
                Debug.LogError("COURSEBUILD_FAILED " + e);
            }
        }

        // ------------------------------------------------------------------ materials

        static Material Mat(string name, Color c, float smooth = 0.12f)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", 0f);
            Directory.CreateDirectory(MatDir);
            AssetDatabase.CreateAsset(m, $"{MatDir}/M_{name}.mat");
            return AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_{name}.mat");
        }

        static void BuildMaterials()
        {
            // Colour codes function, so a screenshot of the course explains itself.
            _matGround = Mat("Ground", new Color(0.52f, 0.55f, 0.58f));
            _matRampOk = Mat("RampWalkable", new Color(0.30f, 0.62f, 0.38f));
            _matRampSteep = Mat("RampTooSteep", new Color(0.72f, 0.31f, 0.28f));
            _matStep = Mat("Steps", new Color(0.40f, 0.50f, 0.70f));
            _matWall = Mat("Wall", new Color(0.35f, 0.36f, 0.40f));
            _matCrate = Mat("Crate", new Color(0.78f, 0.56f, 0.26f));
            _matPlatform = Mat("Platform", new Color(0.62f, 0.40f, 0.72f));
            _matGoal = Mat("Goal", new Color(0.90f, 0.78f, 0.25f), 0.45f);
        }

        static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            if (rot.HasValue) go.transform.rotation = rot.Value;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        static void Label(string text, Vector3 pos)
        {
            var go = new GameObject("Label_" + text.Replace(" ", ""));
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 72;
            tm.characterSize = 0.16f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.08f, 0.08f, 0.10f);
            // Arial.ttf was removed from builtin resources; LegacyRuntime.ttf is its replacement.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        // ------------------------------------------------------------------ world

        static void BuildLighting()
        {
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.03f);
            sky.SetFloat("_AtmosphereThickness", 0.9f);
            sky.SetColor("_SkyTint", new Color(0.48f, 0.60f, 0.80f));
            sky.SetColor("_GroundColor", new Color(0.62f, 0.66f, 0.70f));
            sky.SetFloat("_Exposure", 1.15f);
            Directory.CreateDirectory(MatDir);
            AssetDatabase.CreateAsset(sky, $"{MatDir}/M_Sky.mat");

            var sunGo = new GameObject("Sun_Directional");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.90f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.shadowNormalBias = 0.4f;
            sunGo.transform.rotation = Quaternion.Euler(48f, 150f, 0f);

            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_Sky.mat");
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.66f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.52f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.28f, 0.28f);
            RenderSettings.ambientIntensity = 0.95f;
            RenderSettings.fog = false;
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
        }

        static void BuildGround()
        {
            // A thick box rather than a Plane: a Plane has a zero-thickness collider, which a fast
            // moving body can tunnel through.
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(70f, 1f, 80f), _matGround);
            Box("Wall_North", new Vector3(0f, 2f, 40f), new Vector3(70f, 5f, 1f), _matWall);
            Box("Wall_South", new Vector3(0f, 2f, -40f), new Vector3(70f, 5f, 1f), _matWall);
            Box("Wall_East", new Vector3(35f, 2f, 0f), new Vector3(1f, 5f, 80f), _matWall);
            Box("Wall_West", new Vector3(-35f, 2f, 0f), new Vector3(1f, 5f, 80f), _matWall);
            Label("START", SpawnPoint + new Vector3(0f, 1.6f, -2f));
        }

        static void BuildRamps()
        {
            // 55 degrees is deliberately past the controller's 45 degree slope limit, so the
            // course itself demonstrates the limit rather than the README merely claiming it.
            float[] angles = { 15f, 30f, 40f, 55f };
            float[] xs = { -12f, -4f, 4f, 12f };
            const float len = 9f;

            for (int i = 0; i < angles.Length; i++)
            {
                float a = angles[i];
                bool walkable = a <= 45f;
                var rot = Quaternion.Euler(-a, 0f, 0f);
                var pos = new Vector3(xs[i], Mathf.Sin(a * Mathf.Deg2Rad) * len * 0.5f, -14f);
                Box($"Ramp_{a:0}deg", pos, new Vector3(6f, 0.5f, len), walkable ? _matRampOk : _matRampSteep, rot);
                Label($"{a:0} deg" + (walkable ? "" : "  (too steep)"),
                      new Vector3(xs[i], 0.4f, -19.2f));
            }
        }

        static void BuildStairsAndCeiling()
        {
            const float stepH = 0.28f, stepD = 0.9f;
            for (int i = 0; i < 6; i++)
                Box($"Step_{i}", new Vector3(-12f, stepH * (i + 0.5f), -4f + i * stepD),
                    new Vector3(6f, stepH, stepD), _matStep);
            Label("STAIRS", new Vector3(-12f, 0.4f, -6.2f));

            Box("Ceiling_LowBar", new Vector3(0f, 2.0f, -3f), new Vector3(7f, 0.4f, 1.2f), _matWall);
            Box("Ceiling_PostA", new Vector3(-3.3f, 1.0f, -3f), new Vector3(0.4f, 2f, 1.2f), _matWall);
            Box("Ceiling_PostB", new Vector3(3.3f, 1.0f, -3f), new Vector3(0.4f, 2f, 1.2f), _matWall);
            Label("HEAD BUMP", new Vector3(0f, 2.6f, -3f));

            Box("Wall_Blocker", new Vector3(12f, 1.5f, -4f), new Vector3(7f, 3f, 1f), _matWall);
            Label("WALL", new Vector3(12f, 3.2f, -4f));
        }

        static void BuildGapJump()
        {
            Box("Jump_PlatformA", new Vector3(0f, 1.0f, 6f), new Vector3(8f, 2f, 6f), _matStep);
            Box("Jump_PlatformB", new Vector3(0f, 1.0f, 16f), new Vector3(8f, 2f, 6f), _matStep);
            Label("GAP JUMP", new Vector3(0f, 2.4f, 3.2f));
        }

        static void BuildCrates()
        {
            float[] masses = { 1f, 6f, 25f };
            for (int i = 0; i < masses.Length; i++)
            {
                var go = Box($"Crate_{masses[i]:0}kg", new Vector3(-14f + i * 3.5f, 0.6f, 8f),
                             Vector3.one * 1.2f, _matCrate);
                GameObjectUtility.SetStaticEditorFlags(go, 0);   // dynamic, so clear static flags
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = masses[i];
                rb.linearDamping = 0.6f;
                rb.angularDamping = 0.9f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                Label($"{masses[i]:0} kg", new Vector3(-14f + i * 3.5f, 1.35f, 8f));
            }
            Label("PUSHABLE CRATES", new Vector3(-10.5f, 3.6f, 8f));
        }

        static void BuildMovingPlatform()
        {
            var go = Box("MovingPlatform", new Vector3(12f, 1.0f, 10f), new Vector3(5f, 0.5f, 5f), _matPlatform);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var mp = go.AddComponent<MovingPlatform>();
            mp.travel = new Vector3(0f, 0f, 12f);
            mp.period = 8f;
            Label("MOVING PLATFORM", new Vector3(12f, 2.2f, 7f));

            Box("Goal_Pad", new Vector3(12f, 0.1f, 26f), new Vector3(6f, 0.2f, 6f), _matGoal);
            Label("GOAL", new Vector3(12f, 0.6f, 26f));
        }

        // ------------------------------------------------------------------ player

        static GameObject BuildPlayer()
        {
            var root = new GameObject("Player");
            root.transform.position = SpawnPoint;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = PlayerHeight;
            capsule.radius = PlayerRadius;
            capsule.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            // Low friction: the controller sets velocity directly, so collider friction fighting
            // it just makes the player stick to walls it brushes against.
            var pm = new PhysicsMaterial("PM_Player")
            {
                dynamicFriction = 0.02f,
                staticFriction = 0.02f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(pm, $"{MatDir}/PM_Player.physicMaterial");
            capsule.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>($"{MatDir}/PM_Player.physicMaterial");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 70f;
            rb.useGravity = true;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            root.AddComponent<PlayerController>();

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
            if (src != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
                model.name = "Model";
                float s = PlayerHeight / CharacterNativeHeight;
                model.transform.localScale = Vector3.one * s;
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                var anim = model.AddComponent<CharacterAnimator>();
                anim.controller = root.GetComponent<PlayerController>();
            }
            else Debug.LogWarning("Character model missing: " + CharacterFbx);

            return root;
        }

        static void BuildCamera(Transform player)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 400f;

            var follow = go.AddComponent<FollowCamera>();
            follow.target = player;
            follow.distance = 7.5f;
            follow.startPitch = 16f;

            go.transform.position = player.position + new Vector3(0f, 3f, -7.5f);
            go.transform.rotation = Quaternion.Euler(16f, 0f, 0f);
        }
    }
}
