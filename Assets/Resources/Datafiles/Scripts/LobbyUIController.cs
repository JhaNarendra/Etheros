using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Connection;
using System.Collections.Generic;
using System.Collections;
using FishNet.Managing;
using FishNet.Object;
using FishNet;
using FishNet.Transporting;

public class LobbyUIController : MonoBehaviour
{
    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button joinLobbyButton;

    [Header("Error Display")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private Button errorCloseButton;

    [Header("Loading")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Lobby")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private Transform playerListContent;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TMP_Dropdown teamDropdown;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Button leaveLobbyButton;

    [SerializeField] private LobbyManager lobbyManager;
    private NetworkConnection localConnection;
    private bool isReady = false;
    private Dictionary<NetworkConnection, GameObject> playerListEntries = new Dictionary<NetworkConnection, GameObject>();

    private void Start()
    {
        Debug.Log("LobbyUIController Started"); // Add this
        SetupUI();
        ShowMainMenu();
        if (InstanceFinder.NetworkManager != null)
        {
            InstanceFinder.NetworkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
        }
    }

    private void HandleClientConnectionState(ClientConnectionStateArgs args)
    {
        Debug.Log($"Client Connection State Changed: {args.ConnectionState}");
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            var connection = InstanceFinder.NetworkManager.ClientManager.Connection;
            Debug.Log($"Client Connected with connection: {connection}");
            Initialize(connection);
        }
    }
   
    private void SetupUI()
    {
        Debug.Log("Create Lobby Button Clicked"); // Add this
        // Main Menu Setup
        createLobbyButton?.onClick.AddListener(OnCreateLobbyClicked);
        joinLobbyButton?.onClick.AddListener(OnJoinLobbyClicked);

        // Lobby Setup
        readyButton?.onClick.AddListener(ToggleReady);
        leaveLobbyButton?.onClick.AddListener(OnLeaveLobbyClicked);

        if (teamDropdown != null)
        {
            teamDropdown.ClearOptions();
            teamDropdown.AddOptions(new List<string> { "Random", "Red Team", "Blue Team", "Green Team", "Yellow Team" });
            teamDropdown.onValueChanged.AddListener(OnTeamSelected);
        }

        LoadSavedPlayerName();
    }

    private void LoadSavedPlayerName()
    {
        string savedName = PlayerPrefs.GetString("PlayerName", "");
        if (!string.IsNullOrEmpty(savedName) && playerNameInput != null)
        {
            playerNameInput.text = savedName;
        }
    }

    public void Initialize(NetworkConnection connection)
    {
        Debug.Log($"Initialize called with connection: {connection}");
        localConnection = connection;

        if (connection == null)
        {
            Debug.LogError("Received null connection in Initialize!");
        }
    }

    #region UI Event Handlers

    private void OnCreateLobbyClicked()
    {
        Debug.Log("Create Lobby Button Clicked");
        if (ValidateInputs())
        {
            Debug.Log($"Local Connection: {localConnection != null}");
            Debug.Log($"LobbyManager: {lobbyManager != null}");

            if (lobbyManager == null)
            {
                lobbyManager = FindObjectOfType<LobbyManager>();
                if (lobbyManager == null)
                {
                    Debug.LogError("LobbyManager not found!");
                    return;
                }
            }

            SavePlayerName();
            lobbyManager.RequestCreateLobby(lobbyNameInput.text, playerNameInput.text);
            ShowLobby();
        }
    }

    private void OnJoinLobbyClicked()
    {
        Debug.Log("Join Lobby Button Clicked");
        if (!InstanceFinder.NetworkManager.IsClient)
        {
            Debug.LogError("Must be connected as client to join lobby!");
            ShowError("Not connected to server!");
            return;
        }

        if (ValidateInputs())
        {
            if (lobbyManager == null)
            {
                lobbyManager = FindObjectOfType<LobbyManager>();
                if (lobbyManager == null)
                {
                    Debug.LogError("LobbyManager not found!");
                    ShowError("Internal error: LobbyManager not found");
                    return;
                }
            }

            SavePlayerName();
            lobbyManager.RequestJoinLobby(playerNameInput.text);
            ShowLobby();
        }
    }


    private void OnLeaveLobbyClicked()
    {
        lobbyManager.LeaveLobby(localConnection);
        ShowMainMenu();
    }

    private void ToggleReady()
    {
        isReady = !isReady;
        lobbyManager.SetPlayerReady(localConnection, isReady);
        UpdateReadyButton();
    }

    private void OnTeamSelected(int index)
    {
        lobbyManager.SetDesiredTeam(localConnection, index - 1);
    }
    public void OnTeamSelectionFailed(string reason)
    {
        ShowError(reason);
        // Reset dropdown to previous selection
        teamDropdown.value = 0; // Reset to "Random"
    }
    #endregion

    #region UI Updates

    public void UpdateLobbyName(string lobbyName)
    {
        if (lobbyNameText != null)
            lobbyNameText.text = $"Lobby: {lobbyName}";
    }

    public void UpdatePlayerList(List<LobbyManager.LobbyPlayer> players)
    {
        Debug.Log($"Updating player list. Player count: {players.Count}");

        if (playerListContent == null)
        {
            Debug.LogError("Player list content is null!");
            return;
        }

        ClearPlayerList();

        foreach (var player in players)
        {
            Debug.Log($"Creating entry for player: {player.playerName}");
            GameObject playerEntry = Instantiate(playerEntryPrefab, playerListContent);

            // Update name
            var nameText = playerEntry.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = player.playerName;
            else
                Debug.LogError("Name text component not found in player entry prefab!");

            // Other player entry updates...
        }
    }

    public void UpdateCountdown(float time)
    {
        if (countdownPanel != null)
            countdownPanel.SetActive(true);

        if (countdownText != null)
            countdownText.text = $"Match Starting in: {Mathf.CeilToInt(time)}";
    }

    public void UpdateStatus(string status)
    {
        if (statusText != null)
            statusText.text = status;
    }

    public void ShowError(string message)
    {
        Debug.LogWarning(message);
        if (errorPanel != null && errorText != null)
        {
            errorText.text = message;
            errorPanel.SetActive(true);
            StartCoroutine(HideErrorAfterDelay(3f));
        }
    }

    private IEnumerator HideErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        errorPanel?.SetActive(false);
    }

    #endregion

    #region Helper Methods

    private void ShowMainMenu()
    {
        mainMenuPanel?.SetActive(true);
        lobbyPanel?.SetActive(false);
    }

    private void ShowLobby()
    {
        mainMenuPanel?.SetActive(false);
        lobbyPanel?.SetActive(true);
    }

    private void UpdateReadyButton()
    {
        if (readyButtonText != null)
            readyButtonText.text = isReady ? "Not Ready" : "Ready";
    }

    private void ClearPlayerList()
    {
        foreach (var entry in playerListEntries.Values)
        {
            Destroy(entry);
        }
        playerListEntries.Clear();
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrEmpty(playerNameInput?.text))
        {
            ShowError("Please enter a player name!");
            return false;
        }

        if (string.IsNullOrEmpty(lobbyNameInput?.text))
        {
            ShowError("Please enter a lobby name!");
            return false;
        }

        return true;
    }

    private void SavePlayerName()
    {
        PlayerPrefs.SetString("PlayerName", playerNameInput.text);
    }

    public void ShowLoading(string message)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            if (loadingText != null)
                loadingText.text = message;
        }
    }

    public void HideLoading()
    {
        loadingPanel?.SetActive(false);
    }

    #endregion
    private void OnDestroy()
    {
        if (InstanceFinder.NetworkManager != null)
        {
            InstanceFinder.NetworkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
        }
    }
}