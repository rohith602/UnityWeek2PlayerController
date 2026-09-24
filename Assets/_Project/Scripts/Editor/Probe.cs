using UnityEditor;
using UnityEngine;

namespace Week2.EditorTools
{
    /// <summary>Throwaway probe used while wiring up the character.</summary>
    public static class Probe
    {
        [MenuItem("Tools/Week 2/Probe Character")]
        public static void Run()
        {
            const string path = "Assets/ThirdParty/Kenney_BlockyCharacters/Models/character-a.fbx";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.Log("PROBE missing " + path); return; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.transform.position = Vector3.zero;

            var rends = inst.GetComponentsInChildren<Renderer>();
            var b = rends.Length > 0 ? rends[0].bounds : new Bounds();
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Debug.Log($"PROBE size={b.size} centre={b.center} renderers={rends.Length} " +
                      $"skinned={inst.GetComponentsInChildren<SkinnedMeshRenderer>().Length} " +
                      $"animator={(inst.GetComponentInChildren<Animator>() != null)}");

            foreach (var t in inst.GetComponentsInChildren<Transform>())
            {
                int depth = 0;
                for (var p = t.parent; p != null && p != inst.transform.parent; p = p.parent) depth++;
                Debug.Log($"PROBE bone {new string(' ', depth * 2)}{t.name}  local={t.localPosition}");
            }

            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
                Debug.Log($"PROBE smr bones={smr.bones.Length} root={(smr.rootBone ? smr.rootBone.name : "null")} " +
                          $"mat={(smr.sharedMaterial ? smr.sharedMaterial.name : "none")} " +
                          $"tex={(smr.sharedMaterial && smr.sharedMaterial.mainTexture ? smr.sharedMaterial.mainTexture.name : "none")}");

            Object.DestroyImmediate(inst);
            Debug.Log("PROBE_DONE");
        }
    }
}
