using System;
using UnityEngine;
using UTJ.Jobs;

namespace UTJ {
    public class OriginalWindForceProvider: ForceProvider {
        public SpringForceComponent m_forceComponent;

        public override SpringForceComponent GetActiveForce()
        {
            return m_forceComponent;
        }

        void Update()
        {
            m_forceComponent.time = (float)(m_forceComponent.time + Time.deltaTime * 0.5);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var origin = transform.position;
            var strengthMultiplier = Mathf.Clamp(m_forceComponent.strength, 0.1f, 1f);
            var destination = origin + strengthMultiplier * transform.forward;
            var offsets = new Vector3[]
            {
                Vector3.zero,
                0.02f * transform.up,
                -0.02f * transform.up
            };

            foreach (var offset in offsets)
            {
                GizmoUtil.DrawArrow(origin + offset, destination + offset, Color.gray, 0.1f);
            }
        }
#endif
    }
}