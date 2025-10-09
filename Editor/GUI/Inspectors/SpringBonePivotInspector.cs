using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.SceneManagement;
using UTJ.Jobs;

namespace UTJ
{
    [CustomEditor(typeof(SpringBonePivot))]
    [CanEditMultipleObjects]
    public class SpringBonePivotInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            InitializeData();

            if (GUILayout.Button("Select bone", SpringBoneGUIStyles.ButtonStyle))
            {
                Selection.objects = bones.Select(bone => bone.gameObject).ToArray();
            }

            base.OnInspectorGUI();

            var managerCount = managers.Length;
            for (int managerIndex = 0; managerIndex < managerCount; managerIndex++)
            {
                EditorGUILayout.ObjectField("Manager", managers[managerIndex], typeof(SpringJobManager), true);
            }

            var boneCount = bones.Length;
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                EditorGUILayout.ObjectField("Bone", bones[boneIndex], typeof(SpringBone), true);
            }
        }

        private SpringJobManager[] managers;
        private SpringBone[] bones;

        private void InitializeData()
        {
            var pivotComponents = targets.OfType<Component>().ToArray();

            managers = pivotComponents
                .Select(component => component.GetComponentInParent<SpringJobManager>())
                .Where(manager => manager != null)
                .Distinct()
                .ToArray();

            var pivots = pivotComponents
                .Select(component => component.transform)
                .ToArray();

            IEnumerable<SpringBone> candidates;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                candidates = prefabStage.prefabContentsRoot
                    .GetComponentsInChildren<SpringBone>(true);
            }
            else
            {
                candidates = Support.GameObjectExtensions.GameObjectUtil.FindComponentsOfType<SpringBone>();
            }

            bones = candidates
                .Where(bone => bone != null && bone.pivotNode != null && pivots.Contains(bone.pivotNode))
                .Distinct()
                .ToArray();
        }
    }
}
