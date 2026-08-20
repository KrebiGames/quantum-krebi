namespace Quantum {
	using Photon.Deterministic;

	unsafe partial struct PlayerInputData {
		public static implicit operator Input(PlayerInputData playerInput) {
			Input input = default;

			input._a = playerInput.Jump;
			input._b = playerInput.Sprint;
			input._l1 = playerInput.LeftClawKick;
			input._r1 = playerInput.RightClawKick;
			input._c = playerInput.LeftClawPinch;
			input._d = playerInput.RightClawPinch;

			input._analogLeftTrigger = (byte)(int)(FPMath.Clamp01(playerInput.LeftClawReach) * 255);
			input._analogRightTrigger = (byte)(int)(FPMath.Clamp01(playerInput.RightClawReach) * 255);

			input.ThumbSticks.Regular->_leftThumb = playerInput.MoveDirection;
			input.ThumbSticks.Regular->_rightThumb = playerInput.AimDirection;

			return input;
		}

		public static implicit operator PlayerInputData(Input input) {
			PlayerInputData playerInput = default;

			playerInput.Jump = input._a;
			playerInput.Sprint = input._b;
			playerInput.LeftClawKick = input._l1;
			playerInput.RightClawKick = input._r1;
			playerInput.LeftClawPinch = input._c;
			playerInput.RightClawPinch = input._d;

			playerInput.LeftClawReach = (FP)input._analogLeftTrigger / 255;
			playerInput.RightClawReach = (FP)input._analogRightTrigger / 255;

			playerInput.MoveDirection = input.ThumbSticks.Regular->_leftThumb;
			playerInput.AimDirection = input.ThumbSticks.Regular->_rightThumb;

			return playerInput;
		}
	}
}