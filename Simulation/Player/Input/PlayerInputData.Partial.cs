namespace Quantum {
	using Photon.Deterministic;

	unsafe partial struct PlayerInputData {

		public static implicit operator Input(PlayerInputData playerInput) {
			Input input = default;

			input._a = playerInput.Jump;
			input._b = playerInput.Sprint;

			input.ThumbSticks.Regular->_leftThumb = playerInput.MoveDirection;
			input.ThumbSticks.Regular->_rightThumb = playerInput.AimDirection;

			return input;
		}

		public static implicit operator PlayerInputData(Input input) {
			PlayerInputData playerInput = default;

			playerInput.Jump = input._a;
			playerInput.Sprint = input._b;

			playerInput.MoveDirection = input.ThumbSticks.Regular->_leftThumb;
			playerInput.AimDirection = input.ThumbSticks.Regular->_rightThumb;

			return playerInput;
		}
	}
}