using Quantum;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using static FIMSpace.FProceduralAnimation.LegsAnimator;

namespace FIMSpace.FProceduralAnimation {

	[CreateAssetMenu(fileName = "LAM_VerticalUmbrellaPose", menuName = "FImpossible Creations/Legs Animator/Module - Vertical Umbrella Pose", order = 10)]
	public unsafe class LAM_VerticalUmbrellaPose : LegsAnimatorControlModuleBase {

		[Header("Motion")]
		[SerializeField] float deadZone = 0.1f;
		[SerializeField] float smoothing = 15f;

		[Header("Going Up")]
		[SerializeField] float upSpread = 0.35f;
		[SerializeField] float upVerticalOffset = 0.15f;
		[SerializeField] float upWindStrength = 0.04f;
		[SerializeField] float upLateralTrail = 0.3f;

		[Header("Going Down")]
		[SerializeField] float downContract = 0.2f;
		[SerializeField] float downWindStrength = 0.05f;

		[Header("Wind")]
		[SerializeField] float windFrequency = 3f;
		[SerializeField] float windPhase = 0.8f;

		[Header("Landing")]
		[SerializeField] UnityEngine.LayerMask groundMask = ~0;
		[SerializeField] float landingResetDistance = 1.5f;
		[SerializeField] float standingGroundDistance = 0.3f;
		[SerializeField] float groundRayStartOffset = 0.5f;

		[NonSerialized] LegsAnimator.Leg[] legs;
		float maxSpeed = 5f;
		float maxJumpSpeed = 5f;
		float smoothedVerticalVelocity;
		Vector3 smoothedHorizontalVelocity;

		static readonly Dictionary<LegsAnimator, Vector3> velocities = new Dictionary<LegsAnimator, Vector3>();

		public static void SetVelocity(LegsAnimator legsAnimator, Vector3 velocity) {
			if (legsAnimator == null)
				return;
			velocities[legsAnimator] = velocity;
		}

		public override void OnInit(LegsAnimatorCustomModuleHelper helper) {
			var playerView = LA.GetComponentInParent<PlayerViewController>();
			if (playerView == null || playerView.EntityView == null)
				return;

			Frame frame = QuantumRunner.Default.Game.Frames.Predicted;
			if (!frame.TryGet<KCC>(playerView.EntityView.EntityRef, out var kcc))
				return;

			KCCSettings settings = frame.FindAsset(kcc.Settings);
			foreach (var processorRef in settings.Processors) {
				KCCProcessor processor = frame.FindAsset(processorRef);

				if (processor is EnvironmentProcessor env) {
					maxJumpSpeed = env.JumpMultiplier.AsFloat;
					maxSpeed = env.KinematicSpeed.AsFloat;
					break;
				}
			}

			smoothedVerticalVelocity = 0f;
			smoothedHorizontalVelocity = Vector3.zero;
			List<LegsAnimator.Leg> selectedLegs = new List<LegsAnimator.Leg>();

			if (helper.customStringList == null || helper.customStringList.Count == 0) {
				for (int i = 0; i < LA.Legs.Count; i++)
					selectedLegs.Add(LA.Legs[i]);
			} else {
				int count = Mathf.Min(helper.customStringList.Count, LA.Legs.Count);
				for (int i = 0; i < count; i++) {
					if (helper.customStringList[i] == "1")
						selectedLegs.Add(LA.Legs[i]);
				}
			}

			if (selectedLegs.Count == 0) {
				helper.Enabled = false;
				Debug.Log("[Legs Animator] Vertical Umbrella Pose: No legs selected!");
				return;
			}

			legs = selectedLegs.ToArray();
		}

		private float GetDistanceToGround(LAM_VerticalUmbrellaPose settings) {
			Vector3 origin = LA.transform.position + Vector3.up * settings.groundRayStartOffset;
			float rayLength = settings.standingGroundDistance + settings.landingResetDistance + settings.groundRayStartOffset + 1f;

			if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, settings.groundMask, QueryTriggerInteraction.Ignore)) {
				float distance = hit.distance - settings.groundRayStartOffset - settings.standingGroundDistance;
				return Mathf.Max(0f, distance);
			}

			return float.PositiveInfinity;
		}

		public override void OnLateUpdatePreApply(LegsAnimatorCustomModuleHelper helper) {
			if (legs == null)
				return;

			var settings = helper.ModuleReference as LAM_VerticalUmbrellaPose;
			if (settings == null)
				settings = this;

			Vector3 velocity = Vector3.zero;
			velocities.TryGetValue(LA, out velocity);

			float smoothT = settings.smoothing <= 0f ? 1f : 1f - Mathf.Exp(-settings.smoothing * LA.DeltaTime);
			smoothedVerticalVelocity = Mathf.Lerp(smoothedVerticalVelocity, velocity.y, smoothT);
			smoothedHorizontalVelocity = Vector3.Lerp(smoothedHorizontalVelocity, new Vector3(velocity.x, 0f, velocity.z), smoothT);

			if (Mathf.Abs(smoothedVerticalVelocity) < settings.deadZone)
				smoothedVerticalVelocity = 0f;

			float vertical01 = Mathf.Clamp(smoothedVerticalVelocity / Mathf.Max(0.01f, settings.maxJumpSpeed), -1f, 1f);
			float horizontal01 = Mathf.Clamp01(smoothedHorizontalVelocity.magnitude / Mathf.Max(0.01f, settings.maxSpeed));
			float spreadMultiplier = 1f;
			float yOffset = 0f;
			float upAmount = 0f;
			float downAmount = 0f;

			if (vertical01 > 0f) {
				upAmount = vertical01;
				spreadMultiplier = 1f + upAmount * settings.upSpread;
				yOffset = -upAmount * settings.upVerticalOffset;
			} else if (vertical01 < 0f) {
				downAmount = -vertical01;

				if (settings.landingResetDistance > 0f) {
					float groundDistance = GetDistanceToGround(settings);

					if (!float.IsPositiveInfinity(groundDistance)) {
						float landingFade = Mathf.Clamp01(groundDistance / settings.landingResetDistance);
						downAmount *= Mathf.SmoothStep(0f, 1f, landingFade);
					}
				}

				spreadMultiplier = 1f - downAmount * settings.downContract;
			}

			Vector3 localMoveDir = Vector3.zero;

			if (upAmount > 0f && smoothedHorizontalVelocity.sqrMagnitude > 0.0001f) {
				Vector3 rootWorld = LA.RootToWorldSpace(Vector3.zero);
				Vector3 localVelocity = LA.ToRootLocalSpace(rootWorld + smoothedHorizontalVelocity) - LA.ToRootLocalSpace(rootWorld);
				localVelocity.y = 0f;
				if (localVelocity.sqrMagnitude > 0.0001f)
					localMoveDir = localVelocity.normalized;
			}

			float blend = LA._MainBlend * ModuleBlend;
			if (blend < 0.001f)
				return;

			for (int i = 0; i < legs.Length; i++) {
				var leg = legs[i];
				Vector3 original = leg.GetFinalIKPos();
				Vector3 local = LA.ToRootLocalSpace(original);
				float legSpread = spreadMultiplier;
				float wind = Mathf.Sin(Time.time * settings.windFrequency * Mathf.PI * 2f + i * settings.windPhase);

				if (upAmount > 0f)
					legSpread += wind * settings.upWindStrength * upAmount;
				else if (downAmount > 0f)
					legSpread += wind * settings.downWindStrength * downAmount;

				local.x *= legSpread;
				local.z *= legSpread;
				local.y += yOffset;

				if (upAmount > 0f) {
					float lateralAmount = settings.upLateralTrail * horizontal01 * upAmount;
					local.x -= localMoveDir.x * lateralAmount;
					local.z -= localMoveDir.z * lateralAmount;
				}

				Vector3 target = LA.RootToWorldSpace(local);
				leg.OverrideFinalIKPos(Vector3.LerpUnclamped(original, target, blend));
			}
		}

#if UNITY_EDITOR

		public override void Editor_InspectorGUI(LegsAnimator legsAnimator, LegsAnimatorCustomModuleHelper helper) {
			EditorGUILayout.HelpBox("Effect settings are configured on the Vertical Umbrella Pose module asset.", MessageType.Info);
			GUILayout.Space(6);
			EditorGUILayout.LabelField("Legs affected by module:", EditorStyles.boldLabel);

			if (helper.customStringList == null)
				helper.customStringList = new List<string>();

			var list = helper.customStringList;
			int targetCount = legsAnimator.Legs.Count;

			while (list.Count < targetCount)
				list.Add("1");
			while (list.Count > targetCount)
				list.RemoveAt(list.Count - 1);

			GUI.enabled = !legsAnimator.LegsInitialized;

			for (int i = 0; i < list.Count; i++) {
				var boneStart = legsAnimator.Legs[i].BoneStart;

				if (boneStart == null) {
					EditorGUILayout.LabelField("[" + (i + 1) + "] LEG LACKING BONE REFERENCES");
					continue;
				}

				EditorGUILayout.BeginHorizontal();

				bool enabled = list[i].Length > 0 && list[i][0] == '1';
				enabled = EditorGUILayout.Toggle("[" + (i + 1) + "] " + boneStart.name, enabled);
				list[i] = enabled ? "1" : "0";

				EditorGUILayout.ObjectField(boneStart, typeof(Transform), true, GUILayout.Width(60));
				EditorGUILayout.EndHorizontal();
			}

			GUI.enabled = true;
		}

		[CanEditMultipleObjects]
		[CustomEditor(typeof(LAM_VerticalUmbrellaPose))]
		public class LAM_VerticalUmbrellaPoseEditor : LegsAnimatorControlModuleBaseEditor { }

#endif
	}
}