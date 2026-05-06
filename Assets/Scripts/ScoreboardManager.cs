using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq; // จำเป็นสำหรับการเรียงลำดับ (Sort)
using Unity.Netcode;

public class ScoreboardManager : MonoBehaviour
{
    public static ScoreboardManager Instance;

    [Header("In-Game Scoreboard")]
    public TextMeshProUGUI scoreTextDisplay;

    [Header("Waiting Room Scroll View")]
    public GameObject playerSlotPrefab;
    public Transform waitingRoomContent;

    private List<PlayerScore> players = new List<PlayerScore>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // ดึงรายชื่อกลับมาเวลาเปลี่ยนด่านใหม่
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            players.Clear();
            PlayerScore[] existingPlayers = FindObjectsByType<PlayerScore>(FindObjectsSortMode.None);
            foreach (var p in existingPlayers)
            {
                RegisterPlayer(p);
            }
        }
    }

    public void RegisterPlayer(PlayerScore player)
    {
        if (!players.Contains(player))
        {
            players.Add(player);
        }
        RefreshScoreboard();
    }

    public void UnregisterPlayer(PlayerScore player)
    {
        if (players.Contains(player))
        {
            players.Remove(player);
        }
        RefreshScoreboard();
    }

    public void RefreshScoreboard()
    {
        // ระบบเรียงลำดับคนที่คะแนนสูงสุดของเพื่อนคุณ
        var sorted = players.OrderByDescending(p => p.currentScore.Value).ToList();
        string[] medals = { "1", "2", "3", "4 " };
        string[] scoreColors = { "#FFD700", "#C0C0C0", "#CD7F32", "#FFFFFF" };

        string board = "<b><color=#FFD700>══ SCOREBOARD ══</color></b>\n\n";
        for (int i = 0; i < sorted.Count; i++)
        {
            string pName = string.IsNullOrEmpty(sorted[i].playerName.Value.ToString())
                ? $"Player {sorted[i].OwnerClientId}"
                : sorted[i].playerName.Value.ToString();
            string medal = i < medals.Length ? medals[i] : "   ";
            string sc = i < scoreColors.Length ? scoreColors[i] : "#FFFFFF";
            board += $"{medal} <b>{pName}</b>  <color={sc}>{sorted[i].currentScore.Value} pts</color>\n";
        }

        if (scoreTextDisplay != null) scoreTextDisplay.text = board;

        UpdateWaitingRoomScrollView();
    }

    private void UpdateWaitingRoomScrollView()
    {
        if (playerSlotPrefab == null || waitingRoomContent == null) return;

        foreach (Transform child in waitingRoomContent)
        {
            Destroy(child.gameObject);
        }

        foreach (var player in players)
        {
            string pName = string.IsNullOrEmpty(player.playerName.Value.ToString()) ? $"Player {player.OwnerClientId}" : player.playerName.Value.ToString();

            GameObject newSlot = Instantiate(playerSlotPrefab, waitingRoomContent);
            TextMeshProUGUI slotText = newSlot.GetComponent<TextMeshProUGUI>();
            if (slotText == null) slotText = newSlot.GetComponentInChildren<TextMeshProUGUI>();
            if (slotText != null)
            {
                slotText.text = pName;
            }
        }
    }
}