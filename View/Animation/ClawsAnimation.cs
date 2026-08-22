using UnityEngine;
using Quantum;

public class ClawsAnimation : QuantumEntityViewComponent {
	[SerializeField] bool usePredictedFrame = true;
	[SerializeField] Transform[] claws;
	[SerializeField] Transform[] targets;

	void LateUpdate() {
		for (int i = 0; i < claws.Length; i++) {
			targets[i].position = claws[i].position;
		}
	}
}