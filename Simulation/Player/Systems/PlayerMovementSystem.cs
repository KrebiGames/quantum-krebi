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
			QuantumDemoInputTopDown input = *frame.GetPlayerInput(filter.PlayerStatus->PlayerRef);

			Player* player = filter.Player;
			KCC* kcc = filter.KCC;
			PlayerMovementData movementData = frame.FindAsset<PlayerMovementData>(filter.PlayerStatus->PlayerMovementData.Id);

			kcc->SetInputDirection(kcc->Data.TransformRotation * input.MoveDirection.XOY, true);

			kcc->AddLookRotation(
				FP._0,
				input.AimDirection.X * movementData.DefaultRotationSpeed * frame.DeltaTime
			);

			if (input.Jump.WasPressed && kcc->IsGrounded)
				kcc->Jump(FPVector3.Up * player->JumpForce);
		}
	}
}