using UnityEditor;
using UnityEngine;

namespace UTJ.Jobs
{
    [CustomEditor(typeof(SpringJobScheduler))]
    public class SpringJobSchedulerInspector : Editor
    {
        private static class Styles
        {
            public static readonly GUIContent asyncLabel = EditorGUIUtility.TrTextContent("Enable asynchronous processing", "LateUpdate以降のバックグラウンドで計算を行います。ボーンへの反映が1F遅れることに注意してください");
            public static readonly GUIContent threadLabel = EditorGUIUtility.TrTextContent("Maximum amount of threads for arithmetics jobs", "最大でいくつのワーカースレッドで演算部分の分散処理を行うのか設定します。0で無制限になります。");
            public static readonly GUIContent registerLabel = EditorGUIUtility.TrTextContent("SpringJobManagerの上限値", "シーン内でアクティブなSpringJobManagerの最大値です。十分な値を設定してください。");
            public static readonly GUIContent boneLabel = EditorGUIUtility.TrTextContent("SpringBoneの上限値", "シーン内でアクティブなSpringJobManagerで扱われるSpringBoneの最大値です。十分な値を設定してください。");
            public static readonly GUIContent colliderLabel = EditorGUIUtility.TrTextContent("SpringColliderの上限値", "シーン内でアクティブなSpringJobManagerで扱われるSpringColliderの最大値です。十分な値を設定してください。");
            public static readonly GUIContent regiColliderLabel = EditorGUIUtility.TrTextContent("Collider判定の上限値", "シーン内でアクティブなSpringJobManagerで扱われるSpringBoneに登録されているColliderの最大値です。肥大化しがちなので十分な値を設定してください。");
            public static readonly GUIContent regiLengthLabel = EditorGUIUtility.TrTextContent("LenghLimit判定の上限値", "シーン内でアクティブなSpringJobManagerで扱われるSpringBoneに登録されているLengthLimitの最大値です。肥大化しがちなので十分な値を設定してください。");
            public static readonly GUIContent forceLabel = EditorGUIUtility.TrTextContent("外力の上限値", "シーン内でアクティブなForceProviderの最大値です。");
        }

        private SerializedProperty m_propAsync;
        private SerializedProperty m_propThread;
        private SerializedProperty m_propRegister;
        private SerializedProperty m_propBone;
        private SerializedProperty m_propCollider;
        private SerializedProperty m_propRegiCollider;
        private SerializedProperty m_propRegiLength;
        private SerializedProperty m_propForce;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_propAsync, Styles.asyncLabel);
            EditorGUILayout.PropertyField(m_propThread, Styles.threadLabel);
            EditorGUILayout.PropertyField(m_propRegister, Styles.registerLabel);
            EditorGUILayout.PropertyField(m_propBone, Styles.boneLabel);
            EditorGUILayout.PropertyField(m_propCollider, Styles.colliderLabel);
            EditorGUILayout.PropertyField(m_propRegiCollider, Styles.regiColliderLabel);
            EditorGUILayout.PropertyField(m_propRegiLength, Styles.regiLengthLabel);
            EditorGUILayout.PropertyField(m_propForce, Styles.forceLabel);

            if (m_propThread.intValue < 0)
                m_propThread.intValue = 0;
            if (m_propRegister.intValue < 1)
                m_propRegister.intValue = 1;
            if (m_propBone.intValue < 1)
                m_propBone.intValue = 1;
            if (m_propCollider.intValue < 1)
                m_propCollider.intValue = 1;
            if (m_propRegiCollider.intValue < 1)
                m_propRegiCollider.intValue = 1;
            if (m_propRegiLength.intValue < 1)
                m_propRegiLength.intValue = 1;

            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            m_propAsync = serializedObject.FindProperty("asynchronize");
            m_propThread = serializedObject.FindProperty("maxWorkerThreadCount");
            m_propRegister = serializedObject.FindProperty("registerCapacity");
            m_propBone = serializedObject.FindProperty("boneCapacity");
            m_propCollider = serializedObject.FindProperty("colliderCapacity");
            m_propRegiCollider = serializedObject.FindProperty("registedColliderCapacity");
            m_propRegiLength = serializedObject.FindProperty("registeredLengthLimitCapacity");
            m_propForce = serializedObject.FindProperty("forceCapacity");
        }
    }
}