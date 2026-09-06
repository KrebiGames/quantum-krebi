using UnityEngine;
using Quantum;

public class ClawsAnimation : QuantumEntityViewComponent {
	[SerializeField] Transform[] entities;
	[SerializeField] Transform[] IKTtargets;

	[SerializeField] Vector3 idleRotation;
	[SerializeField] Vector3 punchRotation;
	[SerializeField] Vector3 reachRotation;
	[SerializeField] Vector3 lockRotationOffset;

	[SerializeField] float rotationSmooth = 10f;

	private ClawState[] states;
	private Quaternion[] lockedRotations;

	public override void OnInitialize() {
		states = new ClawState[IKTtargets.Length];
		lockedRotations = new Quaternion[IKTtargets.Length];

		for (int i = 0; i < IKTtargets.Length; i++)
			lockedRotations[i] = IKTtargets[i].rotation;

		QuantumEvent.Subscribe<EventClawStateChanged>(this, OnClawStateChanged);
	}

	void LateUpdate() {
		float t = 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime);

		for (int i = 0; i < entities.Length; i++) {
			IKTtargets[i].position = entities[i].position;

			if (UsesLockedRotation(states[i]))
				IKTtargets[i].rotation = Quaternion.Slerp(IKTtargets[i].rotation, lockedRotations[i], t);
			else
				IKTtargets[i].localRotation = Quaternion.Slerp(IKTtargets[i].localRotation, GetLocalRotation(i), t);
		}
	}

	private void OnClawStateChanged(EventClawStateChanged e) {
		if (e.Entity != EntityRef)
			return;

		int index = (int)e.Side;
		states[index] = e.State;

		if (UsesLockedRotation(e.State))
			lockedRotations[index] = GetLockedRotation(index, e.TargetPosition.ToUnityVector3());
	}

	private bool UsesLockedRotation(ClawState state) => state == ClawState.Grab;

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

	private Quaternion GetLockedRotation(int index, Vector3 targetPosition) {
		Vector3 direction = targetPosition - entities[index].position;

		if (direction.sqrMagnitude < 0.0001f)
			return IKTtargets[index].rotation;

		Vector3 offset = lockRotationOffset;

		if (index == (int)ClawSide.Right)
			offset.z = -offset.z;

		return Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(offset);
	}
}