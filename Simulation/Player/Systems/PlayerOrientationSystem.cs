namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class PlayerOrientationSystem : SystemMainThreadFilter<PlayerOrientationSystem.Filter> {
		public struct Filter {
			public EntityRef Entity;
			public Transform3D* Transform;
			public PlayerStatus* PlayerStatus;
			public CrabOrientation* Orientation;
		}

		public override void Update(Frame frame, ref Filter filter) {
			PlayerInputData input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);
			CrabOrientation* orientation = filter.Orientation;

			FPVector3 forward = filter.Transform->Forward;
			FP baseYaw = FPMath.Atan2(forward.X, forward.Z) * FP.Rad2Deg;

			if (input.MoveDirection.SqrMagnitude <= FP._0_01 * FP._0_01) {
				orientation->WorldYaw = baseYaw + orientation->LocalYaw;
				return;
			}

			FP movementYaw = FPMath.Atan2(input.MoveDirection.X, input.MoveDirection.Y) * FP.Rad2Deg;

			FP yawA = movementYaw + orientation->OrientationAngle;
			FP yawB = movementYaw - orientation->OrientationAngle;
			FP targetYaw;

			if (FPMath.Abs(input.MoveDirection.X) > FPMath.Abs(input.MoveDirection.Y)) {
				FP forwardA = FPMath.Abs(FPMath.AngleBetweenDegrees(FP._0, yawA));
				FP forwardB = FPMath.Abs(FPMath.AngleBetweenDegrees(FP._0, yawB));
				targetYaw = forwardA < forwardB ? yawA : yawB;
			} else {
				bool movingForward = input.MoveDirection.Y > FP._0;

				if (input.AimDirection.X > FP._0_01)
					targetYaw = movingForward ? yawA : yawB;
				else if (input.AimDirection.X < -FP._0_01)
					targetYaw = movingForward ? yawB : yawA;
				else {
					FP deltaA = FPMath.Abs(FPMath.AngleBetweenDegrees(orientation->LocalYaw, yawA));
					FP deltaB = FPMath.Abs(FPMath.AngleBetweenDegrees(orientation->LocalYaw, yawB));
					targetYaw = deltaA < deltaB ? yawA : yawB;
				}
			}

			FP delta = FPMath.AngleBetweenDegrees(orientation->LocalYaw, targetYaw);
			FP maxDelta = orientation->RotationSpeed * frame.DeltaTime;

			orientation->LocalYaw += FPMath.Clamp(delta, -maxDelta, maxDelta);
			orientation->WorldYaw = baseYaw + orientation->LocalYaw;
		}
	}
}