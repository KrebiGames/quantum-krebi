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
					!frame.Unsafe.TryGetPointer<ClawPunch>(clawEntity, out var punch) ||
					!frame.Unsafe.TryGetPointer<ClawGrab>(clawEntity, out var grab) ||
					!frame.Unsafe.TryGetPointer<Transform3D>(clawEntity, out var clawTransform))
					continue;

				frame.Unsafe.TryGetPointer<ClawTarget>(clawEntity, out var target);
				UpdateClaw(frame, filter.Entity, input, clawEntity, claw, punch, grab, target, clawTransform, anchorTransform);

				if (grab->Target != EntityRef.None)
					interacting = true;
			}

			if (orientation != null)
				orientation->Interacting = interacting;
		}

		private void UpdateClaw(Frame frame, EntityRef entity, PlayerInputData input, EntityRef clawEntity, Claw* claw, ClawPunch* punch, ClawGrab* grab, ClawTarget* target, Transform3D* clawTransform, Transform3D* anchorTransform) {
			FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
			bool punchPressed = claw->Side == ClawSide.Left ? input.LeftClawPunch.WasPressed : input.RightClawPunch.WasPressed;

			reach = FPMath.Clamp01(reach);
			bool reaching = reach > FP._0_01;

			ClawState state;
			FPVector3 targetPosition;

			if (grab->Target != EntityRef.None) {
				state = ClawState.Grab;
				targetPosition = grab->TargetPosition;
			} else if (punch->PunchTime > FP._0) {
				state = ClawState.Punch;
				targetPosition = anchorTransform->Position + anchorTransform->Rotation * punch->TargetLocalPosition;
				punch->PunchTime -= frame.DeltaTime;
			} else if (punchPressed && !reaching) {
				state = ClawState.Punch;
				punch->TargetLocalPosition = GetPunchTargetLocalPosition(claw, target, clawTransform, anchorTransform);
				targetPosition = anchorTransform->Position + anchorTransform->Rotation * punch->TargetLocalPosition;
				punch->PunchTime = punch->PunchDuration;
				punch->PunchApplied = false;
			} else if (reaching) {
				state = ClawState.Reach;
				FPVector3 localPosition = claw->IdleLocalPosition + (claw->ReachLocalPosition - claw->IdleLocalPosition) * reach;
				targetPosition = anchorTransform->Position + anchorTransform->Rotation * localPosition;
			} else {
				state = ClawState.Idle;
				targetPosition = anchorTransform->Position + anchorTransform->Rotation * claw->IdleLocalPosition;
			}

			SetState(frame, entity, claw, state, targetPosition);
			UpdatePosition(frame, clawEntity, claw, punch, grab, clawTransform, anchorTransform, reach);
		}

		private void SetState(Frame frame, EntityRef entity, Claw* claw, ClawState state, FPVector3 targetPosition) {
			if (claw->State == state)
				return;

			claw->State = state;
			frame.Events.ClawStateChanged(entity, claw->Side, state, targetPosition);
		}

		private void UpdatePosition(Frame frame, EntityRef clawEntity, Claw* claw, ClawPunch* punch, ClawGrab* grab, Transform3D* clawTransform, Transform3D* anchorTransform, FP reach) {
			FPVector3 targetLocalPosition;

			if (grab->Target != EntityRef.None) {
				targetLocalPosition = anchorTransform->Rotation.Inverted * (grab->TargetPosition - anchorTransform->Position);
				targetLocalPosition = ClampReach(claw, targetLocalPosition);
			} else if (claw->State == ClawState.Punch) {
				targetLocalPosition = punch->TargetLocalPosition;
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

			FP smooth = claw->State == ClawState.Punch ? punch->PunchSmooth : claw->PositionSmooth;
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

		private FPVector3 GetPunchTargetLocalPosition(Claw* claw, ClawTarget* target, Transform3D* clawTransform, Transform3D* anchorTransform) {
			if (target == null || target->Entity == EntityRef.None)
				return claw->ReachLocalPosition;

			FPVector3 direction = target->Point - clawTransform->Position;

			if (direction.SqrMagnitude == FP._0)
				return claw->ReachLocalPosition;

			FP distance = (claw->ReachLocalPosition - claw->IdleLocalPosition).Magnitude;
			FPVector3 position = clawTransform->Position + direction.Normalized * distance;
			return ClampReach(claw, anchorTransform->Rotation.Inverted * (position - anchorTransform->Position));
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