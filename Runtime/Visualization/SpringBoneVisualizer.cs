using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UTJ;

namespace UTJ.Jobs
{
    [ExecuteInEditMode]
    public class SpringBoneVisualizer : MonoBehaviour
    {
        #if UNITY_EDITOR
        public bool showInRuntime = true;
        public bool showBoneNames;
        public Color color = Color.cyan;

        private List<SpringBone> m_bones;
        private Vector3[] m_boneLinePoints;

        void OnEnable()
        {
            m_bones = new List<SpringBone>();
            GetComponentsInChildren(m_bones);
        }

        // Update is called once per frame
        void OnDrawGizmos()
        {
            if (showInRuntime)
            {
                if (Application.isEditor)
                {
                    GetComponentsInChildren(m_bones);
                }

                Gizmos.color = color;

                if (m_boneLinePoints == null || m_boneLinePoints.Length != m_bones.Count * 2)
                {
                    m_boneLinePoints = new Vector3[m_bones.Count * 2];
                }

                for (int boneIndex = 0, pointIndex = 0; boneIndex < m_bones.Count; boneIndex++, pointIndex +=2)
                {
                    var bone = m_bones[boneIndex];
                    var origin = bone.transform.position;
//                    var pivotForward = -bone.GetPivotTransform().right;
//                    m_boneLinePoints[pointIndex] = origin;
//                    m_boneLinePoints[pointIndex + 1] = origin + pivotForward;
                    m_boneLinePoints[pointIndex] = origin;
                    m_boneLinePoints[pointIndex + 1] = bone.ComputeChildPosition();
                    Gizmos.DrawLine(m_boneLinePoints[pointIndex], m_boneLinePoints[pointIndex + 1]);
                    Gizmos.DrawWireSphere(m_boneLinePoints[pointIndex+1], bone.radius);
                }

                if (showBoneNames)
                {
                    foreach (var bone in m_bones)
                    {
                        UnityEditor.Handles.Label(bone.transform.position, bone.name, "BoldLabel");
                    }
                }
            }
        }

        #endif

    }
}