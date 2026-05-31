#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FixSpriteMaterialsEditor
{
    [MenuItem("Tools/Fix Sprite Materials/Set All SpriteRenderers To Sprites-Default")]
    public static void FixAllSpriteRenderers()
    {
        Material defaultSpriteMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        if (defaultSpriteMaterial == null)
        {
            Debug.LogError("No se ha podido encontrar Sprites-Default.mat");
            return;
        }

        int fixedCount = 0;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

            bool changed = false;
            SpriteRenderer[] renderers = prefabRoot.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer sr in renderers)
            {
                if (sr.sharedMaterial != defaultSpriteMaterial)
                {
                    sr.sharedMaterial = defaultSpriteMaterial;
                    changed = true;
                    fixedCount++;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Materiales corregidos en {fixedCount} SpriteRenderer(s). Ahora usan Sprites-Default.");
    }
}
#endif
