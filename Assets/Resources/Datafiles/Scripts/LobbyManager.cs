using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Scened;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class LobbyManager : NetworkBehaviour
{
    [SerializeField] private LobbyUIController uiController;
    private const int MAX_PLAYERS = 20;
    private const int MIN_PLAYERS_TO_START = 8;
    private const float COUNTDOWN_DURATION = 10f;
    private const string GAME_SCENE = "BattleRoyaleMap";

    public enum MatchState
    {
        WaitingForPlayers,
        Countdown,
        InGame,
        GameOver
    }

    [System.Serializable]
    public class LobbyPlayer
    {
        public NetworkConnection connection;
        public string playerName;
        public bool isReady;
        public int desiredTeam = -1;
    }
    private bool isLobbyCreated = false;
    private Dictionary<NetworkConnection, LobbyPlayer> lobbyPlayers = new Dictionary<NetworkConnection, LobbyPlayer>();
    private MatchState currentState = MatchState.WaitingForPlayers;
    private float countdownTimer = COUNTDOWN_DURATION;
    private string currentLobbyName;
    private static Dictionary<NetworkConnection, int> pendingTeamAssignments = new Dictionary<NetworkConnection, int>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        ResetLobby();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("Client Started");

        // Add a small delay to ensure everything is properly initialized
        StartCoroutine(InitializeUIWithDelay());
    }
    private IEnumerator InitializeUIWithDelay()
    {
        yield return new WaitForSeconds(0.1f);

        if (uiController == null)
        {
            uiController = FindFirstObjectByType<LobbyUIController>();
            Debug.Log($"Found UI Controller: {uiController != null}");
        }

        if (uiController != null)
        {
            var clientConnection = NetworkManager.ClientManager.Connection;
            Debug.Log($"Initializing UI with client connection: {clientConnection}");
            uiController.Initialize(clientConnection);
        }
        else
        {
            Debug.LogError("Could not find LobbyUIController!");
        }
    }

    public void RequestJoinLobby(string playerName)
    {
        Debug.Log($"Requesting to join lobby as: {playerName}");
        if (!IsSpawned)
        {
            Debug.LogError("LobbyManager is not spawned!");
            return;
        }
        ServerJoinLobbyRequest(playerName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServerJoinLobbyRequest(string playerName)
    {
        NetworkConnection conn = LocalConnection;
        Debug.Log($"Server received join request from {playerName}");

        if (!isLobbyCreated)
        {
            Debug.LogError("No lobby exists to join!");
            ClientRpcLobbyJoinFailed(conn, "No lobby exists!");
            return;
        }

        if (lobbyPlayers.Count >= MAX_PLAYERS)
        {
            ClientRpcLobbyJoinFailed(conn, "Lobby is full!");
            return;
        }

        AddPlayer(conn, playerName);
        // Send the lobby name to the specific client
        ClientRpcUpdateLobbyName(conn, currentLobbyName);
    }
    #region Server RPCs

    // Add this client-side method
    public void RequestCreateLobby(string lobbyName, string playerName)
    {
        Debug.Log($"Requesting lobby creation: {lobbyName} for player: {playerName}");
        if (!IsSpawned)
        {
            Debug.LogError("LobbyManager is not spawned!");
            return;
        }
        ServerCreateLobby(lobbyName, playerName);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ServerCreateLobby(string lobbyName, string playerName)
    {
        Debug.Log($"Creating lobby on server: {lobbyName} for player: {playerName}");
        NetworkConnection conn = LocalConnection;
        currentLobbyName = lobbyName;
        isLobbyCreated = true;
        AddPlayer(conn, playerName);
        // Send to all clients
        ObserversUpdateLobbyName(lobbyName);
    }
    [ObserversRpc]
    private void ObserversUpdateLobbyName(string lobbyName)
    {
        Debug.Log($"Updating lobby name to: {lobbyName}");
        if (uiController != null)
            uiController.UpdateLobbyName(lobbyName);
    }

    [ServerRpc(RequireOwnership = false)]
    public void CreateLobby(string lobbyName, NetworkConnection conn, string playerName)
    {
        Debug.Log($"Creating lobby: {lobbyName} for player: {playerName}"); // Debug log
        currentLobbyName = lobbyName;
        AddPlayer(conn, playerName);
        ClientRpcUpdateLobbyName(conn, lobbyName);
    }

    [ServerRpc(RequireOwnership = false)]
    public void JoinLobbyRequest(string playerName, NetworkConnection conn)
    {
        if (lobbyPlayers.Count >= MAX_PLAYERS)
        {
            ClientRpcLobbyJoinFailed(conn, "Lobby is full!");
            return;
        }

        AddPlayer(conn, playerName);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerReady(NetworkConnection conn, bool ready)
    {
        if (lobbyPlayers.TryGetValue(conn, out LobbyPlayer player))
        {
            player.isReady = ready;
            UpdateLobbyState();
            BroadcastLobbyUpdate();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetDesiredTeam(NetworkConnection conn, int teamId)
    {
        if (lobbyPlayers.TryGetValue(conn, out LobbyPlayer player))
        {
            player.desiredTeam = teamId;
            BroadcastLobbyUpdate();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void LeaveLobby(NetworkConnection conn)
    {
        RemovePlayer(conn);
    }

    #endregion

    #region Client RPCs

    [TargetRpc]
    private void ClientRpcUpdateLobbyName(NetworkConnection target, string lobbyName)
    {
        Debug.Log($"Updating lobby name to: {lobbyName}");
        if (uiController != null)
            uiController.UpdateLobbyName(lobbyName);
    }

    [TargetRpc]
    private void ClientRpcLobbyJoinFailed(NetworkConnection conn, string reason)
    {
        if (uiController != null)
            uiController.ShowError(reason);
    }

    [ObserversRpc]
    private void BroadcastLobbyUpdate()
    {
        Debug.Log($"Broadcasting lobby update. Player count: {lobbyPlayers.Count}");
        if (uiController != null)
        {
            var playerList = lobbyPlayers.Values.ToList();
            Debug.Log($"Sending player list to UI. Count: {playerList.Count}");
            uiController.UpdatePlayerList(playerList);
        }
    }

        [ObserversRpc]
    private void BroadcastCountdown(float remainingTime)
    {
        if (uiController != null)
            uiController.UpdateCountdown(remainingTime);
    }

    [ObserversRpc]
    private void UpdateGameState(string status)
    {
        if (uiController != null)
            uiController.UpdateStatus(status);
    }

    #endregion

    #region Private Methods

    private void AddPlayer(NetworkConnection conn, string playerName)
    {
        if (!lobbyPlayers.ContainsKey(conn))
        {
            Debug.Log($"Adding player: {playerName} to lobby"); // Debug log
            var player = new LobbyPlayer
            {
                connection = conn,
                playerName = playerName,
                isReady = false,
                desiredTeam = -1
            };

            lobbyPlayers.Add(conn, player);
            UpdateGameState($"Players: {lobbyPlayers.Count}/{MAX_PLAYERS}");
            BroadcastLobbyUpdate(); // This should update the UI
        }
    }

    private void RemovePlayer(NetworkConnection conn)
    {
        if (lobbyPlayers.Remove(conn))
        {
            UpdateLobbyState();
            BroadcastLobbyUpdate();
            UpdateGameState($"Players: {lobbyPlayers.Count}/{MAX_PLAYERS}");
        }
    }

    private void UpdateLobbyState()
    {
        bool allPlayersReady = lobbyPlayers.Count >= MIN_PLAYERS_TO_START &&
                              lobbyPlayers.Values.All(p => p.isReady);

        if (allPlayersReady && currentState == MatchState.WaitingForPlayers)
        {
            currentState = MatchState.Countdown;
            countdownTimer = COUNTDOWN_DURATION;
        }
        else if (!allPlayersReady && currentState == MatchState.Countdown)
        {
            currentState = MatchState.WaitingForPlayers;
            UpdateGameState("Waiting for players to be ready...");
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (currentState == MatchState.Countdown)
        {
            countdownTimer -= Time.deltaTime;
            BroadcastCountdown(countdownTimer);

            if (countdownTimer <= 0f)
            {
                StartMatch();
            }
        }
    }

    private void StartMatch()
    {
        AssignTeams();

        SceneLoadData sld = new SceneLoadData(GAME_SCENE);
        sld.ReplaceScenes = ReplaceOption.All;
        NetworkManager.SceneManager.LoadGlobalScenes(sld);
    }

    private void AssignTeams()
    {
        var players = lobbyPlayers.Values.ToList();
        var teamAssignments = new Dictionary<int, List<LobbyPlayer>>();

        for (int i = 0; i < 4; i++)
            teamAssignments[i] = new List<LobbyPlayer>();

        // Assign preferred teams first
        foreach (var player in players.Where(p => p.desiredTeam != -1))
        {
            if (teamAssignments[player.desiredTeam].Count < 5)
            {
                teamAssignments[player.desiredTeam].Add(player);
            }
        }

        // Distribute remaining players
        int currentTeam = 0;
        foreach (var player in players.Where(p => !teamAssignments.Values.Any(t => t.Contains(p))))
        {
            while (teamAssignments[currentTeam].Count >= 5)
                currentTeam = (currentTeam + 1) % 4;

            teamAssignments[currentTeam].Add(player);
            currentTeam = (currentTeam + 1) % 4;
        }

        // Store assignments
        pendingTeamAssignments.Clear();
        foreach (var kvp in teamAssignments)
        {
            foreach (var player in kvp.Value)
            {
                if (player.connection != null)
                    pendingTeamAssignments[player.connection] = kvp.Key;
            }
        }
    }

    private void ResetLobby()
    {
        lobbyPlayers.Clear();
        currentState = MatchState.WaitingForPlayers;
        countdownTimer = COUNTDOWN_DURATION;
        UpdateGameState("Waiting for players...");
    }

    public static int GetTeamAssignment(NetworkConnection conn)
    {
        return pendingTeamAssignments.TryGetValue(conn, out int teamId) ? teamId : -1;
    }

    #endregion
}