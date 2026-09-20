using Quantum;
using Unity.Cinemachine;
using UnityEngine;

public class CameraView : QuantumEntityViewComponent {
	[SerializeField] private Vector2 normalVerticalRange = new(5f, 80f);
	[SerializeField] private Vector2 waterVerticalRange = new(-80f, 80f);
	[SerializeField] private float verticalRangeTransitionSpeed = 150f;

	private CinemachineOrbitalFollow orbitalFollow;
	private Vector2 targetVerticalRange;

	public override void OnActivate(Frame frame) {
		PlayerStatus status = frame.Get<PlayerStatus>(EntityRef);

		if (!Game.PlayerIsLocal(status.PlayerRef))
			return;

		LocalPlayerAccess localPlayerAccess = LocalPlayersController.Instance.GetLocalPlayerAccess(status.PlayerRef);

		if (localPlayerAccess == null)
			return;

		orbitalFollow = localPlayerAccess.CMCamera.GetComponent<CinemachineOrbitalFollow>();
		targetVerticalRange = status.WaterState != PlayerWaterState.None ? waterVerticalRange : normalVerticalRange;
	}

	public override void OnUpdateView() {
		if (orbitalFollow == null)
			return;

		PlayerStatus status = PredictedFrame.Get<PlayerStatus>(EntityRef);
		targetVerticalRange = status.WaterState != PlayerWaterState.None ? waterVerticalRange : normalVerticalRange;

		InputAxis axis = orbitalFollow.VerticalAxis;
		axis.Range.x = Mathf.MoveTowards(axis.Range.x, targetVerticalRange.x, verticalRangeTransitionSpeed * Time.deltaTime);
		axis.Range.y = Mathf.MoveTowards(axis.Range.y, targetVerticalRange.y, verticalRangeTransitionSpeed * Time.deltaTime);
		axis.Value = Mathf.Clamp(axis.Value, axis.Range.x, axis.Range.y);
		orbitalFollow.VerticalAxis = axis;
	}

	public override void OnDeactivate() {
		orbitalFollow = null;
	}
}