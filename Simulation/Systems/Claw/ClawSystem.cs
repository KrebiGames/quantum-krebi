namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawSystem : SystemMainThreadFilter<ClawSystem.Filter> {
		private static readonly FP MaxSpeed = FP.FromFloat_UNSAFE(8.0f);
		private static readonly FP TeleportDistance = FP.FromFloat_UNSAFE(1.0f);

		public struct Filter {
			public EntityRef Entity;
			public PlayerStatus* PlayerStatus;
			public EntityGroup* EntityGroup;
		}

		public override void Update(Frame frame, ref Filter filter) {
			PlayerInputData input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);

			if (!TryGetBodyAnchor(frame, filter.Entity, out var anchorEntity, out var anchorTransform))
				return;

			frame.Unsafe.TryGetPointer<BodyOrientation>(anchorEntity, out var orientation);

			bool interacting = false;

			foreach (var (clawEntity, index) in frame.GetEntityGroupIterator(filter.Entity)) {
				if (!frame.Unsafe.TryGetPointer<Claw>(clawEntity, out var claw) || 
					!frame.Unsafe.TryGetPointer<ClawGrab>(clawEntity, out var grab) || 
					!frame.Unsafe.TryGetPointer<Transform3D>(clawEntity, out var clawTransform))
					continue;

				UpdateClaw(frame,
					filter.Entity,
					input,
					clawEntity,
					claw,
					grab,
					clawTransform,
					anchorTransform);

				if (grab->Target != EntityRef.None)
					interacting = true;
			}

			if (orientation != null)
				orientation->Interacting = interacting;
		}

		private void UpdateClaw(Frame frame,
						  EntityRef entity,
						  PlayerInputData input,
						  EntityRef clawEntity,
						  Claw* claw,
						  ClawGrab* grab,
						  Transform3D* clawTransform,
						  Transform3D* anchorTransform) {
			FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
			bool kickPressed = claw->Side == ClawSide.Left ? input.LeftClawPunch.WasPressed : input.RightClawPunch.WasPressed;

			reach = FPMath.Clamp01(reach);
			bool reaching = reach > FP._0_01;

			if (grab->Target != EntityRef.None && (!reaching || !IsGrabValid(frame, claw, grab, clawTransform, anchorTransform)))
				ReleaseGrab(frame, clawEntity, grab);

			ClawState state;
			FPVector3 targetPosition;

			if (grab->Target != EntityRef.None) {
				state = ClawState.Grab;
				grab->CollisionSuppressed = true;

				if (!TryGetGrabPoint(frame, grab, out targetPosition))
					targetPosition = grab->LastInteractionPosition;
			} else if (claw->PunchTime > FP._0) {
				state = ClawState.Punch;
				targetPosition = anchorTransform->Position + anchorTransform->Rotation * claw->ReachLocalPosition;
				claw->PunchTime -= frame.DeltaTime;
			} else if (kickPressed && !reaching) {
				state = ClawState.Punch;
				targetPosition = anchorTransform->Position + anchorTransform->Rotation * claw->ReachLocalPosition;
				claw->PunchTime = claw->PunchDuration;
			} else if (reaching) {
				state = ClawState.Reach;
				FPVector3 localPosition = claw->IdleLocalPosition + (claw->ReachLocalPosition - claw->IdleLocalPosition) * reach;
				targetPosition = anchorTransform->Position + anchorTransform->Rotation * localPosition;
			} else {
				state = ClawState.Idle;
				targetPosition = anchorTransform->Position + anchorTransform->Rotation * claw->IdleLocalPosition;
			}

			SetState(frame, entity, claw, state, targetPosition);

			UpdatePosition(frame, clawEntity, claw, grab, clawTransform, anchorTransform, reach);
			UpdateCollisionSuppression(frame, grab, clawTransform);
		}

		private void SetState(Frame frame, EntityRef entity, Claw* claw, ClawState state, FPVector3 targetPosition) {
			if (claw->State == state)
				return;

			claw->State = state;
			frame.Events.ClawStateChanged(entity, claw->Side, state, targetPosition);
		}

		private void UpdatePosition(Frame frame, EntityRef clawEntity, Claw* claw, ClawGrab* grab, Transform3D* clawTransform, Transform3D* anchorTransform, FP reach) {
			FPVector3 targetLocalPosition;

			if (grab->Target != EntityRef.None && TryGetGrabPoint(frame, grab, out FPVector3 grabPosition)) {
				targetLocalPosition = anchorTransform->Rotation.Inverted * (grabPosition - anchorTransform->Position);
				targetLocalPosition = ClampReach(claw, targetLocalPosition);
				grab->LastInteractionPosition = grabPosition;
			} else if (claw->State == ClawState.Punch) {
				targetLocalPosition = claw->ReachLocalPosition;
			} else if (claw->State == ClawState.Reach) {
				targetLocalPosition = claw->IdleLocalPosition + (claw->ReachLocalPosition - claw->IdleLocalPosition) * reach;
			} else {
				targetLocalPosition = claw->IdleLocalPosition;
			}

			if (!frame.Unsafe.TryGetPointer<PhysicsBody3D>(clawEntity, out var body))
				return;

			if (claw->PreviousDesiredPosition.SqrMagnitude == FP._0 && claw->CurrentLocalPosition.SqrMagnitude == FP._0) {
				claw->CurrentLocalPosition = targetLocalPosition;
				FPVector3 startPosition = anchorTransform->Position + anchorTransform->Rotation * targetLocalPosition;
				clawTransform->Position = startPosition;
				body->Velocity = FPVector3.Zero;
				claw->PreviousDesiredPosition = startPosition;
				return;
			}

			FP smooth = claw->State == ClawState.Punch ? claw->PunchSmooth : claw->PositionSmooth;
			FP t = FPMath.Clamp01(smooth * frame.DeltaTime);
			claw->CurrentLocalPosition += (targetLocalPosition - claw->CurrentLocalPosition) * t;

			FPVector3 desiredPosition = anchorTransform->Position + anchorTransform->Rotation * claw->CurrentLocalPosition;
			FPVector3 error = desiredPosition - clawTransform->Position;

			if (error.SqrMagnitude > TeleportDistance * TeleportDistance) {
				clawTransform->Position = desiredPosition;
				body->Velocity = FPVector3.Zero;
				claw->PreviousDesiredPosition = desiredPosition;
				return;
			}

			FPVector3 targetVelocity = (desiredPosition - claw->PreviousDesiredPosition) / frame.DeltaTime;
			FPVector3 correctionVelocity = error * claw->PositionSmooth;
			FPVector3 desiredVelocity = targetVelocity + correctionVelocity;

			body->Velocity = FPVector3.ClampMagnitude(desiredVelocity, MaxSpeed);
			claw->PreviousDesiredPosition = desiredPosition;
		}

		private void ReleaseGrab(Frame frame, EntityRef clawEntity, ClawGrab* grab) {
			EntityRef target = grab->Target;
			FPVector3 position = grab->LastInteractionPosition;

			if (TryGetGrabPoint(frame, grab, out FPVector3 currentPosition))
				position = currentPosition;

			frame.Signals.OnTargetGrabReleased(target, clawEntity, position);

			grab->CollisionTarget = target;
			grab->CollisionLocalPoint = grab->LocalPoint;
			grab->LastInteractionPosition = position;

			grab->Target = EntityRef.None;
			grab->LocalPoint = FPVector3.Zero;
			grab->PreviousHandlePosition = FPVector3.Zero;
			grab->CurrentGrabForce = FPVector3.Zero;
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

		private bool IsGrabValid(Frame frame, Claw* claw, ClawGrab* grab, Transform3D* clawTransform, Transform3D* anchorTransform) {
			FPVector3 localClawPosition = anchorTransform->Rotation.Inverted * (clawTransform->Position - anchorTransform->Position);
			FP angle = FPMath.Abs(FPMath.Atan2(localClawPosition.X, localClawPosition.Z) * FP.Rad2Deg);

			if (angle > grab->GrabMaxAngle)
				return false;

			if (!TryGetGrabPoint(frame, grab, out FPVector3 point))
				return false;

			return IsWithinReach(claw, anchorTransform, point, grab->GrabReleaseRange);
		}

		private bool TryGetGrabPoint(Frame frame, ClawGrab* grab, out FPVector3 position) {
			position = FPVector3.Zero;

			if (grab->Target == EntityRef.None || !frame.Unsafe.TryGetPointer<Transform3D>(grab->Target, out var transform))
				return false;

			position = transform->Position + transform->Rotation * grab->LocalPoint;
			return true;
		}

		private bool IsWithinReach(Claw* claw, Transform3D* anchorTransform, FPVector3 position, FP extraRange) {
			FPVector3 shoulder = anchorTransform->Position + anchorTransform->Rotation * claw->ShoulderLocalPosition;
			FP maxReach = claw->MaxReach + extraRange;
			return (position - shoulder).SqrMagnitude <= maxReach * maxReach;
		}

		private FPVector3 ClampReach(Claw* claw, FPVector3 position) {
			FPVector3 offset = position - claw->ShoulderLocalPosition;

			if (offset.SqrMagnitude > claw->MaxReach * claw->MaxReach)
				return claw->ShoulderLocalPosition + offset.Normalized * claw->MaxReach;

			return position;
		}

		private bool TryGetBodyAnchor(Frame frame, EntityRef entity, out EntityRef anchorEntity, out Transform3D* anchorTransform) {
			anchorEntity = EntityRef.None;
			anchorTransform = null;

			foreach (var (childEntity, index) in frame.GetEntityGroupIterator(entity)) {
				if (!frame.Has<BodyAnchor>(childEntity) || !frame.Unsafe.TryGetPointer<Transform3D>(childEntity, out anchorTransform))
					continue;

				anchorEntity = childEntity;
				return true;
			}

			return false;
		}
	}
}