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
    [Tooltip("กดปุ่ม + เพื่อเพิ่มรายชื่อ Scene ด่านต่างๆ ตามลำดับ")]
    public List<string> levelScenes = new List<string>();
    private int currentLevelIndex = 0; // ตัวจำว่าตอนนี้อยู่ด่านที่เท่าไหร่

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
        // ป้องกันไม่ให้ Manager ตัวนี้ถูกทำลายตอนเปลี่ยนด่าน (เพื่อให้มันจำ Level Index ได้)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // <--- คำสั่งสำคัญที่ทำให้รอดจากการเปลี่ยน Scene
        }
        else
        {
            Destroy(gameObject); // ถ้ามีซ้ำให้ทำลายทิ้ง
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
                currentLevelIndex = 0; // เริ่มต้นที่ด่านแรก (Index 0)
                NetworkManager.Singleton.SceneManager.LoadScene(levelScenes[currentLevelIndex], LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("คุณยังไม่ได้ใส่ชื่อด่านใน List Level Scenes ใน Inspector!");
            }
        }
    }

    // สร้างฟังก์ชันใหม่สำหรับให้ Host กดเพื่อไปด่านต่อไป
    public void HostLoadNextLevel()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        currentLevelIndex++; // ขยับไปด่านถัดไป

        if (currentLevelIndex < levelScenes.Count)
        {
            // โหลดด่านถัดไป
            NetworkManager.Singleton.SceneManager.LoadScene(levelScenes[currentLevelIndex], LoadSceneMode.Single);
        }
        else
        {
            Debug.Log("จบทุกด่านแล้ว! พากลับหน้า Lobby");
            // วนกลับมาด่านแรก หรือหน้า Lobby (อย่าลืมใส่ชื่อซีน Lobby ของคุณตรงนี้)
            NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);

            // รีเซ็ต UI ให้กลับมาหน้าแรก
            SwitchToPanel(nameInputPanel);
        }
    }

    // ================== ส่วนจัดการ UI และ Relay (คงเดิม) ==================
    // ... โค้ดส่วนที่เหลือ (OnConfirmNameClicked, CreateLobby, JoinLobby ฯลฯ) วางต่อตรงนี้ได้เลยครับ ...

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

    private async void KeepLobbyAlive(string lobbyId)
    {
        while (currentLobby != null)
        {
            await Task.Delay(15000);
            if (currentLobby != null) await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
        }
    }
}