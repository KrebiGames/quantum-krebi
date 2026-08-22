namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class BodyAnchorSystem : SystemMainThreadFilter<BodyAnchorSystem.Filter> {
		public struct Filter {
			public EntityRef Entity;
			public Transform3D* Transform;
			public BodyOrientation* Orientation;
			public EntityGroup* EntityGroup;
		}

		public override void Update(Frame frame, ref Filter filter) {
			FPVector3 forward = filter.Transform->Forward;
			FP baseYaw = FPMath.Atan2(forward.X, forward.Z) * FP.Rad2Deg;

			foreach (var (childEntity, index) in frame.GetEntityGroupIterator(filter.Entity)) {
				if (!frame.Unsafe.TryGetPointer<BodyAnchor>(childEntity, out var bodyAnchor) || !frame.Unsafe.TryGetPointer<Transform3D>(childEntity, out var bodyAnchorTransform))
					continue;

				if (!bodyAnchor->Initialized) {
					bodyAnchor->PreviousBaseYaw = baseYaw;
					bodyAnchor->ContinuousBaseYaw = baseYaw;
					bodyAnchor->Initialized = true;
				} else {
					FP yawDelta = FPMath.AngleBetweenDegrees(bodyAnchor->PreviousBaseYaw, baseYaw);
					bodyAnchor->ContinuousBaseYaw += yawDelta;
					bodyAnchor->PreviousBaseYaw = baseYaw;
				}

				FP bodyYaw = bodyAnchor->ContinuousBaseYaw + filter.Orientation->LocalYaw;

				bodyAnchorTransform->Position = filter.Transform->Position;
				bodyAnchorTransform->Rotation = FPQuaternion.Euler(FP._0, bodyYaw, FP._0);
			}
		}
	}
}