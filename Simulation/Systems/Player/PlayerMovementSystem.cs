namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class PlayerMovementSystem : SystemMainThreadFilter<PlayerMovementSystem.Filter> {
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
			KCC* kcc = filter.KCC;

			// Movement
			FP moveSpeed = player->WalkSpeed;

			if (input.Sprint.IsDown && kcc->IsGrounded)
				moveSpeed = FP._1;

			FPVector3 moveDirection =
				kcc->Data.TransformRotation * input.MoveDirection.XOY;

			kcc->SetInputDirection(moveDirection * moveSpeed, false);

			// Look
			PlayerMovementData movementData =
				frame.FindAsset<PlayerMovementData>(filter.PlayerStatus->PlayerMovementData.Id);

			kcc->AddLookRotation(
				FP._0,
				input.AimDirection.X * movementData.DefaultRotationSpeed * frame.DeltaTime
			);

			// Jump
			if (input.Jump.WasPressed && kcc->IsGrounded)
				kcc->Jump(FPVector3.Up * player->NormalJump);
		}
	}
}