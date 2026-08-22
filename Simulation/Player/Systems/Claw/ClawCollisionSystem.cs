namespace Quantum {
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawCollisionSystem : SystemSignalsOnly, ISignalOnCollision3D {
		public void OnCollision3D(Frame frame, CollisionInfo3D info) {
			if (!frame.Unsafe.TryGetPointer<Claw>(info.Entity, out var claw) || !frame.Unsafe.TryGetPointer<ClawGrip>(info.Entity, out var grip))
				return;

			if (grip->Target == info.Other || claw->CollisionSuppressed && claw->CollisionTarget == info.Other)
				info.IgnoreCollision = true;
		}
	}
}