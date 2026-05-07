using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyAndRelayManager : MonoBehaviour
{
    public static LobbyAndRelayManager Instance;
    public static string PlayerName = "Player";

    [Header("Level Settings")]
    public List<string> levelScenes = new List<string>();
    private int currentLevelIndex = 0;

    [Header("UI Panels")]
    public GameObject nameInputPanel;
    public GameObject lobbyMenuPanel;
    public GameObject waitingRoomPanel;

    [Header("Name Input UI")]
    public TMP_InputField nameInput;

    [Header("Lobby Menu UI")]
    public TMP_InputField joinInput;

    [Header("Waiting Room UI")]
    public TextMeshProUGUI lobbyCodeText;
    public GameObject hostStartGameButton;

    private Lobby currentLobby;
    private const int MaxPlayers = 4;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        SwitchToPanel(nameInputPanel);
        await InitializeAndSignIn();
    }

    // ================== ระบบสลับด่าน (Level Management) ==================

    public void HostStartGame()
    {
        if (NetworkManager.Singleton.IsServer && levelScenes.Count > 0)
        {
            currentLevelIndex = 0;
            NetworkManager.Singleton.SceneManager.LoadScene(levelScenes[currentLevelIndex], LoadSceneMode.Single);
        }
    }

    public void HostLoadNextLevel()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        currentLevelIndex++;
        if (currentLevelIndex < levelScenes.Count)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(levelScenes[currentLevelIndex], LoadSceneMode.Single);
        }
       
    }

    // ================== ระบบ Leave & Disconnect (New) ==================

    /// <summary>
    /// ใช้สำหรับกดออกจากหน้า Waiting Room กลับไปหน้า Lobby Menu
    /// </summary>
    public async void LeaveRoomAndReturnToMenu()
    {
        Debug.Log("[Session] Leaving Waiting Room...");

        // 1. จัดการ Lobby Service
        if (currentLobby != null)
        {
            try
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                if (currentLobby.HostId == playerId)
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
            }
            catch (System.Exception e) { Debug.LogWarning($"Lobby Error: {e.Message}"); }
            currentLobby = null;
        }

        // 2. ปิด Network Connection
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 3. จัดการ UI
        SwitchToPanel(lobbyMenuPanel);
    }

    /// <summary>
    /// ใช้สำหรับกดออกจาก "ระหว่างเล่นเกม" เพื่อกลับไปหน้าเมนูหลัก
    /// </summary>
    public async void LeaveGameAndReturnToMenu()
    {
        Debug.Log("[Session] Leaving Game Scene...");

        // 1. จัดการ Lobby Service
        if (currentLobby != null)
        {
            try
            {
                if (currentLobby.HostId == AuthenticationService.Instance.PlayerId)
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            catch { /* Ignore errors during exit */ }
            currentLobby = null;
        }

        // 2. Shutdown Network
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 3. โหลด Scene เริ่มต้นใหม่ (Scene ที่มี UI Lobby)
        var loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(0);
        while (!loadOp.isDone) await Task.Yield();

        // 4. รีเซ็ต UI Panel
        SwitchToPanel(lobbyMenuPanel);
    }

    // ================== ส่วนจัดการระบบพื้นฐาน (Core) ==================

    public void OnConfirmNameClicked()
    {
        if (!string.IsNullOrEmpty(nameInput.text))
        {
            PlayerName = nameInput.text;
            SwitchToPanel(lobbyMenuPanel);
        }
    }

    private void SwitchToPanel(GameObject activePanel)
    {
        if (nameInputPanel) nameInputPanel.SetActive(false);
        if (lobbyMenuPanel) lobbyMenuPanel.SetActive(false);
        if (waitingRoomPanel) waitingRoomPanel.SetActive(false);

        if (activePanel != null) activePanel.SetActive(true);
    }

    public void CopyLobbyCode()
    {
        if (currentLobby != null) GUIUtility.systemCopyBuffer = currentLobby.LobbyCode;
    }

    private async Task InitializeAndSignIn()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    public async void CreateLobbyAndStartHost()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            CreateLobbyOptions lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject> { { "RelayCode", new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) } }
            };
            currentLobby = await LobbyService.Instance.CreateLobbyAsync("Smash Lobby", MaxPlayers, lobbyOptions);

            KeepLobbyAlive(currentLobby.Id);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);

            NetworkManager.Singleton.StartHost();

            lobbyCodeText.text = currentLobby.LobbyCode;
            hostStartGameButton.SetActive(true);
            SwitchToPanel(waitingRoomPanel);
        }
        catch (LobbyServiceException e) { Debug.LogError(e); }
    }

    public void OnClickJoinButton()
    {
        if (joinInput != null && !string.IsNullOrEmpty(joinInput.text)) JoinLobbyWithCode(joinInput.text);
    }

    private async void JoinLobbyWithCode(string lobbyCode)
    {
        try
        {
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            string relayJoinCode = currentLobby.Data["RelayCode"].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port, joinAllocation.AllocationIdBytes, joinAllocation.Key, joinAllocation.ConnectionData, joinAllocation.HostConnectionData);

            NetworkManager.Singleton.StartClient();

            lobbyCodeText.text = currentLobby.LobbyCode;
            hostStartGameButton.SetActive(false);
            SwitchToPanel(waitingRoomPanel);
        }
        catch (LobbyServiceException e) { Debug.LogError(e); }
    }

    private async void KeepLobbyAlive(string lobbyId)
    {
        while (currentLobby != null)
        {
            await Task.Delay(15000);
            if (currentLobby != null) await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        }
    }

    public void QuitApplication()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}