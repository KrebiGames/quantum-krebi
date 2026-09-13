namespace Quantum {
	public unsafe class PlayerSpawnSystem : SystemSignalsOnly, ISignalOnPlayerAdded {
		public void OnPlayerAdded(Frame frame, PlayerRef player, bool firstTime) {
			RuntimePlayer runtimePlayerData = frame.GetPlayerData(player);
			EntityPrototype playerPrototype = frame.FindAsset<EntityPrototype>(runtimePlayerData.PlayerAvatar.Id);
			EntityRef playerEntityRef = frame.Create(playerPrototype);

			PlayerStatus* playerStatus = frame.Unsafe.GetPointer<PlayerStatus>(playerEntityRef);
			Transform3D* playerTransform = frame.Unsafe.GetPointer<Transform3D>(playerEntityRef);

			playerStatus->PlayerRef = player;

			var filtered = frame.Filter<PlayerSpawner, Transform3D>();

			while (filtered.NextUnsafe(out var spawnerEntityRef, out var spawner, out var spawnerTransform)) {
				if (spawner->PlayerRef != player)
					continue;

				playerStatus->SpawnerEntityRef = spawnerEntityRef;
				playerStatus->PlayerTeam = spawner->PlayerTeam;
				playerTransform->Position = spawnerTransform->Position;
				playerTransform->Rotation = spawnerTransform->Rotation;
				break;
			}
		}
	}
}