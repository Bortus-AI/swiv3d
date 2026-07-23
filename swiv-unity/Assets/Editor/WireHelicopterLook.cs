using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the Blender multi-material helicopter body mesh + paint scheme to the Helicopter prefab.
/// Menu: SWIV / Wire Helicopter Look
/// </summary>
public static class WireHelicopterLook
{
    const string PrefabPath = "Assets/Prefab/Helicopter.prefab";
    const string FbxPath = "Assets/Meshes/helicopter.fbx";
    const string ObjPath = "Assets/Meshes/helicopter.obj";

    [MenuItem("SWIV/Wire Helicopter Look")]
    public static void Wire()
    {
        // Prefer OBJ: authored in Unity axes (Y-up, Z-forward) matching the original heli.
        AssetDatabase.ImportAsset(ObjPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

        Mesh mesh = LoadMesh(ObjPath) ?? LoadMesh(FbxPath);
        if (mesh == null)
        {
            Debug.LogError("[WireHelicopterLook] No helicopter mesh found at FBX/OBJ paths.");
            return;
        }

        var mats = new[]
        {
            LoadMat("Assets/Materials/HelicopterBody.mat"),
            LoadMat("Assets/Materials/HelicopterCanopy.mat"),
            LoadMat("Assets/Materials/HelicopterAccent.mat"),
            LoadMat("Assets/Materials/HelicopterMetal.mat"),
            LoadMat("Assets/Materials/HelicopterNose.mat"),
            LoadMat("Assets/Materials/HelicopterUnderside.mat"),
        };

        var bladeTop = LoadMat("Assets/Materials/HelicopterBladesTop.mat");
        var bladeFront = LoadMat("Assets/Materials/HelicopterBladesFront.mat");

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform body = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Body")
                {
                    body = t;
                    break;
                }
            }

            if (body == null)
            {
                Debug.LogError("[WireHelicopterLook] Body child not found on Helicopter prefab.");
                return;
            }

            var mf = body.GetComponent<MeshFilter>();
            var mr = body.GetComponent<MeshRenderer>();
            if (mf != null) mf.sharedMesh = mesh;

            if (mr != null)
            {
                int n = Mathf.Max(1, mesh.subMeshCount);
                var finalMats = new Material[n];
                for (int i = 0; i < n; i++)
                    finalMats[i] = mats[Mathf.Min(i, mats.Length - 1)];
                mr.sharedMaterials = finalMats;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "BladesTop" && t.name != "BladeFront" && t.name != "BladeBack")
                    continue;
                var bmr = t.GetComponent<MeshRenderer>();
                if (bmr == null) continue;
                bmr.sharedMaterial = t.name == "BladesTop" ? bladeTop : bladeFront;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[WireHelicopterLook] Wired mesh '{mesh.name}' ({mesh.vertexCount} verts, {mesh.subMeshCount} submeshes) + materials onto {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static Mesh LoadMesh(string assetPath)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (go != null)
        {
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
        }

        return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Mesh>().FirstOrDefault();
    }

    static Material LoadMat(string path)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
            Debug.LogWarning("[WireHelicopterLook] Missing material: " + path);
        return mat;
    }

    // Auto-wire disabled: the player heli now uses the RAH-66 Comanche FBX
    // (Assets/Meshes/RAH66). Use menu SWIV/Wire Helicopter Look only for the
    // old procedural mesh workflow.
}
