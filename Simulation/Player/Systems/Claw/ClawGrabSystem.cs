namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawGrabSystem : SystemMainThreadFilter<ClawGrabSystem.Filter> {
		public struct Filter {
			public EntityRef Entity;
			public PlayerStatus* PlayerStatus;
			public KCC* KCC;
			public EntityGroup* EntityGroup;
		}

		public override void Update(Frame frame, ref Filter filter) {
			PlayerInputData input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);

			if (!TryGetBodyAnchor(frame, filter.Entity, out var anchorTransform))
				return;

			foreach (var (clawEntity, index) in frame.GetEntityGroupIterator(filter.Entity)) {
				if (!frame.Unsafe.TryGetPointer<Claw>(clawEntity, out var claw) || !frame.Unsafe.TryGetPointer<ClawGrip>(clawEntity, out var grip))
					continue;

				if (claw->State != ClawState.Grab || grip->Target == EntityRef.None)
					continue;

				ApplyGrabForce(frame, ref filter, input, claw, grip, anchorTransform);
			}
		}

		private void ApplyGrabForce(Frame frame, ref Filter filter, PlayerInputData input, Claw* claw, ClawGrip* grip, Transform3D* anchorTransform) {
			if (!frame.Unsafe.TryGetPointer<Transform3D>(grip->Target, out var targetTransform) || !frame.Unsafe.TryGetPointer<PhysicsBody3D>(grip->Target, out var targetBody))
				return;

			FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
			reach = FPMath.Clamp01(reach);

			FPVector3 grabPoint = targetTransform->Position + targetTransform->Rotation * grip->LocalPoint;
			FPVector3 reachLocalPosition = claw->IdleLocalPosition + (claw->ReachLocalPosition - claw->IdleLocalPosition) * reach;
			FPVector3 handlePosition = anchorTransform->Position + anchorTransform->Rotation * reachLocalPosition;

			FPVector3 handleVelocity = filter.KCC->Data.KinematicVelocity + filter.KCC->Data.DynamicVelocity;
			FPVector3 pointVelocity = targetBody->GetPointVelocity(grabPoint, targetTransform);

			FPVector3 positionError = handlePosition - grabPoint;
			FPVector3 velocityError = handleVelocity - pointVelocity;

			FPVector3 force = positionError * claw->GrabStrength + velocityError * claw->GrabDamping;
			force = FPVector3.ClampMagnitude(force, claw->MaxGrabForce);

			targetBody->AddForceAtPosition(force, grabPoint, targetTransform);

			FPVector3 reaction = -force * claw->CrabReactionScale * frame.DeltaTime;
			reaction.Y = FP._0;

			filter.KCC->Data.DynamicVelocity += reaction;
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