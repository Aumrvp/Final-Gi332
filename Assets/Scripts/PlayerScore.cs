using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using System;

public class PlayerScore : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> currentScore = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float lastClickTime;
    private float clickCooldown = 0.1f;

    public event Action OnSmashActionExecuted;

    public override void OnNetworkSpawn()
    {
        // เมื่อตัวละครเกิด ให้ส่งชื่อไปให้ Server ทันที
        if (IsOwner)
        {
            SubmitNameServerRpc(LobbyAndRelayManager.PlayerName);
        }

        // แจ้ง ScoreboardManager (ถ้ามีใน Scene นั้น) ให้ลงทะเบียนรายชื่อ
        ScoreboardManager.Instance?.RegisterPlayer(this);

        // เมื่อชื่อหรือคะแนนเปลี่ยน ให้สั่งอัปเดตหน้าจอทันที
        playerName.OnValueChanged += (oldValue, newValue) => { ScoreboardManager.Instance?.RefreshScoreboard(); };
        currentScore.OnValueChanged += (oldValue, newValue) => { ScoreboardManager.Instance?.RefreshScoreboard(); };
    }

    public override void OnNetworkDespawn()
    {
        // เมื่อผู้เล่นออกจากเกม ให้ลบชื่อออกจากกระดาน
        ScoreboardManager.Instance?.UnregisterPlayer(this);
    }

    [ServerRpc]
    private void SubmitNameServerRpc(FixedString32Bytes name)
    {
        playerName.Value = name;
    }

    public void OnClickSmashButton()
    {
        if (!IsOwner) return;
        if (GameManager.Instance == null || !GameManager.Instance.isGameActive.Value) return;
        if (Time.time - lastClickTime < clickCooldown) return;

        lastClickTime = Time.time;
        SubmitScoreServerRpc();

        OnSmashActionExecuted?.Invoke();
    }

    [ServerRpc]
    private void SubmitScoreServerRpc()
    {
        currentScore.Value++;
    }
}