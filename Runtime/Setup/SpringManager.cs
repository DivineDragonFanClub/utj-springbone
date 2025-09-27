using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UTJ.Support.GameObjectExtensions;

namespace UTJ
{
    public class SpringManager : MonoBehaviour
    {
        [Header("Properties")]
        public bool automaticUpdates = true;
        public bool isPaused = false;
        public int simulationFrameRate = 60;
        [Range(0f, 1f)]
        public float dynamicRatio = 0.5f;
        public Vector3 gravity = new Vector3(0f, -10f, 0f);
        [Range(0f, 1f)]
        public float bounce = 0f;
        [Range(0f, 1f)]
        public float friction = 1f;

        [Header("Constraints")]
        public bool enableAngleLimits = true;
        public bool enableCollision = true;
        public bool enableLengthLimits = true;

        [Header("Ground Collision")]
        public bool collideWithGround = true;
        public float groundHeight = 0f;

        [Header("Bones")]
        public SpringBone[] springBones;

        [SerializeField] // RVA: 0x3D4130 Offset: 0x3D4231 VA: 0x3D4130
        public bool windDisabled; // 0x48
        [SerializeField] // RVA: 0x3D41A0 Offset: 0x3D42A1 VA: 0x3D41A0
        public float windInfluence = 1; // 0x4C
        [SerializeField] // RVA: 0x3D4210 Offset: 0x3D4311 VA: 0x3D4210
        public Vector3 distanceRate = new Vector3(30, 0, 0); // 0x50

        public bool automaticReset; // 0x78
        public float resetDistance = 1; // 0x7C
        public float resetAngle = 60; // 0x80
        private bool firstReset; // 0x84
        public bool LodEnable; // 0x85
        private static float[] LodDistance; // 0x20
        private static int LodCount; // 0x28
        private int LodVariation; // 0x88
        [SerializeField]
        public float windTime; // 0x8C
        public Vector3 localWindPower = new Vector3(60, 1, 1); // 0x90
        public float localWindSpeed = 1; // 0x9C
        public Vector3 localWindDir = new Vector3(0.3f, 0, 0); // 0xA0
        private float localApplyTime; // 0xAC
        private float localStartTime; // 0xB0
        private float localDurationTime; // 0xB4
        private float localDecayTime; // 0xB8
        private Vector3 prevFramePosition; // 0xBC
        private Quaternion prevFrameRotation; // 0xC8
        private bool[] boneIsAnimatedStates; // 0xD8

        [SerializeField]
        public Vector3 WindPower
        {
            get
            {
                return localWindPower;
            }
            set
            {
                localWindPower = value;
            }
        }

        [SerializeField]
        public float WindSpeed {
            get
            {
                return localWindSpeed;
            }
            set
            {
                localWindSpeed = value;
            }
        }

        [SerializeField]
        public Vector3 WindDir {
            get
            {
                return localWindDir;
            }
            set
            {
                localWindDir = value;
            }
        }

#if UNITY_EDITOR
        [Header("Gizmos")]
        public Color boneColor = Color.yellow;
        public Color colliderColor = Color.gray;
        public Color collisionColor = Color.red;
        public Color groundCollisionColor = Color.green;
        public float angleLimitDrawScale = 0.05f;

        public bool onlyShowSelectedBones = false;
        public static bool onlyShowSelectedColliders = false;
        public bool showBoneSpheres = true;
        public bool showBoneNames = false;

        // Can't declare as const...
        public static Color DefaultBoneColor { get { return Color.yellow; } }
        public static Color DefaultColliderColor { get { return Color.gray; } }
        public static Color DefaultCollisionColor { get { return Color.red; } }
        public static Color DefaultGroundCollisionColor { get { return Color.green; } }
#endif

        // Removes any nulls from bone colliders
        public void CleanUpBoneColliders()
        {
            foreach (var bone in springBones)
            {
                bone.sphereColliders = bone.sphereColliders.Where(collider => collider != null).ToArray();
                bone.capsuleColliders = bone.capsuleColliders.Where(collider => collider != null).ToArray();
                bone.panelColliders = bone.panelColliders.Where(collider => collider != null).ToArray();
            }
        }

        // Find SpringBones in children and assign them in depth order.
        // Note that the original list will be overwritten.
        public void FindSpringBones(bool includeInactive = false)
        {
            var unsortedSpringBones = GetComponentsInChildren<SpringBone>(includeInactive);
            var boneDepthList = unsortedSpringBones
                .Select(bone => new { bone, depth = GetObjectDepth(bone.transform) })
                .ToList();
            boneDepthList.Sort((a, b) => a.depth.CompareTo(b.depth));
            springBones = boneDepthList.Select(item => item.bone).ToArray();
        }

        // Tells the SpringManager which bones are moved by animation.
        // Can be called on a per-animation basis.
        public void UpdateBoneIsAnimatedStates(IList<string> animatedBoneNames)
        {
            if (boneIsAnimatedStates == null
                || boneIsAnimatedStates.Length != springBones.Length)
            {
                boneIsAnimatedStates = new bool[springBones.Length];
            }

            var boneCount = springBones.Length;
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                boneIsAnimatedStates[boneIndex] = animatedBoneNames.Contains(springBones[boneIndex].name);
            }
        }

        public float UpdateLocalWindRate()
        {
            var timeAlpha = localStartTime + localDurationTime + localDecayTime;

            if (0.0 < timeAlpha) {
                localApplyTime = localApplyTime + Time.deltaTime;

                var wind_rate = Time.deltaTime;

                if (localStartTime <= localApplyTime)
                {
                    wind_rate = localStartTime + localDurationTime;

                    if (localApplyTime < wind_rate)
                    {
                        wind_rate = 1;
                    } else {
                        wind_rate = 1 - (localApplyTime - wind_rate) / localDecayTime;
                    }
                } else {
                    wind_rate = localApplyTime / localStartTime;
                }

                if (timeAlpha <= localApplyTime)
                {
                    localApplyTime = 0;
                    localStartTime = 0;
                    localDurationTime = 0;
                    localDecayTime = 0;
                }

                return Mathf.Clamp(wind_rate, 0, 1);
            }

            return 0;
        }

        public void UpdateDynamics()
        {
            var boneCount = springBones.Length;

            if (isPaused)
            {
                for (var boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    var springBone = springBones[boneIndex];
                    if (springBone.enabled)
                    {
                        springBone.ComputeRotation(boneIsAnimatedStates[boneIndex] ? dynamicRatio : 1f);
                    }
                }
                return;
            }

            var timeStep = (simulationFrameRate > 0) ?
                (1f / simulationFrameRate) :
                Time.deltaTime;

            var wind_rate = UpdateLocalWindRate();

            var windSpeed = (1.0f - wind_rate) * WindSpeed + wind_rate * localWindSpeed;
            this.windTime = (float)(this.windTime + Time.deltaTime * 0.5 * windSpeed);


            for (var boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                var springBone = springBones[boneIndex];
                if (!springBone.enabled)
                {
                    var sumOfForces = gravity;

                    if (!windDisabled)
                    {
                        var transform = springBone.transform;
                        var spring_pos = transform.position;
                        var wind_force = ApplyWindForce(spring_pos, windTime, wind_rate);

                        sumOfForces.x = gravity.x + wind_force.z + springBone.windInfluence + windInfluence;
                        sumOfForces.y = gravity.y + wind_force.y + springBone.windInfluence + windInfluence;
                        sumOfForces.z = gravity.z + wind_force.x + springBone.windInfluence + windInfluence;
                    }
                    
                    springBone.UpdateSpring(timeStep, sumOfForces);
                    springBone.SatisfyConstraintsAndComputeRotation(
                        timeStep, boneIsAnimatedStates[boneIndex] ? dynamicRatio : 1f);
                }
            }
        }

        // private

        private ForceProvider[] forceProviders;

        // Get the depth of an object (number of consecutive parents)
        private static int GetObjectDepth(Transform inObject)
        {
            var depth = 0;
            var currentObject = inObject;
            while (currentObject != null)
            {
                currentObject = currentObject.parent;
                ++depth;
            }
            return depth;
        }

        private Vector3 GetSumOfForcesOnBone(SpringBone springBone)
        {
            var sumOfForces = gravity;
            var providerCount = forceProviders.Length;
            for (var providerIndex = 0; providerIndex < providerCount; ++providerIndex)
            {
                var forceProvider = forceProviders[providerIndex];
                if (forceProvider.isActiveAndEnabled)
                {
                    sumOfForces += forceProvider.GetForceOnBone(springBone);
                }
            }
            return sumOfForces;
        }

        private Vector3 ApplyWindForce(Vector3 pos, float time, float timeAlpha)
        {
            var dis_y = pos.y = distanceRate.y;
            var dis_z = dis_y + pos.z * distanceRate.z + time;
            var dis_x = pos.x * distanceRate.x + dis_z + time;

            var timeAlpha_clamp = Mathf.Clamp01(timeAlpha);

            var perlin_z = Mathf.PerlinNoise(dis_x, time * 0.01f + dis_y);
            var perlin_y = Mathf.PerlinNoise(dis_x, time * 0.02f + dis_y);
            var perlin_x = Mathf.PerlinNoise(dis_z, time * 0.03f + dis_y);

            Vector3 result = new Vector3();

            result.x = (WindPower.z + (localWindPower.z - WindPower.z) * timeAlpha_clamp) * (WindDir.z + (localWindDir.z - WindDir.z) * timeAlpha_clamp + perlin_x - 0.5f);
            result.y = (WindPower.y + (localWindPower.y - WindPower.y) * timeAlpha_clamp) * (WindDir.y + (localWindDir.y - WindDir.y) * timeAlpha_clamp + perlin_y - 0.5f);
            result.z = (WindPower.x + (localWindPower.x - WindPower.x) * timeAlpha_clamp) * (WindDir.x + (localWindDir.x - WindDir.x) * timeAlpha_clamp + perlin_z - 0.5f);

            return result;
        }

        private void Awake()
        {
            FindSpringBones(true);
            var boneCount = springBones.Length;
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                springBones[boneIndex].Initialize(this);
            }
            if (boneIsAnimatedStates == null || boneIsAnimatedStates.Length != boneCount)
            {
                boneIsAnimatedStates = new bool[boneCount];
            }
        }

        private void Start()
        {
            // Must get the ForceProviders in Start and not Awake or Unity will complain that
            // "the scene is not loaded"
            forceProviders = GameObjectUtil.FindComponentsOfType<ForceProvider>().ToArray();
        }

        private void LateUpdate()
        {
            if (automaticUpdates) {
                UpdateDynamics();
            }
        }

#if UNITY_EDITOR
        private Vector3[] groundPoints;

        private void OnDrawGizmos()
        {
            if (collideWithGround)
            {
                if (groundPoints == null)
                {
                    groundPoints = new Vector3[] {
                        new Vector3(-1f, 0f, -1f),
                        new Vector3( 1f, 0f, -1f),
                        new Vector3( 1f, 0f,  1f),
                        new Vector3(-1f, 0f,  1f)
                    };
                }

                var characterPosition = transform.position;
                var groundOrigin = new Vector3(characterPosition.x, groundHeight, characterPosition.z);
                var pointCount = groundPoints.Length;
                Gizmos.color = Color.yellow;
                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    var endPointIndex = (pointIndex + 1) % pointCount;
                    Gizmos.DrawLine(
                        groundOrigin + groundPoints[pointIndex],
                        groundOrigin + groundPoints[endPointIndex]);
                }
            }

            DrawBones();
        }

        private List<SpringBone> selectedBones;
        private Vector3[] boneLines;

        private void DrawBones()
        {
            // Draw each item by color to reduce Material.SetPass calls
            var boneCount = springBones.Length;
            IList<SpringBone> bonesToDraw = springBones;
            if (onlyShowSelectedBones)
            {
                if (selectedBones == null) { selectedBones = new List<SpringBone>(boneCount); }
                selectedBones.Clear();
                var selection = UnityEditor.Selection.gameObjects;
                for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    var bone = springBones[boneIndex];
                    var pivot = (bone.pivotNode != null) ? bone.pivotNode.gameObject : null;
                    if (selection.Contains(bone.gameObject) || selection.Contains(pivot))
                    {
                        selectedBones.Add(springBones[boneIndex]);
                    }
                }
                bonesToDraw = selectedBones;
                boneCount = bonesToDraw.Count;
            }

            UnityEditor.Handles.color = new Color(0.2f, 1f, 0.2f);
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                var bone = bonesToDraw[boneIndex];
                bone.DrawAngleLimits(bone.yAngleLimits, angleLimitDrawScale);
            }

            UnityEditor.Handles.color = new Color(0.7f, 0.7f, 1f);
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                var bone = bonesToDraw[boneIndex];
                bone.DrawAngleLimits(bone.zAngleLimits, angleLimitDrawScale);
            }

            var linePointCount = boneCount * 2;
            if (boneLines == null || boneLines.Length != linePointCount)
            {
                boneLines = new Vector3[linePointCount];
            }

            var pointIndex = 0;
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                var bone = bonesToDraw[boneIndex];
                var origin = bone.transform.position;
                var pivotForward = -bone.GetPivotTransform().right;
                boneLines[pointIndex] = origin;
                boneLines[pointIndex + 1] = origin + angleLimitDrawScale * pivotForward;
                pointIndex += 2;
            }
            UnityEditor.Handles.color = Color.gray;
            UnityEditor.Handles.DrawLines(boneLines);

            pointIndex = 0;
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                var bone = bonesToDraw[boneIndex];
                boneLines[pointIndex] = bone.transform.position;
                boneLines[pointIndex + 1] = bone.ComputeChildPosition();
                pointIndex += 2;
            }
            UnityEditor.Handles.color = boneColor;
            UnityEditor.Handles.DrawLines(boneLines);

            if (showBoneSpheres)
            {
                Gizmos.color = new Color(0f, 0f, 0f, 0f);
                for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    bonesToDraw[boneIndex].DrawSpringBoneCollision();
                }
            }

            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                bonesToDraw[boneIndex].MarkCollidersForDrawing();
            }

            if (showBoneNames)
            {
                for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
                {
                    var bone = bonesToDraw[boneIndex];
                    UnityEditor.Handles.Label(bone.transform.position, bone.name);
                }
            }
        }
#endif
    }
}
