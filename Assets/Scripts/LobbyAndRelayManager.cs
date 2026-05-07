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
using UnityEngine.UI;

public class LobbyAndRelayManager : MonoBehaviour
{
    public static LobbyAndRelayManager Instance;

    // เก็บชื่อผู้เล่นเป็น static เพื่อให้จำค่าข้าม Scene ได้
    public static string PlayerName = "Player";
    private bool isServicesInitialized = false;

    [Header("Level Settings")]
    public List<string> levelScenes = new List<string>();
    private int currentLevelIndex = 0;

    [Header("UI Panels")]
    public GameObject nameInputPanel;
    public GameObject lobbyMenuPanel;
    public GameObject waitingRoomPanel;

    [Header("UI Inputs & Texts")]
    public TMP_InputField nameInput;
    public TMP_InputField joinInput;
    public TextMeshProUGUI lobbyCodeText;

    [Header("UI Buttons (Assign in Inspector)")]
    // [Architect Note] ต้องลากปุ่มจาก Hierarchy มาใส่ช่องพวกนี้ทั้งหมด
    public Button confirmNameBtn;
    public Button createLobbyBtn;
    public Button joinLobbyBtn;
    public Button leaveWaitingRoomBtn;
    public Button copyCodeBtn;
    public Button hostStartGameBtn; // ปุ่มเริ่มเกมสำหรับ Host

    private Lobby currentLobby;
    private const int MaxPlayers = 4;

    private void Awake()
    {
        // [Architect Pattern]: Singleton & DDOL
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupButtonListeners(); // ผูกปุ่มครั้งแรกที่เริ่มเกม
        }
        else if (Instance != this)
        {
            // [Architect Pattern]: Reference Handover
            // ส่งมอบ UI ชุดใหม่จาก Scene ที่เพิ่งโหลดมา ให้กับตัวหลัก แล้วทำลายตัวเองทิ้ง
            Instance.UpdateUIReferences(this);
            Destroy(gameObject);
        }
    }

    async void Start()
    {
        // ถ้าเป็นตัวโคลนที่กำลังจะถูกทำลาย ไม่ต้องทำอะไร
        if (Instance != this) return;

        UpdateUIState();
        await InitializeAndSignIn();
    }

    // ================== ระบบ Handover & Event Binding ==================

    /// <summary>
    /// ผูกฟังก์ชันเข้ากับปุ่มผ่าน Code (Dynamic Binding)
    /// ป้องกันปัญหาปุ่มพัง (Missing Reference) เมื่อโหลด Scene ใหม่
    /// </summary>
    private void SetupButtonListeners()
    {
        if (confirmNameBtn)
        {
            confirmNameBtn.onClick.RemoveAllListeners();
            confirmNameBtn.onClick.AddListener(OnConfirmNameClicked);
        }
        if (createLobbyBtn)
        {
            createLobbyBtn.onClick.RemoveAllListeners();
            createLobbyBtn.onClick.AddListener(CreateLobbyAndStartHost);
        }
        if (joinLobbyBtn)
        {
            joinLobbyBtn.onClick.RemoveAllListeners();
            joinLobbyBtn.onClick.AddListener(OnClickJoinButton);
        }
        if (leaveWaitingRoomBtn)
        {
            leaveWaitingRoomBtn.onClick.RemoveAllListeners();
            leaveWaitingRoomBtn.onClick.AddListener(LeaveRoomAndReturnToMenu);
        }
        if (copyCodeBtn)
        {
            copyCodeBtn.onClick.RemoveAllListeners();
            copyCodeBtn.onClick.AddListener(CopyLobbyCode);
        }
        if (hostStartGameBtn)
        {
            hostStartGameBtn.onClick.RemoveAllListeners();
            hostStartGameBtn.onClick.AddListener(HostStartGame);
        }
    }

    /// <summary>
    /// อัปเดตตัวแปรทั้งหมดให้ชี้ไปยัง UI ใน Scene ล่าสุด
    /// </summary>
    public void UpdateUIReferences(LobbyAndRelayManager newInstance)
    {
        // 1. รับค่า Panel & Input
        this.nameInputPanel = newInstance.nameInputPanel;
        this.lobbyMenuPanel = newInstance.lobbyMenuPanel;
        this.waitingRoomPanel = newInstance.waitingRoomPanel;
        this.nameInput = newInstance.nameInput;
        this.joinInput = newInstance.joinInput;
        this.lobbyCodeText = newInstance.lobbyCodeText;

        // 2. รับค่า Button
        this.confirmNameBtn = newInstance.confirmNameBtn;
        this.createLobbyBtn = newInstance.createLobbyBtn;
        this.joinLobbyBtn = newInstance.joinLobbyBtn;
        this.leaveWaitingRoomBtn = newInstance.leaveWaitingRoomBtn;
        this.copyCodeBtn = newInstance.copyCodeBtn;
        this.hostStartGameBtn = newInstance.hostStartGameBtn;

        // 3. ผูก Event ปุ่มใหม่
        SetupButtonListeners();

        // 4. อัปเดตหน้าตา UI
        UpdateUIState();
    }

    private void UpdateUIState()
    {
        if (!string.IsNullOrEmpty(PlayerName) && PlayerName != "Player")
        {
            SwitchToPanel(lobbyMenuPanel); // มีชื่อแล้ว ข้ามไปหน้า Lobby
        }
        else
        {
            SwitchToPanel(nameInputPanel); // ยังไม่มีชื่อ ไปหน้าตั้งชื่อ
        }
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

    // ================== ระบบ Core & Network ==================

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
        if (isServicesInitialized) return;

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        isServicesInitialized = true;
    }

    public async void CreateLobbyAndStartHost()
    {
        // ปิด NetworkManager ตัวเก่าก่อนถ้ามันค้างอยู่
        if (NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            await Awaitable.NextFrameAsync();
        }

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

            if (NetworkManager.Singleton.StartHost())
            {
                lobbyCodeText.text = currentLobby.LobbyCode;
                if (hostStartGameBtn) hostStartGameBtn.gameObject.SetActive(true);
                SwitchToPanel(waitingRoomPanel);
            }
        }
        catch (LobbyServiceException e) { Debug.LogError($"[Lobby Error] {e.Message}"); }
    }

    public void OnClickJoinButton()
    {
        if (joinInput != null && !string.IsNullOrEmpty(joinInput.text)) JoinLobbyWithCode(joinInput.text);
    }

    private async void JoinLobbyWithCode(string lobbyCode)
    {
        if (NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            await Awaitable.NextFrameAsync();
        }

        try
        {
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            string relayJoinCode = currentLobby.Data["RelayCode"].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(
                joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port, joinAllocation.AllocationIdBytes, joinAllocation.Key, joinAllocation.ConnectionData, joinAllocation.HostConnectionData);

            if (NetworkManager.Singleton.StartClient())
            {
                lobbyCodeText.text = currentLobby.LobbyCode;
                if (hostStartGameBtn) hostStartGameBtn.gameObject.SetActive(false); // Client เริ่มเกมไม่ได้
                SwitchToPanel(waitingRoomPanel);
            }
        }
        catch (LobbyServiceException e) { Debug.LogError($"[Lobby Error] {e.Message}"); }
    }

    private async void KeepLobbyAlive(string lobbyId)
    {
        while (currentLobby != null)
        {
            // [Unity 6 Feature]: Awaitable ประหยัด Memory กว่า Task.Delay
            await Awaitable.WaitForSecondsAsync(15f);

            if (currentLobby != null)
            {
                try { await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId); }
                catch { /* ป้องกัน Error หาก Lobby โดนทำลายไปแล้วระหว่างรอ */ }
            }
        }
    }

    // ================== ระบบ Leave & Clean up ==================

    public async void LeaveRoomAndReturnToMenu()
    {
        await CleanUpLobby();
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        SwitchToPanel(lobbyMenuPanel);
    }

    public async void LeaveGameAndReturnToMenu()
    {
        await CleanUpLobby();
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();

        // โหลด Scene หน้าเมนูใหม่ (Scene 0)
        var loadOp = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(0);
        while (!loadOp.isDone)
        {
            await Awaitable.NextFrameAsync();
        }
    }

    private async Task CleanUpLobby()
    {
        if (currentLobby != null)
        {
            try
            {
                if (currentLobby.HostId == AuthenticationService.Instance.PlayerId)
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            catch (System.Exception e) { Debug.LogWarning($"Lobby CleanUp Warning: {e.Message}"); }
            finally { currentLobby = null; }
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