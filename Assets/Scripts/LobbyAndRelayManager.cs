using System;
using System.Collections.Generic;
using System.Threading;
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
    [Tooltip("กดปุ่ม + เพื่อเพิ่มรายชื่อ Scene ด่านต่างๆ ตามลำดับ")]
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

    // [เพิ่มใหม่] ตัวควบคุมสำหรับสั่งยกเลิกระบบ Heartbeat ป้องกัน Memory Leak
    private CancellationTokenSource heartbeatSource;

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
        if (NetworkManager.Singleton.IsServer)
        {
            if (levelScenes.Count > 0)
            {
                currentLevelIndex = 0;
                NetworkManager.Singleton.SceneManager.LoadScene(levelScenes[currentLevelIndex], LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("คุณยังไม่ได้ใส่ชื่อด่านใน List Level Scenes ใน Inspector!");
            }
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
        else
        {
            Debug.Log("จบทุกด่านแล้ว! พากลับหน้า Lobby");
            NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
            SwitchToPanel(nameInputPanel);
        }
    }

    // ================== ส่วนจัดการ UI และ Relay ==================

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
        if (currentLobby != null)
        {
            GUIUtility.systemCopyBuffer = currentLobby.LobbyCode;
        }
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

            // [แก้ไข] ส่ง Token ไปคุม Heartbeat
            heartbeatSource = new CancellationTokenSource();
            KeepLobbyAlive(currentLobby.Id, heartbeatSource.Token);

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
        if (joinInput != null && !string.IsNullOrEmpty(joinInput.text))
        {
            JoinLobbyWithCode(joinInput.text);
        }
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

    // [แก้ไข] อัปเกรดเป็น Awaitable + CancellationToken
    private async void KeepLobbyAlive(string lobbyId, CancellationToken token)
    {
        try
        {
            while (currentLobby != null && !token.IsCancellationRequested)
            {
                // ใช้ Awaitable ของ Unity 6 แทน Task.Delay
                await Awaitable.WaitForSecondsAsync(15f, token);
                if (currentLobby != null)
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("[LobbyManager] ระบบส่งสัญญาณ Heartbeat ถูกระงับเรียบร้อยแล้ว");
        }
    }

    // ================== [เพิ่มใหม่] ระบบออกจาก Lobby ==================

    /// <summary>
    /// ผูกฟังก์ชันนี้เข้ากับปุ่ม "Leave Room" หรือ "Back" ในหน้า Waiting Room UI
    /// </summary>
    public async void OnClickLeaveLobby()
    {
        await LeaveLobbyAsync();
    }

    private async Awaitable LeaveLobbyAsync()
    {
        if (currentLobby == null) return;

        string lobbyId = currentLobby.Id;
        string playerId = AuthenticationService.Instance.PlayerId;

        try
        {
            // 1. ระงับ Heartbeat (ถ้าเป็น Host) ป้องกัน Memory Leak
            if (heartbeatSource != null)
            {
                heartbeatSource.Cancel();
                heartbeatSource.Dispose();
                heartbeatSource = null;
            }

            // 2. ออกจาก Lobby ผ่าน Service
            if (currentLobby.HostId == playerId)
            {
                // ถ้าเป็นหัวห้อง ใหลบห้องทิ้งไปเลย
                await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                Debug.Log("Host Deleted the Lobby");
            }
            else
            {
                // ถ้าเป็นผู้เล่นทั่วไป ให้ถอดชื่อตัวเองออก
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                Debug.Log("Client Left the Lobby");
            }

            // 3. ปิดการเชื่อมต่อของ Netcode For GameObjects (Relay)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // 4. ล้างค่าข้อมูลห้องเพื่อไม่ให้ค้าง
            currentLobby = null;

            // 5. เปลี่ยน UI กลับไปหน้าเมนู
            SwitchToPanel(lobbyMenuPanel);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"เกิดข้อผิดพลาดตอนออกห้อง: {e.Message}");
        }
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}