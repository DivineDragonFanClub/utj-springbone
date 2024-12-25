using UnityEditor;
using UnityEngine;

public class LoadBundle : MonoBehaviour
{
    [Tooltip("Keeping around for debugging purposes.")]
    public GameObject loadedPrefab;
    
    [Tooltip("Keeping around for debugging purposes.")]
    public GameObject createdObject;
    
    public void LoadAssetBundle()
    {
#if UNITY_EDITOR
        var bundlePathToLoad = EditorUtility.OpenFilePanelWithFilters("Choose a bundle to load", Application.streamingAssetsPath, new[] {"AssetBundle", "bundle"});
        if (bundlePathToLoad == null)
        {
            Debug.Log("no path to asset bundle?");
            return;
        }
        
        var myLoadedAssetBundle = AssetBundle.LoadFromFile(bundlePathToLoad);

        if (myLoadedAssetBundle == null)
        {
            Debug.Log("Failed to load AssetBundle!");
            return;
        }
        
        var names = myLoadedAssetBundle.GetAllAssetNames();
        
        
        loadedPrefab = myLoadedAssetBundle.LoadAsset<GameObject>(names[0]);
        createdObject = Instantiate(loadedPrefab);
        PrefabUtility.SaveAsPrefabAsset(createdObject, System.IO.Path.Combine("Assets", "AssetName.prefab"));
#endif
    }

    public void UnloadAssetBundles()
    {
        AssetBundle.UnloadAllAssetBundles(true);
        if (createdObject != null)
        {
            DestroyImmediate(createdObject);
        }
    }
}