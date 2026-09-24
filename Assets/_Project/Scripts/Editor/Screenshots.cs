using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Week2.EditorTools
{
    /// <summary>
    /// Renders the documentation images. Must run WITHOUT -nographics: cam.Render() into a
    /// RenderTexture segfaults the editor when the graphics device is null.
    /// </summary>
    public static class Screenshots
    {
        const string ShotDir = "Docs/screenshots";
        const int W = 1600, H = 900;

        [MenuItem("Tools/Week 2/Capture Screenshots")]
        public static void CaptureAll()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log("SHOTS_SKIPPED no graphics device");
                return;
            }

            EditorSceneManager.OpenScene(CourseBuilder.ScenePath);
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) { Debug.LogError("SHOTS_FAILED no camera"); return; }

            // The follow camera would fight us for control of the transform.
            var follow = cam.GetComponent<Week2.Runtime.FollowCamera>();
            if (follow != null) follow.enabled = false;

            Directory.CreateDirectory(ShotDir);

            Shot(cam, "01_course", new Vector3(-22f, 21f, -36f), new Vector3(0f, 1f, -4f));
            Shot(cam, "02_ramps", new Vector3(-2f, 11f, -30f), new Vector3(0f, 2f, -14f));
            Shot(cam, "03_obstacles", new Vector3(-6f, 12f, -14f), new Vector3(-4f, 1f, 2f));
            Shot(cam, "04_platform", new Vector3(24f, 12f, 2f), new Vector3(11f, 1f, 14f));

            // Jump arc last: it adds markers to the scene, which we then throw away unsaved.
            PhysicsTests.VisualiseJumpArc();
            cam = Object.FindFirstObjectByType<Camera>();
            follow = cam.GetComponent<Week2.Runtime.FollowCamera>();
            if (follow != null) follow.enabled = false;
            Shot(cam, "05_jumparc", new Vector3(-1f, 6.5f, -23f), new Vector3(-24f, 2.2f, -23f));

            Debug.Log("SHOTS_SUCCESS");
        }

        static void Shot(Camera cam, string name, Vector3 pos, Vector3 look)
        {
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((look - pos).normalized, Vector3.up);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Debug.Log("SHOT " + name);
        }
    }
}
