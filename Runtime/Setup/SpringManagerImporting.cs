using Unity.Mathematics;
using UnityEngine;
using UTJ.Jobs;

namespace UTJ.Support
{
    public class SpringManagerImporting
    {
        public class SpringManagerSerializer
        {
            public bool isPaused;
            public int simulationFrameRate;
            public float dynamicRatio;
            public Vector3 gravity;
            public float bounce;
            public float friction;
            public bool enableAngleLimits;
            public bool enableCollision;
            public bool enableLengthLimits;
            public bool collideWithGround;
            public float groundHeight;
            public bool windDisabled;
            public float windInfluence;
            public float3 windPower;
            public float3 windDir;
            public float3 distanceRate;
            public bool automaticReset;
            public float resetDistance;
            public float resetAngle;
        }
        
        public static SpringManagerSerializer ManagerToSerializer(SpringJobManager manager)
        {
            return new SpringManagerSerializer
            {
                friction = manager.friction,
                bounce = manager.bounce,
                gravity = manager.gravity,
                dynamicRatio = manager.dynamicRatio,
                simulationFrameRate = manager.simulationFrameRate,
                isPaused = manager.isPaused,
                enableAngleLimits = manager.enableAngleLimits,
                enableCollision = manager.enableCollision,
                enableLengthLimits = manager.enableLengthLimits,
                collideWithGround = manager.collideWithGround,
                groundHeight = manager.groundHeight,
                windDisabled = manager.windDisabled,
                windInfluence = manager.windInfluence,
                windDir = manager.windDir,
                windPower = manager.windPower,
                distanceRate = manager.distanceRate,
                automaticReset = manager.automaticReset,
                resetDistance = manager.resetDistance,
                resetAngle = manager.resetAngle
            };
        }
    }
}