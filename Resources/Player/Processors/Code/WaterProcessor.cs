namespace Quantum {
	using Photon.Deterministic;

	public unsafe class WaterProcessor : KCCProcessor, EnvironmentProcessor.IPrepareData {
		public override bool OnEnter(KCCContext context, KCCProcessorInfo processorInfo, KCCOverlapHit overlapHit) {
			if (context.Frame.Unsafe.TryGetPointer<PlayerStatus>(context.KCC->Entity, out var status))
				status->IsSwimming = true;

			return true;
		}

		public override bool OnExit(KCCContext context, KCCProcessorInfo processorInfo) {
			if (context.Frame.Unsafe.TryGetPointer<PlayerStatus>(context.KCC->Entity, out var status))
				status->IsSwimming = false;

			return true;
		}

		public void PrepareData(KCCContext context, KCCProcessorInfo processorInfo) {
			context.KCC->Data.Gravity = FPVector3.Zero;
		}
	}
}