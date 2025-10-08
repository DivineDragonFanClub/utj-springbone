using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using FUtility;
using Unity.Mathematics;

#if UNITY_EDITOR
using System;

using System.Reflection;
using UnityEditor;
#endif
namespace UTJ.Jobs {
	/// <summary>
	/// Job版SpringManager
	/// </summary>
	public class SpringJobManager : ISpringManager {
		#region MEMBER
		[Header("Optional Settings")] 
		public bool optimizeTransform = false; // NOTE: 多くの場合上手く行かないかも
		[Header("Properties")]
		public bool isPaused = false;
		[Tooltip("Controls physics calculation frequency. 60 in vanilla bundles")]
		public int simulationFrameRate = 60;
		[Range(0f, 1f)]
		[Tooltip("Amplifies or reduces overall force values. Default 1 - Higher values can cause jerky movement but make bones move quickly")]
		public float dynamicRatio = 1f;
		[Tooltip("Persistent force simulating gravity. Usually Y -10. Use positive Y for floating cloth")]
		public Vector3 gravity = new Vector3(0f, -10f, 0f);
		[Range(0f, 1f)]
		[Tooltip("Bounce-back force on spring bones. 0 in vanilla. Can be used for bouncy physics like bust physics")]
		public float bounce = 0f;
		[Range(0f, 1f)]
		[Tooltip("Damping for movement. Low = stays in motion longer. High = stops quickly")]
		public float friction = 1f;
		public float time = 0f;

		[Header("Constraints")]
		[Tooltip("Enable rotation constraints on spring bones")]
		public bool enableAngleLimits = true;
		[Tooltip("Enable collision detection")]
		public bool enableCollision = true;
		[Tooltip("Enable length limit constraints")]
		public bool enableLengthLimits = true;

		[Header("Ground Collision")]
		[Tooltip("Enable collision with ground plane")]
		public bool collideWithGround = false;
		public float groundHeight = 0f;

		[Tooltip("Disable all wind effects globally")]
		public bool windDisabled = false;
		[Tooltip("Global wind multiplier. Use low/zero for heavy objects. 1 in vanilla")]
		public float windInfluence = 1f;

		[Tooltip("Wind direction and strength. For cloth: (30, 1, 1) in vanilla. Different for special physics")]
		public float3 windPower = new float3(30, 1, 1);
		[Tooltip("Wind rotation. (0.3, 0, 0) in vanilla outfits")]
		public float3 windDir = new float3(0.3f, 0, 0);
		[Tooltip("Wind blow rate. Low = consistent full power. High = sporadic low power. X 30 for sway in vanilla")]
		public float3 distanceRate = new float3(30, 0, 0);

		[Header("Reset")]
		[Tooltip("Automatically reset bones when deformation exceeds thresholds")]
		public bool automaticReset = true;
		[Tooltip("Maximum bone deformation before position reset. 1 in vanilla")]
		public float resetDistance = 1f;
		[Tooltip("Maximum bone deformation angle (degrees) before reset. 60 in vanilla")]
		public float resetAngle = 60f;

#pragma warning disable CS0414 // Field is assigned but its value is never used
		private bool resetRequest;
#pragma warning restore CS0414 // Field is assigned but its value is never used
		private bool firstReset;
		private Vector3 prevFramePosition;
		private Quaternion prevFrameRotation;

		private SpringJobElement job;
		private int jobNo;
		private int boneIndex, colIndex, lengthIndex;
		private NestedNativeArray<SpringBoneProperties> properties;
		private NestedNativeArray<SpringColliderProperties> colProperties;
		private NestedNativeArray<float3> lengthLimitTargets;

		private bool entryJob;

		// NOTE: ランタイム時に行わなくても良いボーンの初期化データは全部用意しておく
		//       その分Job化したら再編集は不可とする（サポートするのは今回パス）
		[SerializeField]
		public SpringBone[] SortedBones = null;
		[SerializeField]
		public SpringCollider[] jobColliders = null;
		[SerializeField]
		public SpringBoneProperties[] jobProperties = null;
		[SerializeField]
		public Quaternion[] initLocalRotations = null;
		[SerializeField]
		public SpringColliderProperties[] jobColProperties = null;
		[SerializeField]
		public LengthLimitProperties[] jobLengthProperties = null;
		#endregion


		#region PROPERTY
		/// <summary>
		/// 初期化済か
		/// </summary>
		public bool initialized { get; set; }
        #endregion


        private static int GetObjectDepth(Transform inObject) {
			var depth = 0;
			var currentObject = inObject;
			while (currentObject != null) {
				currentObject = currentObject.parent;
				++depth;
			}
			return depth;
		}
		// Find SpringBones in children and assign them in depth order.
		public SpringBone[] FindSpringBones(bool includeInactive = false) {
			var unsortedSpringBones = GetComponentsInChildren<SpringBone>(includeInactive);
			var boneDepthList = unsortedSpringBones
				.Select(bone => new { bone, depth = GetObjectDepth(bone.transform) })
				.ToList();
			boneDepthList.Sort((a, b) => a.depth.CompareTo(b.depth));
			return boneDepthList.Select(item => item.bone).ToArray();
		}

		/// <summary>
		/// コンバート時に行えるデータ作成はSerializedFiledにぶち込んでランタイムの初期化を削減する
		/// </summary>
		public void CachedJobParam() {
			this.SortedBones = this.FindSpringBones();
			var nSpringBones = this.SortedBones.Length;

			this.jobProperties = new SpringBoneProperties[nSpringBones];
			this.initLocalRotations = new Quaternion[nSpringBones];
			this.jobColProperties = new SpringColliderProperties[nSpringBones];
			//this.jobLengthProperties = new LengthLimitProperties[nSpringBones][];
			var jobLengthPropertiesList = new List<LengthLimitProperties>();

			for (var i = 0; i < nSpringBones; ++i) {
				SpringBone springBone = this.SortedBones[i];
				springBone.index = i;

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
				//this.jobLengthProperties[i] = new LengthLimitProperties[targetCount];
				if (targetCount > 0) {
					for (int m = 0; m < targetCount; ++m) {
						var targetRoot = springBone.lengthLimitTargets[m];
						int targetIndex = -1;
						// NOTE: SpringBoneの子のTransformをLengthLimitにするのは想定してない
						if (targetRoot.TryGetComponent<SpringBone>(out var targetBone))
							targetIndex = targetBone.index;
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
				if (parent.TryGetComponent<SpringBone>(out var parentBone))
					parentIndex = parentBone.index;

				var pivotIndex = -1;
				var pivotTransform = springBone.GetPivotTransform();
				var pivotBone = pivotTransform.GetComponentInParent<SpringBone>();
				if (pivotBone != null) {
					pivotIndex = pivotBone.index;
					// NOTE: PivotがSpringBoneの子供に置かれている場合の対処
					if (pivotBone.transform != pivotTransform) {
						// NOTE: 1個上の親がSpringBoneとは限らない
						//pivotLocalMatrix = Matrix4x4.TRS(pivotTransform.localPosition, pivotTransform.localRotation, Vector3.one);
						pivotLocalMatrix = Matrix4x4.Inverse(pivotBone.transform.localToWorldMatrix) * pivotTransform.localToWorldMatrix;
					}
				}

				// ReadOnly
				this.jobProperties[i] = new SpringBoneProperties {
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

				this.initLocalRotations[i] = root.localRotation;

				// turn off SpringBone component to let Job work
				springBone.enabled = false;
			}

			// Colliders
			this.jobColliders = this.GetComponentsInChildren<SpringCollider>(true);
			int nColliders = this.jobColliders.Length;
			for (int i = 0; i < nColliders; ++i) {
				this.jobColliders[i].index = i;
				var comp = new SpringColliderProperties() {
					type = this.jobColliders[i].type,
					radius = this.jobColliders[i].radius,
					width = this.jobColliders[i].width,
					height = this.jobColliders[i].height,
				};
				this.jobColProperties[i] = comp;
			}

			// LengthLimits
			this.jobLengthProperties = jobLengthPropertiesList.ToArray();
		}

		/// <summary>
		/// 初期化
		/// </summary>
		internal bool Initialize(SpringJobScheduler scheduler) {
			if (this.initialized)
				return false;

			this.initialized = true;

			Debug.Log("SpringJobManager Initialize : " + this.name);

			// Bones
			var springBones = this.SortedBones;

			var nSpringBones = springBones.Length;
			var propAlloc = scheduler.properties.AllocNestedArray(nSpringBones, out this.boneIndex, out this.properties);
			if (!propAlloc) {
				Debug.LogError("Spring Boneのボーン数上限が不足しています");
				this.Final(scheduler);
				return false;
			}

			int lengthTargetIndex = 0;
			List<Transform> allLengthLimitList = new List<Transform>(128);
			for (var i = 0; i < nSpringBones; ++i) {
				SpringBone springBone = springBones[i];

				// Length Limit
				var nestedLengthLimitProps = new NestedNativeArray<LengthLimitProperties>();
				var nLengthLimitTargets = springBone.lengthLimitTargets.Length;
				if (nLengthLimitTargets > 0) {
					bool lengthAlloc = scheduler.lengthProperties.AllocNestedArray(nLengthLimitTargets, out var temp, out nestedLengthLimitProps);
					if (!lengthAlloc) {
						Debug.LogError("Spring Boneの距離制限上限が不足しています");
						return false;
					}
					for (int m = 0; m < nLengthLimitTargets; ++m) {
						nestedLengthLimitProps[m] = this.jobLengthProperties[lengthTargetIndex];
						var targetTransform = springBone.lengthLimitTargets[m];
						scheduler.lengthLimitTransforms[this.lengthIndex + m] = targetTransform;
						allLengthLimitList.Add(targetTransform);
						++lengthTargetIndex;
					}
				}

				// Colliders Index
				var nestedCollisionNumbers = new NestedNativeArray<int>();
				var nSpringBoneColliders = springBone.jobColliders.Length;
				if (nSpringBoneColliders > 0) {
					bool colNumAlloc = scheduler.colNumbers.AllocNestedArray(nSpringBoneColliders, out var temp, out nestedCollisionNumbers);
					if (!colNumAlloc) {
						Debug.LogError("Spring Boneのコリジョンインデックス上限が不足しています");
						return false;
					}
					for (int m = 0; m < nSpringBoneColliders; ++m)
						nestedCollisionNumbers[m] = springBone.jobColliders[m].index;
				}

				// ReadOnly
				var prop = this.jobProperties[i];
				prop.collisionNumbers = nestedCollisionNumbers;
				prop.lengthLimitProps = nestedLengthLimitProps;
				this.properties[i] = prop;

				int fullBoneIndex = this.boneIndex + i;

				// Read/Write（initialize param）
				// NOTE: ワールド座標依存なので再計算
				Vector3 tipPosition = springBone.ComputeChildPosition();
				scheduler.components[fullBoneIndex] = new SpringBoneComponents {
					currentTipPosition = tipPosition,
					previousTipPosition = tipPosition,
					localRotation = this.initLocalRotations[i],
				};

				// TransformArray
				var root = springBone.transform;
				scheduler.boneTransforms[fullBoneIndex] = root;
				scheduler.boneParentTransforms[fullBoneIndex] = root.parent;
				scheduler.bonePivotTransforms[fullBoneIndex] = springBone.GetPivotTransform();
			}

			// Colliders
			int nColliders = this.jobColliders.Length;
			if (nColliders > 0) {
				var colPropAlloc = scheduler.colProperties.AllocNestedArray(nColliders, out this.colIndex, out this.colProperties);
				if (!colPropAlloc) {
					Debug.LogError("不明なエラー");
					this.Final(scheduler);
					return false;
				}
				for (int i = 0; i < nColliders; ++i) {
					this.colProperties[i] = this.jobColProperties[i];
					scheduler.colliderTransforms[this.colIndex + i] = this.jobColliders[i].transform;

					// 本体についているコライダーの削除
					if (this.optimizeTransform) {
						UnityEngine.Object.DestroyImmediate(this.jobColliders[i]);
						this.jobColliders = null;
					}
				}
			}
			// LengthLimits
			var nLengthLimits = allLengthLimitList.Count;
			if (nLengthLimits > 0) {
				var lenghCompAlloc = scheduler.lengthLimitTargets.AllocNestedArray(nLengthLimits, out this.lengthIndex, out this.lengthLimitTargets);
				if (!lenghCompAlloc) {
					Debug.LogError("不明なエラー");
					this.Final(scheduler);
					return false;
				}
				for (int i = 0; i < allLengthLimitList.Count; ++i)
					this.lengthLimitTargets[i] = allLengthLimitList[i].position;
			} else {
				this.lengthLimitTargets = default;
			}

			this.job.deltaTime = (this.simulationFrameRate > 0) ? (1f / this.simulationFrameRate) : 1f / 60f;
			this.job.dynamicRatio = this.dynamicRatio;
			this.job.gravity = this.gravity;
			this.job.bounce = this.bounce;
			this.job.friction = this.friction;
			this.job.enableAngleLimits = this.enableAngleLimits;
			this.job.enableCollision = this.enableCollision;
			this.job.enableLengthLimits = this.enableLengthLimits;
			this.job.collideWithGround = this.collideWithGround;
			this.job.groundHeight = this.groundHeight;

			this.job.windDisabled = this.windDisabled;
			this.job.windInfluence = this.windInfluence;
			this.job.windPower = this.windPower;
			this.job.windDir = this.windDir;
			this.job.distanceRate = this.distanceRate;

			this.job.nestedProperties = this.properties;
			this.job.nestedComponents = new NestedNativeArray<SpringBoneComponents>(scheduler.components, this.boneIndex, this.properties.Length);
			this.job.nestedParentComponents = new NestedNativeArray<float4x4>(scheduler.parentComponents, this.boneIndex, this.properties.Length);
			this.job.nestedPivotComponents = new NestedNativeArray<float4x4>(scheduler.pivotComponents, this.boneIndex, this.properties.Length);
			this.job.nestedColliderProperties = this.colProperties;
			this.job.nestedColliderComponents = new NestedNativeArray<SpringColliderComponents>(scheduler.colComponents, this.colIndex, this.colProperties.Length);
			this.job.nestedLengthLimitTargets = this.lengthLimitTargets;

			// Transformの階層構造をバラす
			if (this.optimizeTransform)
				AnimatorUtility.OptimizeTransformHierarchy(this.gameObject, null);

			return true;
		}

		/// <summary>
		/// 破棄
		/// </summary>
		internal void Final(SpringJobScheduler scheduler) {
			if (!this.initialized)
				return;

			Debug.Log("SpringJobManager Final : " + this.name);

			// NOTE: TransformをnullにしておくことでIJobParallelForTransformのParallel処理が効率化する
			//       null、または非アクティブなTransformAccessは内部でスキップされる
			for (int i = 0; i < this.properties.Length; ++i) {
				int index = this.boneIndex + i;
				scheduler.boneTransforms[index] = null;
				scheduler.boneParentTransforms[index] = null;
				scheduler.bonePivotTransforms[index] = null;
				scheduler.colNumbers.Free(this.properties[i].collisionNumbers);
				scheduler.lengthProperties.Free(this.properties[i].lengthLimitProps);
			}
			for (int i = 0; i < this.colProperties.Length; ++i)
				scheduler.colliderTransforms[this.colIndex + i] = null;
			for (int i = 0; i < this.lengthLimitTargets.Length; ++i)
				scheduler.lengthLimitTransforms[this.lengthIndex + i] = null;

			// Poolへ返却
			scheduler.properties.Free(this.properties);
			scheduler.colProperties.Free(this.colProperties);
			scheduler.lengthLimitTargets.Free(this.lengthLimitTargets);

			this.initialized = false;
		}

		/// <summary>
		/// Jobデータの取得
		/// </summary>
		/// <returns></returns>
		public SpringJobElement GetElement() {
			this.job.isPaused = this.isPaused;
			this.job.deltaTime = (this.simulationFrameRate > 0) ? (1f / this.simulationFrameRate) : Time.deltaTime;
			this.job.dynamicRatio = this.dynamicRatio;
			this.job.gravity = this.gravity;
			this.job.bounce = this.bounce;
			this.job.friction = this.friction;
			this.job.enableAngleLimits = this.enableAngleLimits;
			this.job.enableCollision = this.enableCollision;
			this.job.enableLengthLimits = this.enableLengthLimits;
			this.job.collideWithGround = this.collideWithGround;
			this.job.groundHeight = this.groundHeight;

			this.job.windDisabled = this.windDisabled;
			this.job.windInfluence = this.windInfluence;
			this.job.windPower = this.windPower;
			this.job.windDir = this.windDir;
			this.job.distanceRate = this.distanceRate;

			return this.job;
		}

		void OnEnable() {
			// TODO: 毎回Initializeが呼ばれるので無駄
			SpringJobScheduler.Entry(this);
		}

		void OnDisable() {
			// TODO: 毎回Finalが呼ばれるので無駄
			SpringJobScheduler.Exit(this);
		}

		void OnDestroy() {
			SpringJobScheduler.Exit(this);
		}

		public override void Stabilize() { }

		// RVA: -1 Offset: -1 Slot: 5
		public override void UpdateSimulation() {
			this.resetRequest = true;
		}

		// RVA: -1 Offset: -1 Slot: 6
		public override void ResetSimulation() { }

		// RVA: -1 Offset: -1 Slot: 7
		public override void SetGravity(Vector3 gravity) { }

		// RVA: -1 Offset: -1 Slot: 8
		public override void SetGroundHeight(float groundHeight) { }

		// RVA: -1 Offset: -1 Slot: 9
		public override void SetDefaultWindParameter() { }

		// RVA: -1 Offset: -1 Slot: 10
		public override void SetWindPower(Vector3 windPower) { }

		// RVA: -1 Offset: -1 Slot: 11
		public override void SetWindSpeed(float windSpeed) { }

		// RVA: -1 Offset: -1 Slot: 12
		public override void SetWindDir(Vector3 windDir) { }

		// RVA: -1 Offset: -1 Slot: 13
		public override void ApplyLocalWind(Vector3 windPower, float windSpeed, Vector3 windDir, float startTime = 0.2f, float durationTime = 1, float decayTime = 0.2f) {

		}
	
		// RVA: -1 Offset: -1 Slot: 14
		public override void ReplaceJobManager() {

		}

#if UNITY_EDITOR
        private void OnValidate() {
            UpdateBoneList(this);
        }

        public static void UpdateBoneList(SpringJobManager manager)
        {
            SpringBoneSetupUTJ.FindAndAssignSpringBones(manager, true);
            CachedJobParam(manager);
            EditorUtility.SetDirty(manager);
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
                
                // Length Limit
                var targetCount = (springBone.lengthLimitTargets != null) ? springBone.lengthLimitTargets.Length : 0;                //manager.jobLengthProperties[i] = new LengthLimitProperties[targetCount];
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
                int pivotIndex = -1;

                Matrix4x4 pivotLocalMatrix = Matrix4x4.identity;
                if (parent.TryGetComponent<SpringBone>(out var parentBone))
                {
                    parentIndex = Array.FindIndex(manager.SortedBones, b => b == parentBone);
                    pivotIndex = parentIndex;
                }

                var pivotTransform = GetPivotTransform(springBone);
                var pivotBone = pivotTransform.GetComponentInParent<SpringBone>();
                if (pivotBone != null)
                {
//        var nsbEnabledJobField = nsb.GetType().GetField("enabledJobSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
// 
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
        public static Transform GetPivotTransform(SpringBone bone)
        {
            if (bone.pivotNode == null)
            {
                bone.pivotNode = bone.transform.parent ?? bone.transform;
            }
            return bone.pivotNode;
        }
#endif
	}
}
