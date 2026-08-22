namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawGrabSystem : SystemMainThreadFilter<ClawGrabSystem.Filter>, ISignalOnCollision3D {
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
				if (!frame.Unsafe.TryGetPointer<Claw>(clawEntity, out var claw) || !frame.Unsafe.TryGetPointer<ClawGrab>(clawEntity, out var grab))
					continue;

				if (claw->State != ClawState.Grab || grab->Target == EntityRef.None)
					continue;

				ApplyGrabForce(frame, ref filter, input, claw, grab, anchorTransform);
			}
		}

		private void ApplyGrabForce(Frame frame, ref Filter filter, PlayerInputData input, Claw* claw, ClawGrab* grab, Transform3D* anchorTransform) {
			if (!frame.Unsafe.TryGetPointer<Transform3D>(grab->Target, out var targetTransform) || !frame.Unsafe.TryGetPointer<PhysicsBody3D>(grab->Target, out var targetBody))
				return;

			FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
			reach = FPMath.Clamp01(reach);

			FPVector3 grabPoint = targetTransform->Position + targetTransform->Rotation * grab->LocalPoint;
			FPVector3 reachLocalPosition = claw->IdleLocalPosition + (claw->ReachLocalPosition - claw->IdleLocalPosition) * reach;
			FPVector3 handlePosition = anchorTransform->Position + anchorTransform->Rotation * reachLocalPosition;

			FPVector3 handleVelocity = (handlePosition - grab->PreviousHandlePosition) / frame.DeltaTime;
			grab->PreviousHandlePosition = handlePosition;

			FPVector3 pointVelocity = targetBody->GetPointVelocity(grabPoint, targetTransform);

			FPVector3 positionError = handlePosition - grabPoint;
			FPVector3 velocityError = handleVelocity - pointVelocity;

			FPVector3 targetForce = positionError * grab->GrabStrength + velocityError * grab->GrabDamping;
			targetForce = FPVector3.ClampMagnitude(targetForce, grab->MaxGrabForce);

			FP forceT = FPMath.Clamp01(grab->GrabForceSmooth * frame.DeltaTime);
			grab->CurrentGrabForce += (targetForce - grab->CurrentGrabForce) * forceT;

			targetBody->AddForceAtPosition(grab->CurrentGrabForce, grabPoint, targetTransform);

			FPVector3 reaction = -grab->CurrentGrabForce * grab->CrabReactionScale * frame.DeltaTime;
			reaction.Y = FP._0;

			filter.KCC->Data.DynamicVelocity += reaction;
		}

		public void OnCollision3D(Frame frame, CollisionInfo3D info) {
			if (!frame.Unsafe.TryGetPointer<ClawGrab>(info.Entity, out var grab))
				return;

			if (grab->Target == info.Other || grab->CollisionSuppressed && grab->CollisionTarget == info.Other)
				info.IgnoreCollision = true;
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