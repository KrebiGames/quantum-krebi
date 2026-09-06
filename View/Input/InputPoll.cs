using System.Collections.Generic;
using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputPoll : MonoBehaviour {
	[SerializeField] private InputActionReference moveAction;
	[SerializeField] private InputActionReference lookAction;
	[SerializeField] private InputActionReference jumpAction;
	[SerializeField] private InputActionReference sprintAction;
	[SerializeField] private InputActionReference leftClawPunchAction;
	[SerializeField] private InputActionReference rightClawPunchAction;
	[SerializeField] private InputActionReference leftClawReachAction;
	[SerializeField] private InputActionReference rightClawReachAction;
	[SerializeField] private InputActionReference leftClawPinchAction;
	[SerializeField] private InputActionReference rightClawPinchAction;

	private readonly Dictionary<PlayerInput, CachedActions> actionCache = new();

	private class CachedActions {
		public InputAction Move;
		public InputAction Look;
		public InputAction Jump;
		public InputAction Sprint;
		public InputAction LeftClawPunch;
		public InputAction RightClawPunch;
		public InputAction LeftClawReach;
		public InputAction RightClawReach;
		public InputAction LeftClawPinch;
		public InputAction RightClawPinch;
	}

	private void Start() => QuantumCallback.Subscribe<CallbackPollInput>(this, PollInput);

	private void PollInput(CallbackPollInput callback) {
		PlayerInputData input = default;

		var localPlayers = callback.Game.GetLocalPlayers();
		if (localPlayers.Count == 0)
			return;

		var player = localPlayers[callback.PlayerSlot];
		LocalPlayerAccess localPlayerAccess = LocalPlayersController.Instance.GetLocalPlayerAccess(player);

		if (localPlayerAccess != null && localPlayerAccess.LocalPlayer != null) {
			PlayerInput playerInput = localPlayerAccess.PlayerInput;

			if (!actionCache.TryGetValue(playerInput, out CachedActions actions)) {
				actions = new CachedActions {
					Move = playerInput.actions.FindAction(moveAction.action.id),
					Look = playerInput.actions.FindAction(lookAction.action.id),
					Jump = playerInput.actions.FindAction(jumpAction.action.id),
					Sprint = playerInput.actions.FindAction(sprintAction.action.id),
					LeftClawReach = playerInput.actions.FindAction(leftClawReachAction.action.id),
					RightClawReach = playerInput.actions.FindAction(rightClawReachAction.action.id),
					LeftClawPunch = playerInput.actions.FindAction(leftClawPunchAction.action.id),
					RightClawPunch = playerInput.actions.FindAction(rightClawPunchAction.action.id),
					LeftClawPinch = playerInput.actions.FindAction(leftClawPinchAction.action.id),
					RightClawPinch = playerInput.actions.FindAction(rightClawPinchAction.action.id)
				};

				actionCache.Add(playerInput, actions);
			}

			input.MoveDirection = actions.Move.ReadValue<Vector2>().ToFPVector2();

			Vector2 look = actions.Look.ReadValue<Vector2>();
			input.AimDirection = new Vector2(look.x, 0).ToFPVector2();

			input.Jump = actions.Jump.IsPressed();
			input.Sprint = actions.Sprint.IsPressed();

			float leftReach = actions.LeftClawReach.phase == InputActionPhase.Performed ? actions.LeftClawReach.ReadValue<float>() : 0.0f;
			float rightReach = actions.RightClawReach.phase == InputActionPhase.Performed ? actions.RightClawReach.ReadValue<float>() : 0.0f;
			input.LeftClawReach = FP.FromFloat_UNSAFE(ReadReach(actions.LeftClawReach));
			input.RightClawReach = FP.FromFloat_UNSAFE(ReadReach(actions.RightClawReach));

			input.LeftClawPunch = ReadPunch(actions.LeftClawPunch);
			input.RightClawPunch = ReadPunch(actions.RightClawPunch);

			input.LeftClawPinch = actions.LeftClawPinch.IsPressed();
			input.RightClawPinch = actions.RightClawPinch.IsPressed();
		}

		callback.SetInput(input, DeterministicInputFlags.Repeatable);
	}

	private float ReadReach(InputAction action) {
		if (action.activeControl?.device is Mouse)
			return action.phase == InputActionPhase.Performed ? action.ReadValue<float>() : 0.0f;

		return action.ReadValue<float>();
	}

	private bool ReadPunch(InputAction action) => action.WasPerformedThisFrame();
}