using UnityEngine;
using Quantum;

public class ClawsAnimation : QuantumEntityViewComponent {
	[SerializeField] Transform[] entities;
	[SerializeField] Transform[] IKTtargets;

	[SerializeField] Vector3 idleRotation;
	[SerializeField] Vector3 punchRotation;
	[SerializeField] Vector3 reachRotation;
	[SerializeField] Vector3 lockRotationOffset;

	[SerializeField] float grabOffset = 0.05f;
	[SerializeField] float rotationSmooth = 10f;

	private ClawState[] states;
	private Vector3[] grabPoints;
	private bool[] hasGrabPoints;

	public override void OnInitialize() {
		states = new ClawState[IKTtargets.Length];
		grabPoints = new Vector3[IKTtargets.Length];
		hasGrabPoints = new bool[IKTtargets.Length];
	}

	public override void OnUpdateView() {
		for (int i = 0; i < hasGrabPoints.Length; i++)
			hasGrabPoints[i] = false;

		Frame frame = PredictedFrame;

		if (frame == null)
			return;

		foreach (var (clawEntity, index) in frame.GetEntityGroupIterator(EntityRef)) {
			if (!frame.TryGet<Claw>(clawEntity, out var claw))
				continue;

			int side = (int)claw.Side;

			if (side < 0 || side >= states.Length)
				continue;

			states[side] = claw.State;

			if (claw.State != ClawState.Grab || !frame.TryGet<ClawGrab>(clawEntity, out var grab) || grab.Target == EntityRef.None || !frame.TryGet<Transform3D>(grab.Target, out var targetTransform))
				continue;

			grabPoints[side] = (targetTransform.Position + targetTransform.Rotation * grab.LocalPoint).ToUnityVector3();
			hasGrabPoints[side] = true;
		}
	}

	void LateUpdate() {
		float t = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);
		int count = Mathf.Min(entities.Length, IKTtargets.Length);

		for (int i = 0; i < count; i++) {
			Vector3 position = entities[i].position;

			if (states[i] == ClawState.Grab && hasGrabPoints[i]) {
				Vector3 direction = grabPoints[i] - transform.position;

				if (direction.sqrMagnitude > 0.0001f)
					position -= direction.normalized * grabOffset;

				IKTtargets[i].position = position;
				IKTtargets[i].rotation = Quaternion.Slerp(IKTtargets[i].rotation, GetGrabRotation(i, position, grabPoints[i]), t);
			} else {
				IKTtargets[i].position = position;
				IKTtargets[i].localRotation = Quaternion.Slerp(IKTtargets[i].localRotation, GetLocalRotation(i), t);
			}
		}
	}

	private Quaternion GetLocalRotation(int index) {
		Vector3 rotation = states[index] switch {
			ClawState.Punch => punchRotation,
			ClawState.Reach => reachRotation,
			_ => idleRotation
		};

		if (index == (int)ClawSide.Right) {
			rotation.y = -rotation.y;
			rotation.z = -rotation.z;
		}

		return Quaternion.Euler(rotation);
	}

	private Quaternion GetGrabRotation(int index, Vector3 position, Vector3 grabPoint) {
		Vector3 direction = grabPoint - position;

		if (direction.sqrMagnitude < 0.0001f)
			return IKTtargets[index].rotation;

		Vector3 offset = lockRotationOffset;

		if (index == (int)ClawSide.Right)
			offset.z = -offset.z;

		return Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(offset);
	}
}