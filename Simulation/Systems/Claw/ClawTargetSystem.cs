namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class ClawTargetSystem : SystemMainThreadFilter<ClawTargetSystem.Filter> {
		public struct Filter {
			public EntityRef Entity;
			public Transform3D* Transform;
			public Claw* Claw;
			public ClawTarget* Target;
		}

		public override void Update(Frame frame, ref Filter filter) {
			filter.Target->Entity = EntityRef.None;
			filter.Target->Point = FPVector3.Zero;

			Shape3D shape = Shape3D.CreateSphere(filter.Target->DetectRange);
			var hits = frame.Physics3D.OverlapShape(filter.Transform->Position, FPQuaternion.Identity, shape, -1, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);
			FP bestDistance = FP.MaxValue;

			for (int i = 0; i < hits.Count; i++) {
				EntityRef entity = hits[i].Entity;

				if (entity == EntityRef.None || entity == filter.Entity || !frame.Has<Interactable>(entity))
					continue;

				FPVector3 point = hits[i].Point;
				FP distance = (point - filter.Transform->Position).SqrMagnitude;

				if (distance >= bestDistance)
					continue;

				bestDistance = distance;
				filter.Target->Entity = entity;
				filter.Target->Point = point;
			}
		}
	}
}