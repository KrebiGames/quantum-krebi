namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawGrabDetectSystem : SystemMainThreadFilter<ClawGrabDetectSystem.Filter> {
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
				if (!frame.Unsafe.TryGetPointer<Claw>(clawEntity, out var claw) ||
					!frame.Unsafe.TryGetPointer<ClawTarget>(clawEntity, out var target) ||
					!frame.Unsafe.TryGetPointer<ClawPunch>(clawEntity, out var punch) ||
					!frame.Unsafe.TryGetPointer<ClawGrab>(clawEntity, out var grab) ||
					!frame.Unsafe.TryGetPointer<Transform3D>(clawEntity, out var clawTransform))
					continue;

				if (grab->Target != EntityRef.None || punch->PunchTime > FP._0 || target->Entity == EntityRef.None)
					continue;

				FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
				reach = FPMath.Clamp01(reach);

				if (reach <= FP._0_01)
					continue;

				FP distance = (target->Point - clawTransform->Position).SqrMagnitude;

				if (distance > grab->GrabRange * grab->GrabRange)
					continue;

				AcquireGrab(frame, clawEntity, claw, grab, target, anchorTransform, reach);
			}
		}

		private void AcquireGrab(Frame frame, EntityRef clawEntity, Claw* claw, ClawGrab* grab, ClawTarget* target, Transform3D* anchorTransform, FP reach) {
			if (!frame.Unsafe.TryGetPointer<Transform3D>(target->Entity, out var targetTransform) || !frame.Has<PhysicsBody3D>(target->Entity))
				return;

			grab->Target = target->Entity;
			grab->LocalPoint = targetTransform->Rotation.Inverted * (target->Point - targetTransform->Position);
			grab->TargetPosition = target->Point;

			FPVector3 reachLocalPosition = claw->IdleLocalPosition + (claw->ReachLocalPosition - claw->IdleLocalPosition) * reach;
			grab->PreviousHandlePosition = anchorTransform->Position + anchorTransform->Rotation * reachLocalPosition;
			grab->CurrentGrabForce = FPVector3.Zero;

			grab->CollisionTarget = target->Entity;
			grab->CollisionLocalPoint = grab->LocalPoint;
			grab->LastInteractionPosition = target->Point;
			grab->CollisionSuppressed = true;

			frame.Signals.OnTargetGrabStarted(target->Entity, clawEntity, target->Point);
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
	}
}