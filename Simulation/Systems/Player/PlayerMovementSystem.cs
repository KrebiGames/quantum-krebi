namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class PlayerMovementSystem : SystemMainThreadFilter<PlayerMovementSystem.Filter> {
		private static readonly FP SurfaceTolerance = FP.FromFloat_UNSAFE(0.05f);
		private static readonly FP VelocityTolerance = FP.FromFloat_UNSAFE(0.02f);

		public struct Filter {
			public EntityRef Entity;
			public Player* Player;
			public PlayerStatus* PlayerStatus;
			public Transform3D* Transform;
			public KCC* KCC;
		}

		public override void Update(Frame frame, ref Filter filter) {
			PlayerInputData input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);
			Player* player = filter.Player;
			PlayerStatus* status = filter.PlayerStatus;
			KCC* kcc = filter.KCC;

			PlayerMovementData movementData = frame.FindAsset<PlayerMovementData>(status->PlayerMovementData.Id);
			FP rotationSpeed = movementData.DefaultRotationSpeed * frame.DeltaTime;

			kcc->AddLookRotation(-input.AimDirection.Y * rotationSpeed, input.AimDirection.X * rotationSpeed);

			bool inWater = status->WaterState != PlayerWaterState.None;
			bool isMoving = input.MoveDirection.X != FP._0 || input.MoveDirection.Y != FP._0;
			FP sprintMultiplier = input.Sprint.IsDown ? player->SprintMultiplier : FP._1;

			if (inWater) {
				FPVector3 moveDirection = kcc->Data.TransformRotation * input.MoveDirection.XOY;
				moveDirection.Y = FP._0;

				FPVector3 lookForward = kcc->Data.LookRotation * FPVector3.Forward;
				FP verticalSpeed = isMoving ? lookForward.Y * player->SwimSpeed * sprintMultiplier : FP._0;
				bool atSurface = kcc->Data.TargetPosition.Y >= status->WaterSurfaceY - SurfaceTolerance;

				if (status->WaterEntryActive && verticalSpeed != FP._0)
					status->WaterEntryActive = false;

				if (status->WaterEntryActive)
					status->WaterState = PlayerWaterState.Submerged;
				else if (atSurface && verticalSpeed >= FP._0)
					status->WaterState = PlayerWaterState.Surface;
				else if (kcc->IsGrounded && verticalSpeed <= FP._0)
					status->WaterState = PlayerWaterState.Seabed;
				else
					status->WaterState = PlayerWaterState.Submerged;

				FP moveSpeed = status->WaterState == PlayerWaterState.Seabed ? player->SeaBedWalkSpeed : player->SwimSpeed;
				moveSpeed *= sprintMultiplier;

				kcc->SetInputDirection(moveDirection * moveSpeed, false);

				FPVector3 velocity = kcc->Data.DynamicVelocity;

				if (input.Jump.WasPressed) {
					status->WaterEntryActive = false;
					velocity.Y += player->SwimJumpSpeed;
				} else if (status->WaterEntryActive) {
					FP response = player->WaterEntryResistance * frame.DeltaTime;

					if (response > FP._1)
						response = FP._1;

					velocity.Y += -velocity.Y * response;

					if (velocity.Y > -VelocityTolerance && velocity.Y < VelocityTolerance) {
						velocity.Y = FP._0;
						status->WaterEntryActive = false;
					}
				} else if (status->WaterState == PlayerWaterState.Surface) {
					velocity.Y = FP._0;
				} else {
					FP response = player->SwimVerticalResponsiveness * frame.DeltaTime;

					if (response > FP._1)
						response = FP._1;

					velocity.Y += (verticalSpeed - velocity.Y) * response;
				}

				kcc->SetDynamicVelocity(velocity);

				if (kcc->IsGrounded && verticalSpeed > FP._0)
					kcc->Jump(FPVector3.Up * verticalSpeed);

				return;
			}

			FP walkSpeed = player->WalkSpeed * sprintMultiplier;
			FPVector3 walkDirection = kcc->Data.TransformRotation * input.MoveDirection.XOY;

			kcc->SetInputDirection(walkDirection * walkSpeed, false);

			if (input.Jump.WasPressed && kcc->IsGrounded)
				kcc->Jump(FPVector3.Up * player->NormalJump);
		}
	}
}