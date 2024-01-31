using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

namespace UTJ.Jobs
{
    using SpringJobManagerButton = SpringJobManagerInspector.InspectorButton<SpringJobManager>;

    [CustomEditor(typeof(SpringJobManager))]
    [CanEditMultipleObjects]
    public class SpringJobManagerInspector : Editor
    {
        public class InspectorButton<T>
        {
            public InspectorButton(string label, System.Action<T> onPress)
            {
                Label = label;
                OnPress = onPress;
            }

            public string Label { get; set; }
            public System.Action<T> OnPress { get; set; }

            public void Show(T target)
            {
                if (GUILayout.Button(Label)) { OnPress(target); }
            }
        }

        public override void OnInspectorGUI()
        {
            if (targets.Length == 1)
            {
                // Only show buttons if one component is selected
                if (actionButtons == null || actionButtons.Length == 0)
                {
                    actionButtons = new[] {
                        new SpringJobManagerButton("Display Spring Bone window", ShowSpringWindow),
                        new SpringJobManagerButton("Select all Spring Bones", SelectAllBones),
                        new SpringJobManagerButton("Update Spring Bone List", UpdateBoneList)
                    };
                }

                EditorGUILayout.Space();
                var manager = (SpringJobManager)target;
                for (int buttonIndex = 0; buttonIndex < actionButtons.Length; buttonIndex++)
                {
                    actionButtons[buttonIndex].Show(manager);
                }
                EditorGUILayout.Space();
                var boneCount = (manager.SortedBones != null) ? manager.SortedBones.Length : 0;
                GUILayout.Label("Bones: " + boneCount);
                EditorGUILayout.Space();
            }

            base.OnInspectorGUI();
        }

        // private

        private SpringJobManagerButton[] actionButtons;

        private static void ShowSpringWindow(SpringJobManager manager)
        {
            SpringBoneWindow.ShowWindow();
        }

        private static void SelectAllBones(SpringJobManager manager)
        {
            var bones = manager.GetComponentsInChildren<SpringBone>(true);
            Selection.objects = bones.Select(item => item.gameObject).ToArray();
        }

        private static void UpdateBoneList(SpringJobManager manager)
        {
            manager.CachedJobParam();
        }

        private static int GetObjectDepth(Transform inObject) {
            var depth = 0;
            var currentObject = inObject;
            while (currentObject != null) {
                currentObject = currentObject.parent;
                ++depth;
            }
            return depth;
        }

        public static Transform GetPivotTransform(SpringBone bone)
        {
            if (bone.pivotNode == null)
            {
                bone.pivotNode = bone.transform.parent ?? bone.transform;
            }
            return bone.pivotNode;
        }

        private static SpringBone[] FindSpringBones(SpringJobManager manager, bool includeInactive = false) {
            var unsortedSpringBones = manager.GetComponentsInChildren<SpringBone>(includeInactive);
            var boneDepthList = unsortedSpringBones
                .Select(bone => new { bone, depth = GetObjectDepth(bone.transform) })
                .ToList();
            boneDepthList.Sort((a, b) => a.depth.CompareTo(b.depth));
            return boneDepthList.Select(item => item.bone).ToArray();
        }

        private static void CachedJobParam(SpringJobManager manager) {
            manager.SortedBones = FindSpringBones(manager);
            var nSpringBones = manager.SortedBones.Length;

            manager.jobProperties = new SpringBoneProperties[nSpringBones];
            manager.initLocalRotations = new Quaternion[nSpringBones];
            manager.jobColProperties = new SpringColliderProperties[nSpringBones];
            //manager.jobLengthProperties = new LengthLimitProperties[nSpringBones][];
            var jobLengthPropertiesList = new List<LengthLimitProperties>();

            for (var i = 0; i < nSpringBones; ++i) {
                SpringBone springBone = manager.SortedBones[i];
                //springBone.index = i;

                var root = springBone.transform;
                var parent = root.parent;

                //var childPos = ComputeChildBonePosition(springBone);
                var childPos = springBone.ComputeChildPosition();
                var childLocalPos = root.InverseTransformPoint(childPos);
                var boneAxis = Vector3.Normalize(childLocalPos);

                var worldPos = root.position;
                //var worldRot = root.rotation;

                var springLength = Vector3.Distance(worldPos, childPos);
                var currTipPos = childPos;
                var prevTipPos = childPos;

                // Length Limit
                var targetCount = springBone.lengthLimitTargets.Length;
                //manager.jobLengthProperties[i] = new LengthLimitProperties[targetCount];
                if (targetCount > 0) {
                    for (int m = 0; m < targetCount; ++m) {
                        var targetRoot = springBone.lengthLimitTargets[m];
                        int targetIndex = -1;
                        // NOTE: 
                        //if (targetRoot.TryGetComponent<SpringBone>(out var targetBone))
                            //targetIndex = targetBone.index;
                        var prop = new LengthLimitProperties {
                            targetIndex = targetIndex,
                            target = Vector3.Magnitude(targetRoot.position - childPos),
                        };
                        jobLengthPropertiesList.Add(prop);
                    }
                }

                // ReadOnly
                int parentIndex = -1;
                Matrix4x4 pivotLocalMatrix = Matrix4x4.identity;
                //if (parent.TryGetComponent<SpringBone>(out var parentBone))
                 //   parentIndex = parentBone.index;

                var pivotIndex = -1;
                var pivotTransform = GetPivotTransform(springBone);
                var pivotBone = pivotTransform.GetComponentInParent<SpringBone>();
                if (pivotBone != null) {
                   // pivotIndex = pivotBone.index;
                    // NOTE: PivotがSpringBoneの子供に置かれている場合の対処
                    if (pivotBone.transform != pivotTransform) {
                        // NOTE: 1個上の親がSpringBoneとは限らない
                        //pivotLocalMatrix = Matrix4x4.TRS(pivotTransform.localPosition, pivotTransform.localRotation, Vector3.one);
                        pivotLocalMatrix = Matrix4x4.Inverse(pivotBone.transform.localToWorldMatrix) * pivotTransform.localToWorldMatrix;
                    }
                }

                // ReadOnly
                manager.jobProperties[i] = new SpringBoneProperties {
                    stiffnessForce = springBone.stiffnessForce,
                    dragForce = springBone.dragForce,
                    springForce = springBone.springForce,
                    windInfluence = springBone.windInfluence,
                    angularStiffness = springBone.angularStiffness,
                    yAngleLimits = new AngleLimitComponent {
                        active = springBone.yAngleLimits.active,
                        min = springBone.yAngleLimits.min,
                        max = springBone.yAngleLimits.max,
                    },
                    zAngleLimits = new AngleLimitComponent {
                        active = springBone.zAngleLimits.active,
                        min = springBone.zAngleLimits.min,
                        max = springBone.zAngleLimits.max,
                    },
                    radius = springBone.radius,
                    boneAxis = boneAxis,
                    springLength = springLength,
                    localPosition = root.localPosition,
                    initialLocalRotation = root.localRotation,
                    parentIndex = parentIndex,

                    pivotIndex = pivotIndex,
                    pivotLocalMatrix = pivotLocalMatrix,
                };

                manager.initLocalRotations[i] = root.localRotation;

                // turn off SpringBone component to let Job work
                springBone.enabled = false;
                //springBone.enabledJobSystem = true;
            }

            // Colliders
            manager.jobColliders = manager.GetComponentsInChildren<SpringCollider>(true);
            int nColliders = manager.jobColliders.Length;
            for (int i = 0; i < nColliders; ++i) {
                //manager.jobColliders[i].index = i;
                var comp = new SpringColliderProperties() {
                    type = manager.jobColliders[i].type,
                    radius = manager.jobColliders[i].radius,
                    width = manager.jobColliders[i].width,
                    height = manager.jobColliders[i].height,
                };
                manager.jobColProperties[i] = comp;
            }

            // LengthLimits
            manager.jobLengthProperties = jobLengthPropertiesList.ToArray();
        }
    }
}