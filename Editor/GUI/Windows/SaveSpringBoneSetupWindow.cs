using System.Linq;
using UnityEditor;
using UnityEngine;
using UTJ.Jobs;

namespace UTJ
{
    public class SaveSpringBoneSetupWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var editorWindow = GetWindow<SaveSpringBoneSetupWindow>(
                "Save Spring Bone setup");
            if (editorWindow != null)
            {
                editorWindow.SelectObjectsFromSelection();
                editorWindow.SelectPrefabFromSelection();
            }
        }

        // private

        private GameObject springBoneRoot;
        private GameObject prefabRoot;
        private UTJ.Support.SpringBoneSerialization.ExportSettings exportSettings;

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

        private void ShowExportSettingsUI(ref Rect uiRect)
        {
            if (exportSettings == null)
            {
                exportSettings = new UTJ.Support.SpringBoneSerialization.ExportSettings();
            }

            GUI.Label(uiRect, "Export settings", SpringBoneGUIStyles.HeaderLabelStyle);
            uiRect.y += uiRect.height;
            exportSettings.ExportSpringBones = GUI.Toggle(uiRect, exportSettings.ExportSpringBones, "Spring Bones", SpringBoneGUIStyles.ToggleStyle);
            uiRect.y += uiRect.height;
            exportSettings.ExportCollision = GUI.Toggle(uiRect, exportSettings.ExportCollision, "Colliders", SpringBoneGUIStyles.ToggleStyle);
            uiRect.y += uiRect.height;
        }

        private void OnGUI()
        {
            SpringBoneGUIStyles.ReacquireStyles();

            const int ButtonHeight = 30;
            const int UISpacing = 8;
            const int UIRowHeight = 24;

            var uiWidth = (int)position.width - UISpacing * 2;
            var yPos = UISpacing;

            springBoneRoot = LoadSpringBoneSetupWindow.DoObjectPicker(
                "Spring Bone Root", springBoneRoot, uiWidth, UIRowHeight, ref yPos);
            prefabRoot = LoadSpringBoneSetupWindow.DoObjectPicker(
                "Prefab Root", prefabRoot, uiWidth, UIRowHeight, ref yPos);
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
            ShowExportSettingsUI(ref massSaveButton);
            if (springBoneRoot != null)
            {
                if (GUI.Button(buttonRect, "Save CSV", SpringBoneGUIStyles.ButtonStyle))
                {
                    BrowseAndSaveSpringSetup(springBoneRoot, null);
                }
            }

            if (prefabRoot != null)
            {
                if (GUI.Button(massSaveButton, "Mass Save Prefab CSVs", SpringBoneGUIStyles.ButtonStyle))
                {
                    var springBoneRoots = prefabRoot.GetComponentsInChildren<SpringJobManager>();
                    var path = EditorUtility.SaveFolderPanel(
                        "Where do you want your spring bone CSVs? Existing files will be overwritten!", "", "Dynamics");
                    if (path == null)
                    {
                        return;
                    }
                    foreach (var springBoneRoot in springBoneRoots)
                    {
                        BrowseAndSaveSpringSetup(springBoneRoot.gameObject, path);
                    }
                }
            }
        }
        
        private string GetCSVSavePath(GameObject myRoot, string folderPrefix)
        {
            if (folderPrefix != null ) // we already know where it should go
            {
                return folderPrefix + "/" + myRoot.name + "_Dynamics.csv";
            }
            
            var initialFileName = myRoot.name + "_Dynamics.csv";
            var path = EditorUtility.SaveFilePanel(
                "Save Spring Bone setup", folderPrefix, initialFileName, "csv");
            if (System.IO.File.Exists(path))
            {
                var overwriteMessage = "The file already exists. Do you want to overwrite it?\n\n" + path;
                if (!EditorUtility.DisplayDialog("Preserve Spring Bones", overwriteMessage, "Overwrite", "Cancel"))
                {
                    return null;
                }
            }
            return path;
            
        }

        private void BrowseAndSaveSpringSetup(GameObject myRoot, string folderPrefix)
        {
            if (myRoot == null) { return; }

            var path = GetCSVSavePath(myRoot, folderPrefix);
            
            if (path == null)
            {
                return;
            }

            var sourceText = UTJ.Support.SpringBoneSerialization.BuildDynamicsSetupString(myRoot, exportSettings);
            if (UTJ.Support.FileUtil.WriteAllText(path, sourceText))
            {
                AssetDatabase.Refresh();
                Debug.Log("Saved at: " + path);
            }
        }
    }
}