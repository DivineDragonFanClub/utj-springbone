using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using FUtility;

namespace UTJ.Jobs {
	/// <summary>
	/// ボーン毎の設定値（ReadOnly in Job）
	/// </summary>
	[System.Serializable]
	public struct SpringBoneProperties {
		public float stiffnessForce;
		public float dragForce;
		public float3 springForce;
		public float windInfluence;
		public float angularStiffness;
		public AngleLimitComponent yAngleLimits;
		public AngleLimitComponent zAngleLimits;
		public float radius;
		public float springLength;
		public float3 boneAxis;
		public float3 localPosition;
		public quaternion initialLocalRotation;
		public int parentIndex; // 親がSpringBoneだった場合のIndex（違う場合 -1）

		public int pivotIndex;  // PivotがSpringBoneだった場合のIndex（違う場合 -1）
		public float4x4 pivotLocalMatrix;

		public NestedNativeArray<int> collisionNumbers;
		public NestedNativeArray<LengthLimitProperties> lengthLimitProps;
	}

	/// <summary>
	/// 更新されるボーン毎の値（Read/Write in Job）
	/// </summary>
	public struct SpringBoneComponents {
		public Vector3 currentTipPosition;
		public Vector3 previousTipPosition;
		public Quaternion localRotation;
		public Vector3 position;
		public Quaternion rotation;
	}

	/// <summary>
	/// 距離制限の設定値
	/// </summary>
	[System.Serializable]
	public struct LengthLimitProperties {
		public int targetIndex; // targetがSpringBoneだった場合のIndex（違う場合 -1）
		public float target;
	}

	/// <summary>
	/// SpringBone計算ジョブ
	/// </summary>
	[Unity.Burst.BurstCompile]
	public struct SpringJob : IJobParallelFor {
		[ReadOnly] public NativeArray<SpringJobElement> jobManagers;
		[ReadOnly] public NativeArray<SpringForceComponent> forces;
		public int forceCount;

		/// <summary>
		/// ジョブ実行
		/// </summary>
		void IJobParallelFor.Execute(int index) {
			this.jobManagers[index].Execute(forces, forceCount);
		}
	}

	/// <summary>
	/// SpringManager単位の計算
	/// </summary>
	public struct SpringJobElement {
		// JobManager settings
		public bool isPaused;
		public float deltaTime;
		public float dynamicRatio;
		public float3 gravity;
		public float bounce;
		public float friction;
		public bool enableAngleLimits;
		public bool enableCollision;
		public bool enableLengthLimits;
		public bool collideWithGround;
		public float groundHeight;
		public bool windDisabled; // 0x28
		public float windInfluence; // 0x2C
		public float3 windPower; // 0x30
		public float3 windDir; // 0x3C
		public float3 distanceRate; // 0x48

		public NestedNativeArray<SpringBoneProperties> nestedProperties;
		public NestedNativeArray<SpringBoneComponents> nestedComponents;
		public NestedNativeArray<float4x4> nestedParentComponents;
		public NestedNativeArray<float4x4> nestedPivotComponents;
		public NestedNativeArray<SpringColliderProperties> nestedColliderProperties;
		public NestedNativeArray<SpringColliderComponents> nestedColliderComponents;
		public NestedNativeArray<float3> nestedLengthLimitTargets;

		/// <summary>
		/// ジョブ実行
		/// </summary>
		public void Execute(NativeArray<SpringForceComponent> forces, int forceCount) {
			if (this.isPaused)
				return;

			var length = this.nestedProperties.Length;
			for (int i = 0; i < length; ++i) {
				var bone = this.nestedComponents[i];
				var prop = this.nestedProperties[i];

				Quaternion parentRot;
				if (prop.parentIndex >= 0) {
					// 親ノードがSpringBoneなら演算結果を反映する
					var parentBone = this.nestedComponents[prop.parentIndex];
					parentRot = parentBone.rotation;
					bone.position = parentBone.position + parentBone.rotation * prop.localPosition;
					bone.rotation = parentRot * bone.localRotation;
				} else {
					Matrix4x4 parentMat = this.nestedParentComponents[i];
					parentRot = parentMat.rotation;
					bone.position = parentMat.MultiplyPoint3x4(prop.localPosition);
					//bone.position = new Vector3(parentMat.m03, parentMat.m13, parentMat.m23) + parentRot * prop.localPosition;
					bone.rotation = parentRot * bone.localRotation;
				}

				var baseWorldRotation = parentRot * prop.initialLocalRotation;
				var force = this.GetTotalForceOnBone(in bone, in prop, forces, forceCount);
				this.UpdateSpring(ref bone, in prop, baseWorldRotation, force);
				this.ResolveCollisionsAndConstraints(ref bone, in prop, i, this.nestedComponents);
				this.UpdateRotation(ref bone, in prop, baseWorldRotation);
				
				// NOTE: 子の為に更新する
				bone.rotation = parentRot * bone.localRotation;

				this.nestedComponents[i] = bone;
			}
		}

		private void UpdateSpring(ref SpringBoneComponents bone, in SpringBoneProperties prop, Quaternion baseWorldRotation, Vector3 externalForce) {
			var orientedInitialPosition = bone.position + baseWorldRotation * prop.boneAxis * prop.springLength;

			// Hooke's law: force to push us to equilibrium
			var force = prop.stiffnessForce * (orientedInitialPosition - bone.currentTipPosition);
			force += (Vector3)prop.springForce + externalForce;
			var sqrDt = this.deltaTime * this.deltaTime;
			force *= 0.5f * sqrDt;

			var temp = bone.currentTipPosition;
			force += (1f - prop.dragForce) * (bone.currentTipPosition - bone.previousTipPosition);
			bone.currentTipPosition += force;
			bone.previousTipPosition = temp;

			// Inlined because FixBoneLength is slow
			var headPosition = bone.position;
			var headToTail = bone.currentTipPosition - headPosition;
			var magnitude = Vector3.Magnitude(headToTail);

			const float MagnitudeThreshold = 0.001f;
			if (magnitude <= MagnitudeThreshold) {
				Matrix4x4 mat = Matrix4x4.TRS(bone.position, bone.rotation, Vector3.one);
				headToTail = mat.MultiplyVector(prop.boneAxis);
			} else {
				headToTail /= magnitude;
			}

			bone.currentTipPosition = headPosition + prop.springLength * headToTail;
		}

		private void ResolveCollisionsAndConstraints(ref SpringBoneComponents bone, in SpringBoneProperties prop, int index, NestedNativeArray<SpringBoneComponents> boneComponents) {
			if (this.enableLengthLimits)
				this.ApplyLengthLimits(ref bone, in prop, boneComponents);

			var hadCollision = false;

			if (this.collideWithGround)
				hadCollision = this.ResolveGroundCollision(ref bone, in prop);

			if (this.enableCollision && !hadCollision)
				this.ResolveCollisions(ref bone, in prop);

			if (this.enableAngleLimits) {
				Matrix4x4 pivotLocalToWorld;
				if (prop.pivotIndex >= 0) {
					var pivotBone = boneComponents[prop.pivotIndex];
					pivotLocalToWorld = (float4x4)Matrix4x4.TRS(pivotBone.position, pivotBone.rotation, Vector3.one) * prop.pivotLocalMatrix;
				} else {
					pivotLocalToWorld = this.nestedPivotComponents[index];
				}
				this.ApplyAngleLimits(ref bone, in prop, in pivotLocalToWorld);
			}
		}

		// Returns the new tip position
		private void ApplyLengthLimits(ref SpringBoneComponents bone, in SpringBoneProperties prop, NestedNativeArray<SpringBoneComponents> boneComponents) {
			var length = prop.lengthLimitProps.Length;
			if (length == 0)
				return;

			const float SpringConstant = 0.5f;
			var accelerationMultiplier = SpringConstant * this.deltaTime * this.deltaTime;
			var movement = Vector3.zero;
			for (int i = 0; i < length; ++i) {
				var limit = prop.lengthLimitProps[i];
				var lengthToLimitTarget = limit.target;
				// TODO: pivotの時と同様にLengthLimitノードがSpringBoneの下についていた場合は反映が遅れてる、そのようなケースがある？
				Vector3 limitPosition = (limit.targetIndex >= 0) ? boneComponents[limit.targetIndex].position
															 : (Vector3)this.nestedLengthLimitTargets[i];
				var currentToTarget = bone.currentTipPosition - limitPosition;
				var currentDistanceSquared = Vector3.SqrMagnitude(currentToTarget);

				// Hooke's Law
				var currentDistance = Mathf.Sqrt(currentDistanceSquared);
				var distanceFromEquilibrium = currentDistance - lengthToLimitTarget;
				movement -= accelerationMultiplier * distanceFromEquilibrium * Vector3.Normalize(currentToTarget);
			}

			bone.currentTipPosition += movement;
		}

		private bool ResolveGroundCollision(ref SpringBoneComponents bone, in SpringBoneProperties prop) {
			var groundHeight = this.groundHeight;
			// Todo: this assumes a flat ground parallel to the xz plane
			var worldHeadPosition = bone.position;
			var worldTailPosition = bone.currentTipPosition;
			// NOTE: スケールが反映されないのだからスカラー値が欲しいなら行列演算する意味はないのでは…？
			var worldRadius = prop.radius;//transform.TransformDirection(prop.radius, 0f, 0f).magnitude;
			var worldLength = Vector3.Magnitude(bone.currentTipPosition - worldHeadPosition);
			worldHeadPosition.y -= groundHeight;
			worldTailPosition.y -= groundHeight;

			var collidingWithGround = SpringCollisionResolver.ResolvePanelOnAxis(
				worldHeadPosition, ref worldTailPosition, worldLength, worldRadius, SpringCollisionResolver.Axis.Y);

			if (collidingWithGround) {
				worldTailPosition.y += groundHeight;
				bone.currentTipPosition = FixBoneLength(ref bone, in prop, in worldTailPosition);
				// Todo: bounce, friction
				bone.previousTipPosition = bone.currentTipPosition;
			}

			return collidingWithGround;
		}

		private static Vector3 FixBoneLength(ref SpringBoneComponents bone, in SpringBoneProperties prop, in Vector3 tailPosition) {
			var minLength = 0.5f * prop.springLength;
			var maxLength = prop.springLength;
			var headPosition = bone.position;
			var headToTail = tailPosition - headPosition;
			var magnitude = headToTail.magnitude;

			const float MagnitudeThreshold = 0.001f;
			if (magnitude <= MagnitudeThreshold) {
				Matrix4x4 mat = Matrix4x4.TRS(bone.position, bone.rotation, Vector3.one);
				return headPosition + mat.MultiplyVector(prop.boneAxis) * minLength;
			}

			var newMagnitude = (magnitude < minLength) ? minLength : magnitude;
			newMagnitude = (newMagnitude > maxLength) ? maxLength : newMagnitude;
			return headPosition + (newMagnitude / magnitude) * headToTail;
		}

		private bool ResolveCollisions(ref SpringBoneComponents bone, in SpringBoneProperties prop) {
			var desiredPosition = bone.currentTipPosition;
			var headPosition = bone.position;

			// var scaledRadius = transform.TransformDirection(radius, 0f, 0f).magnitude;
			// var scaleMagnitude = new Vector3(prop.radius, 0f, 0f).magnitude;
			var hitNormal = new Vector3(0f, 0f, 1f);

			var hadCollision = false;

			var length = prop.collisionNumbers.Length;
			for (var i = 0; i < length; ++i) {
				var num = prop.collisionNumbers[i];
				var collider = this.nestedColliderProperties[num];
				var colliderTransform = this.nestedColliderComponents[num];

				switch (collider.type) {
					case ColliderType.Capsule:
						hadCollision |= SpringCollisionResolver.ResolveCapsule(
							collider, colliderTransform,
							headPosition, ref bone.currentTipPosition, ref hitNormal,
							prop.radius);
						break;
					case ColliderType.Sphere:
						hadCollision |= SpringCollisionResolver.ResolveSphere(
							collider, colliderTransform,
							headPosition, ref bone.currentTipPosition, ref hitNormal, prop.radius);
						break;
					case ColliderType.Panel:
						hadCollision |= SpringCollisionResolver.ResolvePanel(
							collider, colliderTransform,
							headPosition, ref bone.currentTipPosition, ref hitNormal, prop.springLength,
							prop.radius);
						break;
				}
			}

			if (hadCollision) {
				var incidentVector = desiredPosition - bone.previousTipPosition;
				var reflectedVector = Vector3.Reflect(incidentVector, hitNormal);

				// friction
				var upwardComponent = Vector3.Dot(reflectedVector, hitNormal) * hitNormal;
				var lateralComponent = reflectedVector - upwardComponent;

				var bounceVelocity = this.bounce * upwardComponent + (1f - this.friction) * lateralComponent;
				const float BounceThreshold = 0.0001f;
				if (Vector3.SqrMagnitude(bounceVelocity) > BounceThreshold) {
					var distanceTraveled = Vector3.Magnitude(bone.currentTipPosition - bone.previousTipPosition);
					bone.previousTipPosition = bone.currentTipPosition - bounceVelocity;
					bone.currentTipPosition += Mathf.Max(0f, Vector3.Magnitude(bounceVelocity) - distanceTraveled) * Vector3.Normalize(bounceVelocity);
				} else {
					bone.previousTipPosition = bone.currentTipPosition;
				}
			}
			return hadCollision;
		}

		private void ApplyAngleLimits(ref SpringBoneComponents bone, in SpringBoneProperties prop, in Matrix4x4 pivotLocalToWorld) {
			if (!prop.yAngleLimits.active && !prop.zAngleLimits.active)
				return;

			var origin = bone.position;
			var vector = bone.currentTipPosition - origin;

			var forward = pivotLocalToWorld * -Vector3.right;

			var mulBack = pivotLocalToWorld * Vector3.back;
			var mulDown = pivotLocalToWorld * Vector3.down;

			if (prop.yAngleLimits.active) {
				vector = prop.yAngleLimits.ConstrainVector(
							vector,
							mulDown, //pivotLocalToWorld * -Vector3.up,
							mulBack, //pivotLocalToWorld * -Vector3.forward,
							forward, prop.angularStiffness, this.deltaTime);
			}

			if (prop.zAngleLimits.active) {
				vector = prop.zAngleLimits.ConstrainVector(
							vector,
							mulBack, //pivotLocalToWorld * -Vector3.forward,
							mulDown, //pivotLocalToWorld * -Vector3.up,
							forward, prop.angularStiffness, this.deltaTime);
			}

			bone.currentTipPosition = origin + vector;
		}

		private void UpdateRotation(ref SpringBoneComponents bone, in SpringBoneProperties prop, Quaternion baseWorldRotation) {
			if (float.IsNaN(bone.currentTipPosition.x)
				| float.IsNaN(bone.currentTipPosition.y)
				| float.IsNaN(bone.currentTipPosition.z))
			{
				bone.currentTipPosition = bone.position + baseWorldRotation * prop.boneAxis * prop.springLength;
				bone.previousTipPosition = bone.currentTipPosition;
				Debug.LogError("SpringBone : currentTipPosition is NaN.");
			}

			var actualLocalRotation = ComputeLocalRotation(in baseWorldRotation, ref bone, in prop);
			bone.localRotation = Quaternion.Lerp(bone.localRotation, actualLocalRotation, this.dynamicRatio);
		}

		public static quaternion FromToRotation(float3 from, float3 to) {
			return (quaternion)Quaternion.FromToRotation(from, to);
		}

		private Quaternion ComputeLocalRotation(in Quaternion baseWorldRotation, ref SpringBoneComponents bone, in SpringBoneProperties prop) {
			var worldBoneVector = bone.currentTipPosition - bone.position;
			var localBoneVector = Quaternion.Inverse(baseWorldRotation) * worldBoneVector;
			localBoneVector.Normalize();

			var aimRotation = Quaternion.FromToRotation(prop.boneAxis, localBoneVector);
			var outputRotation = prop.initialLocalRotation * aimRotation;

			return outputRotation;
		}

		private float3 ApplyWindForce(in float3 pos, float time) {
			return noise.snoise(pos);
		}

		private Vector3 GetTotalForceOnBone(in SpringBoneComponents bone, in SpringBoneProperties prop, NativeArray<SpringForceComponent> forces, int forceCount) {
			//var sumOfForces = this.gravity;
			//for (var i = 0; i < forceCount; i++) {
			//	var force = forces[i];
			//	sumOfForces += (float3)ComputeForceOnBone(in force, in bone, prop.windInfluence);
			//}

			var force_idx = 0;
			var total_force_z = gravity.x;
			var total_force_y = gravity.y;
			var total_force_x = gravity.z;

			if (0 < forceCount)
            {
				var force_len = forceCount;

				do {
					if (!windDisabled)
					{
						var bone_pos_dist_y = bone.position.y * distanceRate.y;
						var time = forces[force_idx].time;

						var fVar6 = time + bone.position.x * distanceRate.x + bone_pos_dist_y;
						var fVar5 = time + bone_pos_dist_y + bone.position.z * distanceRate.z;
						bone_pos_dist_y = noise.snoise(new Vector2(0, (float)(time * 0.02 + fVar5)));
						var fVar3 = windDir.x;
						var fVar2 = noise.snoise(new Vector2(fVar6, (float)(time * 0.02 + fVar5)));
						var fVar4 = windDir.y;
						fVar5 = noise.snoise(new Vector2(fVar6, (float)(time * 0.03 + fVar5)));
						time = windInfluence;
						total_force_z = (float)(total_force_z + (fVar3 + (bone_pos_dist_y + -0.5) * 0.25) * windPower.x * time);
						total_force_x = (float)(total_force_x + (windDir.z + (fVar5 + -0.5) * 0.25) * windPower.z * time);
						total_force_y = (float)(total_force_y + (fVar4 + (fVar2 + -0.5) * 0.25) * windPower.y * time);

					}

					force_idx++;
					force_len--;
				} while (force_len != 0);
            }

			return new Vector3(total_force_x, total_force_y, total_force_z);
		}

		// ForceVolume
		private static Vector3 ComputeForceOnBone(in SpringForceComponent force, in SpringBoneComponents bone, float boneWindInfluence) {
			//Directional
			if (force.type == SpringBoneForceType.Directional) {
				return math.mul(new float4x4(force.rotation, force.position), new float4(0f, 0f, force.strength, 0f)).xyz;
			}

			//Wind
			//var fullWeight = force.weight * force.strength;
			var fullWeight = force.strength;
			//if ((fullWeight <= 0.0001f) | (force.periodInSecond <= 0.001f))
			if (fullWeight <= 0.0001f)
				return Vector3.zero;

			const float PI2 = math.PI * 2f;

			//var factor = force.timeInSecond / force.periodInSecond * PI2;

			var localToWorldMatrix = new float4x4(force.rotation, force.position);
			var worldToLocalMatrix = math.mul(new float4x4(math.inverse(force.rotation), float3.zero), new float4x4(float3x3.identity, -force.position));
			//var worldToLocalMatrix = Matrix4x4.Inverse(force.localToWorldMatrix);

			// Wind
			var boneLocalPositionInWindWorld = math.mul(worldToLocalMatrix, new float4(bone.position, 1f));
			var positionalMultiplier = PI2 / force.peakDistance;
			var positionalFactor = math.sin(positionalMultiplier * boneLocalPositionInWindWorld.x) + math.cos(positionalMultiplier * boneLocalPositionInWindWorld.z);
			var offsetMultiplier = math.sin(force.timeFactor + positionalFactor);

			var forward = math.mul(localToWorldMatrix, new float4(0f, 0f, 1f, 0f)).xyz;
			float3 offset = offsetMultiplier * force.offsetVector;
			var forceAtPosition = boneWindInfluence * fullWeight * math.normalize(forward + offset);

			return forceAtPosition;
		}
	}
}
