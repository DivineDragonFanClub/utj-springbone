#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

[CustomEditor(typeof(LoadBundle))]
public class LoadBundleEditor: Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        LoadBundle myTarget = (LoadBundle)target;
        if (GUILayout.Button("Load prefab from AssetBundle"))
        {
            myTarget.LoadAssetBundle();
        }
        
        if (GUILayout.Button("Unload All (Do this before saving)"))
        {
            myTarget.UnloadAssetBundles();
        }

      
    }
}
#endif