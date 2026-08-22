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

			foreach (var (anchorEntity, anchorIndex) in frame.GetEntityGroupIterator(filter.Entity)) {
				if (!frame.Has<BodyAnchor>(anchorEntity) || !frame.Unsafe.TryGetPointer<Transform3D>(anchorEntity, out var anchorTransform))
					continue;

				foreach (var (clawEntity, clawIndex) in frame.GetEntityGroupIterator(filter.Entity)) {
					if (!frame.Unsafe.TryGetPointer<Claw>(clawEntity, out var claw) || !frame.Unsafe.TryGetPointer<ClawGrip>(clawEntity, out var grip) || !frame.Unsafe.TryGetPointer<Transform3D>(clawEntity, out var clawTransform))
						continue;

					UpdateClaw(frame, input, clawEntity, claw, grip, clawTransform, anchorTransform);
				}
			}
		}

		private void UpdateClaw(Frame frame, PlayerInputData input, EntityRef clawEntity, Claw* claw, ClawGrip* grip, Transform3D* clawTransform, Transform3D* anchorTransform) {
			FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
			bool kickPressed = claw->Side == ClawSide.Left ? input.LeftClawKick.WasPressed : input.RightClawKick.WasPressed;

			reach = FPMath.Clamp01(reach);
			bool reaching = reach > FP._0_01;

			if (grip->Target != EntityRef.None && (!reaching || !IsGrabValid(frame, claw, grip, anchorTransform)))
				ReleaseGrab(frame, clawEntity, claw, grip);

			if (grip->Target != EntityRef.None) {
				claw->State = ClawState.Grab;
				claw->CollisionSuppressed = true;
			} else if (claw->KickTime > FP._0) {
				claw->State = ClawState.Kick;
				claw->KickTime -= frame.DeltaTime;
			} else if (kickPressed && !reaching) {
				claw->State = ClawState.Kick;
				claw->KickTime = claw->KickDuration;
			} else {
				claw->State = reaching ? ClawState.Reach : ClawState.Idle;
			}

			UpdatePosition(frame, clawEntity, claw, grip, clawTransform, anchorTransform, reach);
			UpdateCollisionSuppression(frame, claw, grip, clawTransform);
		}

		private void UpdatePosition(Frame frame, EntityRef clawEntity, Claw* claw, ClawGrip* grip, Transform3D* clawTransform, Transform3D* anchorTransform, FP reach) {
			FPVector3 targetLocalPosition;

			if (grip->Target != EntityRef.None && TryGetGrabPoint(frame, grip, out FPVector3 grabPosition)) {
				targetLocalPosition = anchorTransform->Rotation.Inverted * (grabPosition - anchorTransform->Position);
				targetLocalPosition = ClampReach(claw, targetLocalPosition);
				claw->LastInteractionPosition = grabPosition;
			} else if (claw->State == ClawState.Kick) {
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

			FP smooth = claw->State == ClawState.Kick ? claw->KickSmooth : claw->PositionSmooth;
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

		private void ReleaseGrab(Frame frame, EntityRef clawEntity, Claw* claw, ClawGrip* grip) {
			EntityRef target = grip->Target;
			FPVector3 position = claw->LastInteractionPosition;

			if (TryGetGrabPoint(frame, grip, out FPVector3 currentPosition))
				position = currentPosition;

			Log.Info($"[GRAB RELEASE] Claw={clawEntity} Target={target}");

			frame.Signals.OnTargetGrabReleased(target, clawEntity, position);
			frame.Events.ClawGrabReleased(claw->Side, position);

			claw->CollisionTarget = target;
			claw->CollisionLocalPoint = grip->LocalPoint;
			claw->LastInteractionPosition = position;

			grip->Target = EntityRef.None;
			grip->LocalPoint = FPVector3.Zero;
		}

		private void UpdateCollisionSuppression(Frame frame, Claw* claw, ClawGrip* grip, Transform3D* clawTransform) {
			if (!claw->CollisionSuppressed || grip->Target != EntityRef.None)
				return;

			FPVector3 interactionPosition = claw->LastInteractionPosition;

			if (claw->CollisionTarget != EntityRef.None && frame.Unsafe.TryGetPointer<Transform3D>(claw->CollisionTarget, out var targetTransform))
				interactionPosition = targetTransform->Position + targetTransform->Rotation * claw->CollisionLocalPoint;

			if ((clawTransform->Position - interactionPosition).SqrMagnitude < claw->CollisionSafeDistance * claw->CollisionSafeDistance)
				return;

			claw->CollisionSuppressed = false;
			claw->CollisionTarget = EntityRef.None;
			claw->CollisionLocalPoint = FPVector3.Zero;
		}

		private bool IsGrabValid(Frame frame, Claw* claw, ClawGrip* grip, Transform3D* anchorTransform) {
			if (!TryGetGrabPoint(frame, grip, out FPVector3 point))
				return false;

			return IsWithinReach(claw, anchorTransform, point, claw->GrabReleaseRange);
		}

		private bool TryGetGrabPoint(Frame frame, ClawGrip* grip, out FPVector3 position) {
			position = FPVector3.Zero;

			if (grip->Target == EntityRef.None || !frame.Unsafe.TryGetPointer<Transform3D>(grip->Target, out var transform))
				return false;

			position = transform->Position + transform->Rotation * grip->LocalPoint;
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
	}
}