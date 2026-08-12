namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class CrabOrientationSystem : SystemMainThreadFilter<CrabOrientationSystem.Filter> {
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

			// Continuous joystick movement angle
			FP movementYaw = FPMath.Atan2(input.MoveDirection.X, input.MoveDirection.Y) * FP.Rad2Deg;

			// Both valid sideways orientations
			FP yawA = movementYaw + orientation->OrientationAngle;
			FP yawB = movementYaw - orientation->OrientationAngle;

			// Choose the one keeping the crab facing forward / back towards camera
			FP angleA = FPMath.Abs(FPMath.AngleBetweenDegrees(FP._0, yawA));
			FP angleB = FPMath.Abs(FPMath.AngleBetweenDegrees(FP._0, yawB));

			FP targetYaw = angleA < angleB ? yawA : yawB;

			FP delta = FPMath.AngleBetweenDegrees(orientation->LocalYaw, targetYaw);
			FP maxDelta = orientation->RotationSpeed * frame.DeltaTime;

			orientation->LocalYaw += FPMath.Clamp(delta, -maxDelta, maxDelta);
		}
	}
}