using UnityEngine;

namespace UTJ.Jobs
{
    // Authoring component
    public class SpringCollider : MonoBehaviour {
        [SerializeField, HideInInspector]
        internal int index = 0;          // number of SpringBone

        public ColliderType type;
        public float radius;
        public float width;
        public float height;

#if UNITY_EDITOR
        public bool shouldDrawGizmosThisFrame;
        private Vector3[] ringPoints;
        private Vector3[] endRingPoints;

        public void DrawGizmos(Color drawColor) {
            var worldRadius = transform.TransformDirection(radius, 0f, 0f).magnitude;
            // For picking
            Gizmos.color = new Color(0f, 0f, 0f, 0f);
            Gizmos.DrawWireSphere(transform.position, worldRadius);

            UnityEditor.Handles.color = drawColor;
            if (type == ColliderType.Capsule) {
                const int PointCount = 16;

                UnityEditor.Handles.color = drawColor;

                if (ringPoints == null || ringPoints.Length != PointCount) {
                    ringPoints = new Vector3[PointCount];
                    endRingPoints = new Vector3[PointCount];
                }

                //var worldRadius = transform.TransformDirection(radius, 0f, 0f).magnitude;

                var startCapOrigin = transform.position;
                var endCapOrigin = transform.TransformPoint(0f, height, 0f);
                AngleLimits.DrawAngleLimit(startCapOrigin, transform.up, transform.forward, -180f, worldRadius);
                AngleLimits.DrawAngleLimit(startCapOrigin, transform.up, transform.right, -180f, worldRadius);
                AngleLimits.DrawAngleLimit(endCapOrigin, transform.up, transform.forward, 180f, worldRadius);
                AngleLimits.DrawAngleLimit(endCapOrigin, transform.up, transform.right, 180f, worldRadius);

                GetRingPoints(startCapOrigin, transform.right, transform.forward, worldRadius, ref ringPoints);
                var startToEnd = endCapOrigin - startCapOrigin;
                for (int pointIndex = 0; pointIndex < PointCount; pointIndex++) {
                    endRingPoints[pointIndex] = ringPoints[pointIndex] + startToEnd;
                }

                for (int pointIndex = 1; pointIndex < PointCount; pointIndex++) {
                    UnityEditor.Handles.DrawLine(ringPoints[pointIndex - 1], ringPoints[pointIndex]);
                    UnityEditor.Handles.DrawLine(endRingPoints[pointIndex - 1], endRingPoints[pointIndex]);
                }

                for (int pointIndex = 0; pointIndex < PointCount; pointIndex++) {
                    UnityEditor.Handles.DrawLine(ringPoints[pointIndex], endRingPoints[pointIndex]);
                }

                if (m_colliderDebugger != null) {
                    m_colliderDebugger.DrawGizmosAndClear();
                }
            } else {
                UnityEditor.Handles.RadiusHandle(Quaternion.identity, transform.position, worldRadius);
            }
            if (m_colliderDebugger != null) {
                m_colliderDebugger.DrawGizmosAndClear();
            }
        }
        private static Vector3 GetAngleVector(Vector3 sideVector, Vector3 forwardVector, float radians) {
            return Mathf.Sin(radians) * sideVector + Mathf.Cos(radians) * forwardVector;
        }

        private static void GetRingPoints
        (
            Vector3 origin,
            Vector3 sideVector,
            Vector3 forwardVector,
            float scale,
            ref Vector3[] ringPoints
        ) {
            var lastPoint = origin + scale * forwardVector;
            var pointCount = ringPoints.Length;
            var deltaAngle = 2f * Mathf.PI / (pointCount - 1);
            var angle = deltaAngle;
            ringPoints[0] = lastPoint;
            for (var iteration = 1; iteration < pointCount; ++iteration) {
                var newPoint = origin + scale * GetAngleVector(sideVector, forwardVector, angle);
                ringPoints[iteration] = newPoint;
                lastPoint = newPoint;
                angle += deltaAngle;
            }
        }

        private SpringManager manager;
        private SpringColliderDebugger m_colliderDebugger;

        private void OnDrawGizmos() {
            if (shouldDrawGizmosThisFrame || !SpringManager.onlyShowSelectedColliders) {
                if (manager == null) { manager = GetComponentInParent<SpringManager>(); }
                DrawGizmos((enabled && manager != null) ? manager.colliderColor : Color.gray);
                shouldDrawGizmosThisFrame = false;
            }
        }

        private void OnDrawGizmosSelected() {
            DrawGizmos(enabled ? Color.white : Color.gray);
        }

        private void RecordCollision
        (
            Vector3 localMoverPosition,
            float worldMoverRadius,
            SpringBone.CollisionStatus collisionStatus
        ) {
            if (!enabled) { return; }
            if (m_colliderDebugger == null) { m_colliderDebugger = new SpringColliderDebugger(); }
            var localNormal = (localMoverPosition).normalized;
            var localContactPoint = localNormal * radius;
            var worldNormal = transform.TransformDirection(localNormal).normalized;
            var worldContactPoint = transform.TransformPoint(localContactPoint);
            m_colliderDebugger.RecordCollision(worldContactPoint, worldNormal, worldMoverRadius, collisionStatus);
        }
        // extend function for Job
        private void OnValidate() {
            // NOTE: Job化したら編集不可
            this.gameObject.hideFlags |= HideFlags.NotEditable;
        }
#endif
    }
}