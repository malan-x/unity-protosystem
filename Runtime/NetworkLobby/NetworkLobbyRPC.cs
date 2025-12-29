// Packages/com.protosystem.core/Runtime/NetworkLobby/NetworkLobbyRPC.cs
using Unity.Netcode;

namespace ProtoSystem.NetworkLobby
{
    /// <summary>
    /// Сетевой компонент для RPC вызовов лобби.
    /// Создаётся и спавнится NetworkLobbySystem.
    /// </summary>
    public class NetworkLobbyRPC : NetworkBehaviour
    {
        private NetworkLobbySystem _system;

        public void Initialize(NetworkLobbySystem system)
        {
            _system = system;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool ready, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var players = _system.Players;
            
            if (players.TryGetValue(clientId, out var player))
            {
                player.IsReady = ready;
                _system.AddPlayer(clientId, player);

                SyncPlayerDataClientRpc(clientId, player.PlayerName, ready);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerNameServerRpc(string name, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var players = _system.Players;
            
            if (players.TryGetValue(clientId, out var player))
            {
                player.PlayerName = name;
                _system.AddPlayer(clientId, player);

                SyncPlayerDataClientRpc(clientId, name, player.IsReady);
            }
        }

        [ClientRpc]
        private void SyncPlayerDataClientRpc(ulong clientId, string name, bool ready)
        {
            _system.OnPlayerDataUpdated(clientId, name, ready);
        }
    }
}
