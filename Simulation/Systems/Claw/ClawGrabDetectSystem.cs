namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawGrabDetectSystem : SystemMainThreadFilter<ClawGrabDetectSystem.Filter> {
		private const ushort GrabTag = 1;

		public struct Filter {
			public EntityRef Entity;
			public PlayerStatus* PlayerStatus;
			public EntityGroup* EntityGroup;
		}

		public override void Update(Frame frame, ref Filter filter) {
			PlayerInputData input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);

			if (!TryGetBodyAnchor(frame, filter.Entity, out var anchorTransform))
				return;

			foreach (var (clawEntity, index) in frame.GetEntityGroupIterator(filter.Entity)) {
				if (!frame.Unsafe.TryGetPointer<Claw>(clawEntity, out var claw) || !frame.Unsafe.TryGetPointer<ClawGrip>(clawEntity, out var grip) || !frame.Unsafe.TryGetPointer<Transform3D>(clawEntity, out var clawTransform))
					continue;

				if (grip->Target != EntityRef.None || claw->KickTime > FP._0)
					continue;

				FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
				reach = FPMath.Clamp01(reach);

				if (reach <= FP._0_01)
					continue;

				TryAcquireGrab(frame, clawEntity, claw, grip, clawTransform, anchorTransform);
			}
		}

		private void TryAcquireGrab(Frame frame, EntityRef clawEntity, Claw* claw, ClawGrip* grip, Transform3D* clawTransform, Transform3D* anchorTransform) {
			Shape3D searchShape = Shape3D.CreateSphere(claw->GrabDetectRange);
			var hits = frame.Physics3D.OverlapShape(clawTransform->Position, FPQuaternion.Identity, searchShape, -1, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);

			EntityRef bestTarget = EntityRef.None;
			FPVector3 bestPoint = FPVector3.Zero;
			FP bestDistance = FP.MaxValue;

			for (int i = 0; i < hits.Count; i++) {
				EntityRef target = hits[i].Entity;

				if (target == EntityRef.None || target == clawEntity)
					continue;

				if (!frame.Has<Interactable>(target))
					continue;

				Shape3D* hitShape = hits[i].GetShape(frame);

				if (hitShape == null || (hitShape->UserTag & GrabTag) == 0)
					continue;

				if (!frame.Unsafe.TryGetPointer<Transform3D>(target, out var targetTransform) || !frame.Has<PhysicsBody3D>(target))
					continue;

				FPVector3 point = hits[i].Point;

				if (!IsWithinReach(claw, anchorTransform, point, claw->GrabReleaseRange))
					continue;

				FP distance = (point - clawTransform->Position).SqrMagnitude;

				if (distance >= bestDistance)
					continue;

				bestDistance = distance;
				bestTarget = target;
				bestPoint = point;
			}

			if (bestTarget == EntityRef.None)
				return;

			if (!frame.Unsafe.TryGetPointer<Transform3D>(bestTarget, out var transform))
				return;

			grip->Target = bestTarget;
			grip->LocalPoint = transform->Rotation.Inverted * (bestPoint - transform->Position);

			claw->CollisionTarget = bestTarget;
			claw->CollisionLocalPoint = grip->LocalPoint;
			claw->LastInteractionPosition = bestPoint;
			claw->CollisionSuppressed = true;
			claw->State = ClawState.Grab;

			Log.Info($"[GRAB ACQUIRED] Claw={clawEntity} Target={bestTarget} Point={bestPoint}");

			frame.Signals.OnTargetGrabStarted(bestTarget, clawEntity, bestPoint);
			frame.Events.ClawGrabStarted(claw->Side, bestPoint);
		}

		private bool TryGetBodyAnchor(Frame frame, EntityRef entity, out Transform3D* anchorTransform) {
			anchorTransform = null;

			foreach (var (childEntity, index) in frame.GetEntityGroupIterator(entity)) {
				if (!frame.Has<BodyAnchor>(childEntity) || !frame.Unsafe.TryGetPointer<Transform3D>(childEntity, out anchorTransform))
					continue;

				return true;
			}

			return false;
		}

		private bool IsWithinReach(Claw* claw, Transform3D* anchorTransform, FPVector3 position, FP extraRange) {
			FPVector3 shoulder = anchorTransform->Position + anchorTransform->Rotation * claw->ShoulderLocalPosition;
			FP maxReach = claw->MaxReach + extraRange;
			return (position - shoulder).SqrMagnitude <= maxReach * maxReach;
		}
	}
}