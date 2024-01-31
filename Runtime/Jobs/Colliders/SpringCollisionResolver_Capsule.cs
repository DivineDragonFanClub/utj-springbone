using UnityEngine;

namespace UTJ.Jobs
{
    // Up is y-axis
    public static partial class SpringCollisionResolver
    {
        public static bool ResolveCapsule
        (
            SpringColliderProperties capsule,
            SpringColliderComponents transform,
            Vector3 moverHeadPosition, 
            ref Vector3 moverPosition, 
            ref Vector3 hitNormal,
            float moverRadius
        )
        {
            const float RadiusThreshold = 0.0001f;
            if (capsule.radius <= RadiusThreshold)
                return false;

            var worldToLocal = transform.worldToLocalMatrix;
            var radiusScale = worldToLocal.MultiplyVector(new Vector3(1f, 0f, 0f)).magnitude;

            // Lower than start cap
            var localHeadPosition = worldToLocal.MultiplyPoint3x4(moverHeadPosition);
            var localMoverPosition = worldToLocal.MultiplyPoint3x4(moverPosition);
            var localMoverRadius = moverRadius * radiusScale;

            var moverIsAboveTop = localMoverPosition.y >= capsule.height;
            var useSphereCheck = (localMoverPosition.y <= 0f) | moverIsAboveTop;
            float combinedRadius;
            
            if (useSphereCheck)
            {
                var sphereOrigin = new Vector3(0f, moverIsAboveTop ? capsule.height : 0f, 0f);
                combinedRadius = localMoverRadius + capsule.radius;
                if ((localMoverPosition - sphereOrigin).sqrMagnitude >= combinedRadius * combinedRadius)
                {
                    // Not colliding
                    return false;
                }

                var originToHead = localHeadPosition - sphereOrigin;
                var isHeadEmbedded = originToHead.sqrMagnitude <= capsule.radius * capsule.radius;
                
                if (isHeadEmbedded)
                {
                    // The head is inside the sphere, so just try to push the tail out
                    var localHitNormal = (localMoverPosition - sphereOrigin).normalized;
                    localMoverPosition = sphereOrigin + localHitNormal * combinedRadius;
                    var localToWorld = transform.localToWorldMatrix;
                    moverPosition = localToWorld.MultiplyPoint3x4(localMoverPosition);
                    hitNormal = Vector3.Normalize(localToWorld.MultiplyVector(localHitNormal));
                    return true;
                }

                var localHeadRadius = (localMoverPosition - localHeadPosition).magnitude;
                if (ComputeIntersection_Sphere(
                    localHeadPosition, localHeadRadius,
                    sphereOrigin, combinedRadius,
                    out var intersection))
                {
                    localMoverPosition = ComputeNewTailPosition_Sphere(intersection, localMoverPosition);
                    var localToWorld = transform.localToWorldMatrix;
                    moverPosition = localToWorld.MultiplyPoint3x4(localMoverPosition);
                    var localHitNormal = Vector3.Normalize(localMoverPosition - sphereOrigin);
                    hitNormal = Vector3.Normalize(localToWorld.MultiplyVector(localHitNormal));
                }

                return true;
            }

            var originToMover = new Vector2(localMoverPosition.x, localMoverPosition.z);
            combinedRadius = capsule.radius + localMoverRadius;
            var collided = originToMover.sqrMagnitude <= combinedRadius * combinedRadius;
            if (collided)
            {
                var normal = originToMover.normalized;
                originToMover = combinedRadius * normal;
                var newLocalMoverPosition = new Vector3(originToMover.x, localMoverPosition.y, originToMover.y);
                var localToWorld = transform.localToWorldMatrix;
                moverPosition = localToWorld.MultiplyPoint3x4(newLocalMoverPosition);
                hitNormal = Vector3.Normalize(localToWorld.MultiplyVector(new Vector3(normal.x, 0f, normal.y)));
            }

            return collided;
        }
    }
}