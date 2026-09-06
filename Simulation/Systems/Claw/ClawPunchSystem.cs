namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawPunchSystem : SystemSignalsOnly, ISignalOnCollision3D {
		public void OnCollision3D(Frame frame, CollisionInfo3D info) {
			EntityRef clawEntity;
			EntityRef targetEntity;
			Claw* claw;
			ClawPunch* punch;

			if (frame.Unsafe.TryGetPointer<Claw>(info.Entity, out claw) && frame.Unsafe.TryGetPointer<ClawPunch>(info.Entity, out punch)) {
				clawEntity = info.Entity;
				targetEntity = info.Other;
			} else if (frame.Unsafe.TryGetPointer<Claw>(info.Other, out claw) && frame.Unsafe.TryGetPointer<ClawPunch>(info.Other, out punch)) {
				clawEntity = info.Other;
				targetEntity = info.Entity;
			} else {
				return;
			}

			if (claw->State != ClawState.Punch || punch->PunchApplied)
				return;

			if (!frame.Has<Interactable>(targetEntity))
				return;

			if (!frame.Unsafe.TryGetPointer<PhysicsBody3D>(clawEntity, out var clawBody) ||
				!frame.Unsafe.TryGetPointer<PhysicsBody3D>(targetEntity, out var targetBody))
				return;

			if (clawBody->Velocity.SqrMagnitude <= FP._0_01)
				return;

			FPVector3 impulse = clawBody->Velocity.Normalized * punch->PunchStrength;
			targetBody->AddLinearImpulse(impulse);

			punch->PunchApplied = true;
		}
	}
}