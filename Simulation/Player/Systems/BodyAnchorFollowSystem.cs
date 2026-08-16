namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class BodyAnchorFollowSystem : SystemMainThreadFilter<BodyAnchorFollowSystem.Filter> {
		public struct Filter {
			public EntityRef Entity;
			public Transform3D* Transform;
			public CrabOrientation* Orientation;
			public EntityGroup* EntityGroup;
		}

		public override void Update(Frame frame, ref Filter filter) {
			FPQuaternion bodyRotation = FPQuaternion.Euler(FP._0, filter.Orientation->WorldYaw, FP._0);

			foreach (var (childEntity, index) in frame.GetEntityGroupIterator(filter.Entity)) {
				if (!frame.Has<BodyAnchor>(childEntity) || !frame.Unsafe.TryGetPointer<Transform3D>(childEntity, out var bodyAnchorTransform))
					continue;

				bodyAnchorTransform->Position = filter.Transform->Position;
				bodyAnchorTransform->Rotation = bodyRotation;
			}
		}
	}
}