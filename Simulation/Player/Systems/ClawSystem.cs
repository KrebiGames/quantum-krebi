namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawSystem : SystemMainThreadFilter<ClawSystem.Filter> {
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
					if (!frame.Unsafe.TryGetPointer<Claw>(clawEntity, out var claw) || !frame.Unsafe.TryGetPointer<Transform3D>(clawEntity, out var clawTransform) || !frame.Unsafe.TryGetPointer<PhysicsBody3D>(clawEntity, out var body))
						continue;

					UpdateClaw(frame, input, clawEntity, claw, clawTransform, body, anchorTransform);
				}
			}
		}

		private void UpdateClaw(Frame frame, PlayerInputData input, EntityRef clawEntity, Claw* claw, Transform3D* clawTransform, PhysicsBody3D* body, Transform3D* anchorTransform) {
			FP reach = claw->Side == ClawSide.Left ? input.LeftClawReach : input.RightClawReach;
			bool kickPressed = claw->Side == ClawSide.Left ? input.LeftClawKick.WasPressed : input.RightClawKick.WasPressed;
			bool cutDown = claw->Side == ClawSide.Left ? input.LeftClawCut.IsDown : input.RightClawCut.IsDown;

			reach = FPMath.Clamp01(reach);

			if (!claw->Initialized) {
				claw->CurrentLocalPosition = anchorTransform->Rotation.Inverted * (clawTransform->Position - anchorTransform->Position);
				claw->Initialized = true;
			}

			bool fullReach = reach >= claw->GrabThreshold;
			bool hasTarget = frame.Unsafe.TryGetPointer<ClawTarget>(clawEntity, out var target) && target->Entity != EntityRef.None;

			if (kickPressed) {
				claw->State = ClawState.Kick;
				claw->KickTime = claw->KickDuration;
			} else if (claw->KickTime > FP._0) {
				claw->State = ClawState.Kick;
				claw->KickTime -= frame.DeltaTime;
			} else if (cutDown && fullReach) {
				claw->State = ClawState.Cut;
			} else if (fullReach && hasTarget) {
				claw->GrabTime += frame.DeltaTime;
				claw->State = claw->GrabTime >= claw->GrabDelay ? ClawState.Grab : ClawState.Reach;
			} else if (reach > FP._0_01) {
				claw->State = ClawState.Reach;
				claw->GrabTime = FP._0;
			} else {
				claw->State = ClawState.Idle;
				claw->GrabTime = FP._0;
			}

			FPVector3 targetLocalPosition = GetTargetLocalPosition(frame, clawEntity, claw, anchorTransform, reach);
			FP smooth = claw->State == ClawState.Kick || claw->State == ClawState.Cut ? claw->AttackSmooth : claw->PositionSmooth;
			FP positionT = FPMath.Clamp01(smooth * frame.DeltaTime);

			claw->CurrentLocalPosition += (targetLocalPosition - claw->CurrentLocalPosition) * positionT;

			FPVector3 previousPosition = clawTransform->Position;
			FPVector3 newPosition = anchorTransform->Position + anchorTransform->Rotation * claw->CurrentLocalPosition;

			clawTransform->Position = newPosition;
			body->Velocity = (newPosition - previousPosition) / frame.DeltaTime;
		}

		private FPVector3 GetTargetLocalPosition(Frame frame, EntityRef clawEntity, Claw* claw, Transform3D* anchorTransform, FP reach) {
			if (claw->State == ClawState.Kick || claw->State == ClawState.Cut)
				return claw->ReachLocalPosition;

			if (claw->State == ClawState.Grab && frame.Unsafe.TryGetPointer<ClawTarget>(clawEntity, out var target) && target->Entity != EntityRef.None && frame.Unsafe.TryGetPointer<Transform3D>(target->Entity, out var targetTransform)) {
				FPVector3 localPosition = anchorTransform->Rotation.Inverted * (targetTransform->Position - anchorTransform->Position);
				return ClampReach(claw, localPosition);
			}

			if (reach > FP._0_01)
				return claw->IdleLocalPosition + (claw->ReachLocalPosition - claw->IdleLocalPosition) * reach;

			return claw->IdleLocalPosition;
		}

		private FPVector3 ClampReach(Claw* claw, FPVector3 position) {
			FPVector3 offset = position - claw->ShoulderLocalPosition;

			if (offset.SqrMagnitude > claw->MaxReach * claw->MaxReach)
				return claw->ShoulderLocalPosition + offset.Normalized * claw->MaxReach;

			return position;
		}
	}
}