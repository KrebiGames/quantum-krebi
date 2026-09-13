namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawGrabSystem : SystemMainThreadFilter<ClawGrabSystem.Filter>, ISignalOnCollision3D {
		public struct Filter {
			public EntityRef Entity;
			public PlayerStatus* PlayerStatus;
			public Player* Player;
			public KCC* KCC;
			public EntityGroup* EntityGroup;
		}

		public override void Update(Frame frame, ref Filter filter) {
			PlayerInputData input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);

			if (!TryGetBodyAnchor(frame, filter.Entity, out var anchorTransform))
				return;

			foreach (var (clawEntity, index) in frame.GetEntityGroupIterator(filter.Entity)) {
				if (!frame.Unsafe.TryGetPointer<Claw>(clawEntity, out var claw) || !frame.Unsafe.TryGetPointer<ClawGrab>(clawEntity, out var grab) || !frame.Unsafe.TryGetPointer<Transform3D>(clawEntity, out var clawTransform))
					continue;

				if (grab->Target != EntityRef.None)
					UpdateGrab(frame, ref filter, input, clawEntity, claw, grab, clawTransform, anchorTransform);

				UpdateCollisionSuppression(frame, grab, clawTransform);
			}
		}

		private void UpdateGrab(Frame frame, ref Filter filter, PlayerInputData input, EntityRef clawEntity, Claw* claw, ClawGrab* grab, Transform3D* clawTransform, Transform3D* anchorTransform) {
			FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
			reach = FPMath.Clamp01(reach);

			if (reach <= FP._0_01) {
				ReleaseGrab(frame, clawEntity, grab);
				return;
			}

			if (!TryGetGrabPoint(frame, grab, out var grabPoint) || !frame.Has<PhysicsBody3D>(grab->Target) || !IsGrabValid(claw, grab, clawTransform, anchorTransform, grabPoint)) {
				ReleaseGrab(frame, clawEntity, grab);
				return;
			}

			grab->TargetPosition = grabPoint;
			grab->LastInteractionPosition = grabPoint;
			ApplyGrabForce(frame, ref filter, claw, grab, anchorTransform, grabPoint, reach);
		}

		private void ApplyGrabForce(Frame frame, ref Filter filter, Claw* claw, ClawGrab* grab, Transform3D* anchorTransform, FPVector3 grabPoint, FP reach) {
			if (!frame.Unsafe.TryGetPointer<Transform3D>(grab->Target, out var targetTransform) || !frame.Unsafe.TryGetPointer<PhysicsBody3D>(grab->Target, out var targetBody))
				return;

			if (targetBody->IsKinematic || targetBody->Mass <= FP._0 || grab->GrabResponsiveness <= FP._0 || grab->MaxGrabAcceleration <= FP._0) {
				grab->CurrentGrabForce = FPVector3.Zero;
				return;
			}

			FPVector3 reachLocalPosition = claw->IdleLocalPosition + (claw->ReachLocalPosition - claw->IdleLocalPosition) * reach;
			FPVector3 handlePosition = anchorTransform->Position + anchorTransform->Rotation * reachLocalPosition;
			FPVector3 handleVelocity = (handlePosition - grab->PreviousHandlePosition) / frame.DeltaTime;
			grab->PreviousHandlePosition = handlePosition;

			FPVector3 positionError = handlePosition - grabPoint;
			FPVector3 velocityError = handleVelocity - targetBody->GetPointVelocity(grabPoint, targetTransform);

			FP response = grab->GrabResponsiveness;
			FP spring = response * response;
			FP damping = response + response;
			FP denominator = FP._1 + damping * frame.DeltaTime + spring * frame.DeltaTime * frame.DeltaTime;
			FP positionGain = spring / denominator;
			FP velocityGain = (damping + spring * frame.DeltaTime) / denominator;

			FPVector3 acceleration = positionError * positionGain + velocityError * velocityGain;
			acceleration = FPVector3.ClampMagnitude(acceleration, grab->MaxGrabAcceleration);

			grab->CurrentGrabForce = acceleration * targetBody->Mass;
			targetBody->AddForceAtPosition(grab->CurrentGrabForce, grabPoint, targetTransform);

			if (grab->CrabReactionScale <= FP._0 || filter.Player->Mass <= FP._0)
				return;

			FPVector3 reactionImpulse = -grab->CurrentGrabForce * grab->CrabReactionScale * frame.DeltaTime;
			reactionImpulse.Y = FP._0;
			filter.KCC->AddExternalImpulse(reactionImpulse / filter.Player->Mass);
		}

		private void ReleaseGrab(Frame frame, EntityRef clawEntity, ClawGrab* grab) {
			EntityRef target = grab->Target;
			FPVector3 position = grab->LastInteractionPosition;

			if (TryGetGrabPoint(frame, grab, out var currentPosition))
				position = currentPosition;

			frame.Signals.OnTargetGrabReleased(target, clawEntity, position);

			grab->CollisionTarget = target;
			grab->CollisionLocalPoint = grab->LocalPoint;
			grab->LastInteractionPosition = position;
			grab->CollisionSuppressed = true;

			grab->Target = EntityRef.None;
			grab->LocalPoint = FPVector3.Zero;
			grab->TargetPosition = FPVector3.Zero;
			grab->PreviousHandlePosition = FPVector3.Zero;
			grab->CurrentGrabForce = FPVector3.Zero;
		}

		private bool IsGrabValid(Claw* claw, ClawGrab* grab, Transform3D* clawTransform, Transform3D* anchorTransform, FPVector3 grabPoint) {
			FPVector3 localClawPosition = anchorTransform->Rotation.Inverted * (clawTransform->Position - anchorTransform->Position);
			FP angle = FPMath.Abs(FPMath.Atan2(localClawPosition.X, localClawPosition.Z) * FP.Rad2Deg);

			if (angle > grab->GrabMaxAngle)
				return false;

			return IsWithinReach(claw, anchorTransform, grabPoint, grab->GrabRange);
		}

		private bool TryGetGrabPoint(Frame frame, ClawGrab* grab, out FPVector3 position) {
			position = FPVector3.Zero;

			if (grab->Target == EntityRef.None || !frame.Unsafe.TryGetPointer<Transform3D>(grab->Target, out var targetTransform))
				return false;

			position = targetTransform->Position + targetTransform->Rotation * grab->LocalPoint;
			return true;
		}

		private void UpdateCollisionSuppression(Frame frame, ClawGrab* grab, Transform3D* clawTransform) {
			if (!grab->CollisionSuppressed || grab->Target != EntityRef.None)
				return;

			FPVector3 interactionPosition = grab->LastInteractionPosition;

			if (grab->CollisionTarget != EntityRef.None && frame.Unsafe.TryGetPointer<Transform3D>(grab->CollisionTarget, out var targetTransform))
				interactionPosition = targetTransform->Position + targetTransform->Rotation * grab->CollisionLocalPoint;

			if ((clawTransform->Position - interactionPosition).SqrMagnitude < grab->CollisionSafeDistance * grab->CollisionSafeDistance)
				return;

			grab->CollisionSuppressed = false;
			grab->CollisionTarget = EntityRef.None;
			grab->CollisionLocalPoint = FPVector3.Zero;
		}

		private bool IsWithinReach(Claw* claw, Transform3D* anchorTransform, FPVector3 position, FP extraRange) {
			FPVector3 shoulder = anchorTransform->Position + anchorTransform->Rotation * claw->ShoulderLocalPosition;
			FP maxReach = claw->MaxReach + extraRange;
			return (position - shoulder).SqrMagnitude <= maxReach * maxReach;
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

		public void OnCollision3D(Frame frame, CollisionInfo3D info) {
			ClawGrab* grab;
			EntityRef other;

			if (frame.Unsafe.TryGetPointer<ClawGrab>(info.Entity, out grab)) {
				other = info.Other;
			} else if (frame.Unsafe.TryGetPointer<ClawGrab>(info.Other, out grab)) {
				other = info.Entity;
			} else {
				return;
			}

			if (grab->Target == other || grab->CollisionSuppressed && grab->CollisionTarget == other)
				info.IgnoreCollision = true;
		}
	}
}