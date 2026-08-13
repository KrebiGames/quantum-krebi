namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class PlayerOrientationSystem : SystemMainThreadFilter<PlayerOrientationSystem.Filter> {
		public struct Filter {
			public EntityRef Entity;
			public PlayerStatus* PlayerStatus;
			public CrabOrientation* Orientation;
		}

		public override void Update(Frame frame, ref Filter filter) {
			QuantumDemoInputTopDown input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);
			CrabOrientation* orientation = filter.Orientation;

			if (input.MoveDirection.SqrMagnitude <= FP._0_01 * FP._0_01)
				return;

			FP movementYaw = FPMath.Atan2(input.MoveDirection.X, input.MoveDirection.Y) * FP.Rad2Deg;

			FP yawA = movementYaw + orientation->OrientationAngle; // left side leads
			FP yawB = movementYaw - orientation->OrientationAngle; // right side leads

			bool movingForward = input.MoveDirection.Y >= FP._0;
			FP targetYaw;

			if (input.AimDirection.X > FP._0_01)
				targetYaw = movingForward ? yawA : yawB;
			else if (input.AimDirection.X < -FP._0_01)
				targetYaw = movingForward ? yawB : yawA;
			else {
				FP deltaA = FPMath.Abs(FPMath.AngleBetweenDegrees(orientation->LocalYaw, yawA));
				FP deltaB = FPMath.Abs(FPMath.AngleBetweenDegrees(orientation->LocalYaw, yawB));
				targetYaw = deltaA < deltaB ? yawA : yawB;
			}

			FP delta = FPMath.AngleBetweenDegrees(orientation->LocalYaw, targetYaw);
			FP maxDelta = orientation->RotationSpeed * frame.DeltaTime;

			orientation->LocalYaw += FPMath.Clamp(delta, -maxDelta, maxDelta);
		}
	}
}