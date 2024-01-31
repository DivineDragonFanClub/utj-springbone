using System.Linq;
using UnityEditor;
using UnityEngine;

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
            }
        }

        // private

        private GameObject springBoneRoot;
        private SpringBoneSerialization.ExportSettings exportSettings;

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

        private void ShowExportSettingsUI(ref Rect uiRect)
        {
            if (exportSettings == null)
            {
                exportSettings = new SpringBoneSerialization.ExportSettings();
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
            var buttonRect = new Rect(UISpacing, yPos, uiWidth, ButtonHeight);
            if (GUI.Button(buttonRect, "Get root from selection", SpringBoneGUIStyles.ButtonStyle))
            {
                SelectObjectsFromSelection();
            }
            yPos += ButtonHeight + UISpacing;
            buttonRect.y = yPos;

            ShowExportSettingsUI(ref buttonRect);
            if (springBoneRoot != null)
            {
                if (GUI.Button(buttonRect, "Save CSV", SpringBoneGUIStyles.ButtonStyle))
                {
                    BrowseAndSaveSpringSetup();
                }
            }
        }

        private void BrowseAndSaveSpringSetup()
        {
            if (springBoneRoot == null) { return; }

            var initialFileName = springBoneRoot.name + "_Dynamics.csv";

            var path = EditorUtility.SaveFilePanel(
                "Save Spring Bone setup", "", initialFileName, "csv");
            if (path.Length == 0) { return; }

            if (System.IO.File.Exists(path))
            {
                var overwriteMessage = "The file already exists. Do you want to overwrite it?\n\n" + path;
                if (!EditorUtility.DisplayDialog("Preserve Spring Bones", overwriteMessage, "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            var sourceText = SpringBoneSerialization.BuildDynamicsSetupString(springBoneRoot, exportSettings);
            if (FileUtil.WriteAllText(path, sourceText))
            {
                AssetDatabase.Refresh();
                Debug.Log("Saved at: " + path);
            }
        }
    }
}