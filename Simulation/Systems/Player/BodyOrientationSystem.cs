namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class BodyOrientationSystem : SystemMainThreadFilter<BodyOrientationSystem.Filter> {
		public struct Filter {
			public EntityRef Entity;
			public Transform3D* Transform;
			public PlayerStatus* PlayerStatus;
			public BodyOrientation* Orientation;
		}

		public override void Update(Frame frame, ref Filter filter) {
			PlayerInputData input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);
			BodyOrientation* orientation = filter.Orientation;
			FP targetYaw;

			if (input.MoveDirection.SqrMagnitude <= FP._0_01 * FP._0_01) {
				if (input.AimDirection.X > FP._0_01)
					targetYaw = orientation->OrientationAngle;
				else if (input.AimDirection.X < -FP._0_01)
					targetYaw = -orientation->OrientationAngle;
				else
					return;
			} else {
				FP movementYaw = FPMath.Atan2(input.MoveDirection.X, input.MoveDirection.Y) * FP.Rad2Deg;
				FP yawA = movementYaw + orientation->OrientationAngle;
				FP yawB = movementYaw - orientation->OrientationAngle;

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
			}

			FP delta = FPMath.AngleBetweenDegrees(orientation->LocalYaw, targetYaw);

			FP rotationSmooth;

			if (orientation->Interacting)
				rotationSmooth = orientation->InteractionRotationSmooth;
			else
				rotationSmooth = input.Sprint.IsDown ? orientation->SprintRotationSmooth : orientation->NormalRotationSmooth;

			FP rotationT = FPMath.Clamp01(rotationSmooth * frame.DeltaTime);
			orientation->LocalYaw += delta * rotationT;
		}
	}
}