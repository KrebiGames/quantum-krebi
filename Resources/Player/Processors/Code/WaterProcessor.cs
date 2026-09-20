namespace Quantum {
	using Photon.Deterministic;

	public unsafe class WaterProcessor : KCCProcessor, EnvironmentProcessor.IPrepareData {
		private static readonly FP EntryVelocityThreshold = FP.FromFloat_UNSAFE(-0.1f);

		public override bool OnEnter(KCCContext context, KCCProcessorInfo processorInfo, KCCOverlapHit overlapHit) {
			if (context.Frame.Unsafe.TryGetPointer<PlayerStatus>(context.KCC->Entity, out var status)) {
				status->WaterSurfaceY = context.KCC->Data.TargetPosition.Y;
				status->WaterState = PlayerWaterState.Surface;
				status->WaterEntryActive = context.KCC->Data.DynamicVelocity.Y < EntryVelocityThreshold;
			}

			return true;
		}

		public override bool OnExit(KCCContext context, KCCProcessorInfo processorInfo) {
			if (context.Frame.Unsafe.TryGetPointer<PlayerStatus>(context.KCC->Entity, out var status)) {
				status->WaterState = PlayerWaterState.None;
				status->WaterEntryActive = false;
			}

			return true;
		}

		public void PrepareData(KCCContext context, KCCProcessorInfo processorInfo) {
			if (!context.Frame.Unsafe.TryGetPointer<PlayerStatus>(context.KCC->Entity, out var status))
				return;

			if (status->WaterState == PlayerWaterState.None)
				return;

			context.KCC->Data.Gravity = FPVector3.Zero;
		}
	}
}