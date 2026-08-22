namespace Quantum {
	using Photon.Deterministic;
	using UnityEngine.Scripting;

	[Preserve]
	public unsafe class KCCBodyInteractionSystem : SystemMainThreadFilter<KCCBodyInteractionSystem.Filter> {
		private static readonly FP ContactMargin = FP.FromFloat_UNSAFE(0.03f);

		public struct Filter {
			public EntityRef Entity;
			public Transform3D* Transform;
			public KCC* KCC;
			public Player* Player;
		}

		public override void Update(Frame frame, ref Filter filter) {
			KCCSettings settings = frame.FindAsset<KCCSettings>(filter.KCC->Settings);

			FP extent = settings.Height * FP._0_50 - settings.Radius;
			Shape3D shape = Shape3D.CreateCapsule(settings.Radius + ContactMargin, extent);

			var hits = frame.Physics3D.OverlapShape(filter.Transform->Position, filter.Transform->Rotation, shape, -1, QueryOptions.HitAll | QueryOptions.ComputeDetailedInfo);

			FPVector3 crabVelocity = filter.KCC->Data.KinematicVelocity + filter.KCC->Data.DynamicVelocity;

			EntityRef bestTarget = EntityRef.None;
			FPVector3 bestNormal = FPVector3.Zero;
			FPVector3 bestPoint = FPVector3.Zero;
			FP bestClosingSpeed = FP._0;

			for (int i = 0; i < hits.Count; i++) {
				EntityRef target = hits[i].Entity;

				if (target == EntityRef.None || target == filter.Entity)
					continue;

				if (!frame.Has<Interactable>(target))
					continue;

				if (!frame.Unsafe.TryGetPointer<PhysicsBody3D>(target, out var body))
					continue;

				if (!frame.Unsafe.TryGetPointer<Transform3D>(target, out var hitTransform))
					continue;

				if (body->IsKinematic)
					continue;

				FPVector3 normal = hits[i].Normal;
				normal.Y = FP._0;

				if (normal.SqrMagnitude <= FP._0)
					continue;

				normal = normal.Normalized;

				FPVector3 toTarget = hitTransform->Position - filter.Transform->Position;
				toTarget.Y = FP._0;

				if (FPVector3.Dot(normal, toTarget) < FP._0)
					normal = -normal;

				FPVector3 objectVelocity = body->GetPointVelocity(hits[i].Point, hitTransform);
				FP closingSpeed = FPVector3.Dot(crabVelocity - objectVelocity, normal);

				if (closingSpeed <= bestClosingSpeed)
					continue;

				bestClosingSpeed = closingSpeed;
				bestTarget = target;
				bestNormal = normal;
				bestPoint = hits[i].Point;
			}

			if (bestTarget == EntityRef.None)
				return;

			if (!frame.Unsafe.TryGetPointer<PhysicsBody3D>(bestTarget, out var targetBody))
				return;

			if (!frame.Unsafe.TryGetPointer<Transform3D>(bestTarget, out var targetTransform))
				return;

			FP crabMass = filter.Player->Mass;
			FP objectMass = targetBody->Mass;

			if (crabMass <= FP._0 || objectMass <= FP._0)
				return;

			FP impulseAmount = bestClosingSpeed / (FP._1 / crabMass + FP._1 / objectMass);
			FPVector3 impulse = bestNormal * impulseAmount;

			targetBody->AddLinearImpulseAtPosition(impulse, bestPoint, targetTransform);
			filter.KCC->AddExternalImpulse(-impulse / crabMass);
		}
	}
}