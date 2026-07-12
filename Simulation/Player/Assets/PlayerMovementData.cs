using Photon.Deterministic;
using Quantum.Inspector;

namespace Quantum {
	public partial class PlayerMovementData : AssetObject {
		public bool FaceAimDirection = false;
		public FP DefaultRotationSpeed = 4;
		public FP QuickRotationSpeed = 15;

		public FP JumpCoyoteTime = FP._0_20;
		public FP NoMovementBraking = 5;
		public FP RespawnDuration = 5;

		public AssetRef<CharacterController3DConfig> DefaultKCCSettings;
		public AssetRef<CharacterController3DConfig> NoMovementKCCSettings;

		public unsafe void UpdateKCCSettings(Frame frame, EntityRef playerEntityRef) {
			//PlayerStatus* playerStatus = frame.Unsafe.GetPointer<PlayerStatus>(playerEntityRef);
			//AbilityInventory* abilityInventory = frame.Unsafe.GetPointer<AbilityInventory>(playerEntityRef);
			//CharacterController3D* kcc = frame.Unsafe.GetPointer<CharacterController3D>(playerEntityRef);

			//CharacterController3DConfig config;

			//if (playerStatus->IsKnockbacked || abilityInventory->HasActiveAbility)
			//	config = frame.FindAsset<CharacterController3DConfig>(NoMovementKCCSettings.Id);
			//else if (playerStatus->IsHoldingBall)
			//	config = frame.FindAsset<CharacterController3DConfig>(CarryingBallKCCSettings.Id);
			//else
			//	config = frame.FindAsset<CharacterController3DConfig>(DefaultKCCSettings.Id);

			//kcc->SetConfig(frame, config);
		}
	}
}
