using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UTJ
{
    public class LoadSpringBoneSetupWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var editorWindow = GetWindow<LoadSpringBoneSetupWindow>(
                "Load spring bone setup");
            if (editorWindow != null)
            {
                editorWindow.SelectObjectsFromSelection();
                editorWindow.SelectPrefabFromSelection();
            }
        }

        public static T DoObjectPicker<T>
        (
            string label,
            T currentObject,
            int uiWidth,
            int uiHeight,
            ref int yPos
        ) where T : UnityEngine.Object
        {
            var uiRect = new Rect(UISpacing, yPos, LabelWidth, uiHeight);
            GUI.Label(uiRect, label, SpringBoneGUIStyles.LabelStyle);
            uiRect.x = LabelWidth + UISpacing;
            uiRect.width = uiWidth - uiRect.x + UISpacing;
            yPos += uiHeight + UISpacing;
            return EditorGUI.ObjectField(uiRect, currentObject, typeof(T), true) as T;
        }

        // private

        private const string StopPlayModeMessage = "Please don't setup during play mode.";
        private const string SelectObjectRootsMessage = "Please select the root spring bone object.";
        private const int UIRowHeight = 24;
        private const int UISpacing = 8;
        private const int LabelWidth = 200;

        private GameObject springBoneRoot;
        private GameObject prefabRoot;
        private Support.DynamicsSetup.ImportSettings importSettings;

        private void SelectObjectsFromSelection()
        {
            springBoneRoot = null;
            if (Selection.objects.Length > 0)
            {
                springBoneRoot = Selection.objects[0] as GameObject;
            }

            if (springBoneRoot == null)
            {
                var characterRootComponentTypes = new System.Type[] {
                    typeof(SpringManager),
                    typeof(Animation),
                    typeof(Animator)
                };
                springBoneRoot = characterRootComponentTypes
                    .Select(type => FindObjectOfType(type) as Component)
                    .Where(component => component != null)
                    .Select(component => component.gameObject)
                    .FirstOrDefault();
            }
        }

        private void SelectPrefabFromSelection()
        {
            if (Selection.objects.Length > 0)
            {
                prefabRoot = Selection.objects[0] as GameObject;
            }
        }

        private void ShowImportSettingsUI(ref Rect uiRect)
        {
            if (importSettings == null)
            {
                importSettings = new Support.DynamicsSetup.ImportSettings();
            }

            GUI.Label(uiRect, "Loading settings", SpringBoneGUIStyles.HeaderLabelStyle);
            uiRect.y += uiRect.height;
            importSettings.ImportSpringBones = GUI.Toggle(uiRect, importSettings.ImportSpringBones, "Spring bones", SpringBoneGUIStyles.ToggleStyle);
            uiRect.y += uiRect.height;
            importSettings.ImportCollision = GUI.Toggle(uiRect, importSettings.ImportCollision, "Colliders", SpringBoneGUIStyles.ToggleStyle);
            uiRect.y += uiRect.height;
        }

        private void OnGUI()
        {
            SpringBoneGUIStyles.ReacquireStyles();

            const int ButtonHeight = 30;

            var uiWidth = (int)position.width - UISpacing * 2;
            var yPos = UISpacing;
            springBoneRoot = DoObjectPicker("Spring bone root", springBoneRoot, uiWidth, UIRowHeight, ref yPos);
            prefabRoot = DoObjectPicker("Prefab root", prefabRoot, uiWidth, UIRowHeight, ref yPos);

            var buttonRect = new Rect(UISpacing, yPos, uiWidth, ButtonHeight);
            if (GUI.Button(buttonRect, "Get root from selection", SpringBoneGUIStyles.ButtonStyle))
            {
                SelectObjectsFromSelection();
            }
            yPos += ButtonHeight + UISpacing;
            buttonRect.y = yPos;
            
            var massSaveButton = new Rect(UISpacing, yPos, uiWidth, ButtonHeight);
            yPos += ButtonHeight + UISpacing;
            massSaveButton.y = yPos;

            ShowImportSettingsUI(ref massSaveButton);

            string errorMessage;
            if (IsOkayToSetup(out errorMessage))
            {
                if (GUI.Button(buttonRect, "Load CSV and Setup", SpringBoneGUIStyles.ButtonStyle))
                {
                    BrowseAndLoadSpringSetup(null, springBoneRoot);
                }
            }
            else
            {
                const int MessageHeight = 24;
                var uiRect = new Rect(UISpacing, buttonRect.y, uiWidth, MessageHeight);
                GUI.Label(uiRect, errorMessage, SpringBoneGUIStyles.HeaderLabelStyle);
            }
            
            if (GUI.Button(massSaveButton, "Mass Load CSV and Setup", SpringBoneGUIStyles.ButtonStyle))
            {
                var path = EditorUtility.OpenFolderPanel("Open folder that has your dynamic CSVs", "", "");
                // get all the csv files in the folder
                var csvFiles = System.IO.Directory.GetFiles(path, "*.csv");
                // get all the names of the files and assume they are the names of the bones
                // then call BrowseAndLoadSpringSetup for each one
                foreach (var file in csvFiles)
                {
                    
                    var boneName = System.IO.Path.GetFileNameWithoutExtension(file);
                    // strip off the _Dynamic part and everything after it
                    var dynamicIndex = boneName.IndexOf("_Dynamic");
                    if (dynamicIndex > 0)
                    {
                        boneName = boneName.Substring(0, dynamicIndex);
                    }
                    BrowseAndLoadSpringSetup(file, getRootToUse(boneName));
                }
            }
        }

        private bool IsOkayToSetup(out string errorMessage)
        {
            errorMessage = "";
            if (EditorApplication.isPlaying)
            {
                errorMessage = StopPlayModeMessage;
                return false;
            }

            if (springBoneRoot == null)
            {
                errorMessage = SelectObjectRootsMessage;
                return false;
            }
            return true;
        }

        private static T FindHighestComponentInHierarchy<T>(GameObject startObject) where T : Component
        {
            T highestComponent = null;
            if (startObject != null)
            {
                var transform = startObject.transform;
                while (transform != null)
                {
                    var component = transform.GetComponent<T>();
                    if (component != null) { highestComponent = component; }
                    transform = transform.parent;
                }
            }
            return highestComponent;
        }

        private class BuildDynamicsAction : SpringBoneSetupErrorWindow.IConfirmAction
        {
            public BuildDynamicsAction
            (
                Support.DynamicsSetup newSetup,
                string newPath,
                GameObject newSpringBoneRoot
            )
            {
                setup = newSetup;
                path = newPath;
                springBoneRoot = newSpringBoneRoot;
            }

            public void Perform()
            {
                setup.Build();
                AssetDatabase.Refresh();

                const string ResultFormat = "Setup complete: {0}\n Bone count: {1} Collider count: {2}";
                var boneCount = springBoneRoot.GetComponentsInChildren<SpringBone>(true).Length;
                var colliderCount = Support.SpringColliderSetup.GetColliderTypes()
                    .Sum(type => springBoneRoot.GetComponentsInChildren(type, true).Length);
                var resultMessage = string.Format(ResultFormat, path, boneCount, colliderCount);
                Debug.Log(resultMessage);
            }

            private Support.DynamicsSetup setup;
            private string path;
            private GameObject springBoneRoot;
        }
        
        private GameObject getRootToUse (string name)
        {
            return FindInChildren.Find(prefabRoot.transform, name).gameObject;
        }

        private void BrowseAndLoadSpringSetup(string providedPath, GameObject springBoneRoot)
        {
            string checkErrorMessage;
            if (!IsOkayToSetup(out checkErrorMessage))
            {
                Debug.LogError(checkErrorMessage);
                return;
            }

            // var initialPath = "";
            var initialDirectory = ""; // System.IO.Path.GetDirectoryName(initialPath);
            var fileFilters = new string[] { "CSVファイル", "csv", "テキストファイル", "txt" };
            var path = providedPath ?? EditorUtility.OpenFilePanelWithFilters(
                "Load spring bone setup", initialDirectory, fileFilters);
            if (path.Length == 0) { return; }

            var sourceText = UTJ.Support.FileUtil.ReadAllText(path);
            if (string.IsNullOrEmpty(sourceText)) { return; }

            var parsedSetup = Support.DynamicsSetup.ParseFromRecordText(springBoneRoot, springBoneRoot, sourceText, importSettings);
            if (parsedSetup.Setup != null)
            {
                var buildAction = new BuildDynamicsAction(parsedSetup.Setup, path, springBoneRoot);
                if (parsedSetup.HasErrors)
                {
                    SpringBoneSetupErrorWindow.ShowWindow(springBoneRoot, springBoneRoot, path, parsedSetup.Errors, buildAction);
                }
                else
                {
                    buildAction.Perform();
                }
            }
            else
            {
                const string ErrorFormat =
                    "スプリングボーンセットアップが失敗しました。\n"
                    + "元データにエラーがあるか、もしくは\n"
                    + "キャラクターにデータが一致しません。\n"
                    + "詳しくはConsoleのログをご覧下さい。\n\n"
                    + "キャラクター: {0}\n\n"
                    + "パス: {1}";
                var resultErrorMessage = string.Format(ErrorFormat, springBoneRoot.name, path);
                EditorUtility.DisplayDialog("スプリングボーンセットアップ", resultErrorMessage, "OK");
                Debug.LogError("スプリングボーンセットアップ失敗: " + springBoneRoot.name + "\n" + path);
            }
            Close();
        }
    }
}

public static class FindInChildren {

    public static Transform Find(this Transform parent, string name) {

        var searchResult = parent.Find(name);

        if (searchResult != null)
            return searchResult;

        foreach (Transform child in parent) {
            searchResult = FindInChildren.Find(child,name);
            if (searchResult != null)
                return searchResult;
        }

        return null;
    }
}