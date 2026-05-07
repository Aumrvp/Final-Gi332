using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class SmashUIManager : MonoBehaviour
{
    public static SmashUIManager Instance;

    [Header("Winner UI")]
    public GameObject winnerPanel;
    public TextMeshProUGUI winnerText;
    public Animator winnerAnimator;

    [Header("Gameplay")]
    public GameObject smashButton;
    public GameObject rematchButton;

    [Header("Rematch Button Label")]
    [Tooltip("ถ้าไม่ใส่ จะหา TextMeshProUGUI ในลูกของ rematchButton อัตโนมัติ")]
    public TextMeshProUGUI rematchButtonLabel;
    public string nextLevelText = "Next Level";
    public string backToLobbyText = "Back to Lobby";

    [Header("Visual Feedback")]
    public SceneSmashAnimator sceneSmashAnimator;

    private Coroutine _idlePulse;
    private Coroutine _punch;

    private void Awake() { Instance = this; }

    private void Start()
    {
        if (rematchButton != null) rematchButton.SetActive(false);
    }

    public void OnPressSmashButton()
    {
        if (GlobalAudioManager.Instance != null)
        {
            OnCliclPlaySmashSound();
        }
        else
        {
            Debug.LogWarning("UIAudioManager หายไป! อย่าลืมเอาไปวางใน Scene นะครับ");
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient) return;
        if (GameManager.Instance == null || !GameManager.Instance.isGameActive.Value) return;

        var player = NetworkManager.Singleton.LocalClient.PlayerObject;
        if (player != null && player.TryGetComponent(out PlayerScore ps))
            ps.OnClickSmashButton();

        if (sceneSmashAnimator != null)
        {
            sceneSmashAnimator.AddSmashEnergy();
        }

        if (_punch != null) StopCoroutine(_punch);
        _punch = StartCoroutine(PunchButton());
        if (smashButton != null) StartCoroutine(FloatingScore(smashButton.transform));
    }

    public void StartIdlePulse()
    {
        StopIdlePulse();
        if (smashButton != null)
            _idlePulse = StartCoroutine(IdlePulse(smashButton.transform));
    }

    public void StopIdlePulse()
    {
        if (_idlePulse != null) { StopCoroutine(_idlePulse); _idlePulse = null; }
        if (smashButton != null) smashButton.transform.localScale = Vector3.one;
    }

    private IEnumerator IdlePulse(Transform t)
    {
        while (true)
        {
            yield return ScaleTo(t, 1.07f, 0.5f);
            yield return ScaleTo(t, 1.0f, 0.5f);
        }
    }

    private IEnumerator PunchButton()
    {
        if (smashButton == null) yield break;
        if (_idlePulse != null) { StopCoroutine(_idlePulse); _idlePulse = null; }
        Transform t = smashButton.transform;
        yield return ScaleTo(t, 0.82f, 0.05f);
        yield return ScaleTo(t, 1.18f, 0.09f);
        yield return ScaleTo(t, 1.0f, 0.1f);
        if (GameManager.Instance != null && GameManager.Instance.isGameActive.Value)
            _idlePulse = StartCoroutine(IdlePulse(t));
    }

    private IEnumerator ScaleTo(Transform t, float target, float dur)
    {
        float start = t.localScale.x, e = 0;
        while (e < dur)
        {
            e += Time.deltaTime;
            t.localScale = Vector3.one * Mathf.Lerp(start, target, e / dur);
            yield return null;
        }
        t.localScale = Vector3.one * target;
    }

    private IEnumerator FloatingScore(Transform anchor)
    {
        var go = new GameObject("ScorePopup");
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = Vector3.up * 60f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "<b>+1</b>";
        tmp.fontSize = 56;
        tmp.color = Color.yellow;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 80);

        float dur = 0.75f, elapsed = 0;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / dur;
            go.transform.localPosition = Vector3.up * Mathf.Lerp(60f, 200f, p);
            tmp.color = new Color(1f, 0.9f, 0.1f, 1 - p * p);
            yield return null;
        }
        Destroy(go);
    }

    public void ShowWinner(string winnerName, int score)
    {
        StopIdlePulse();
        if (winnerPanel != null)
        {
            winnerPanel.SetActive(true);
            StartCoroutine(BounceIn(winnerPanel.transform));
        }
        if (winnerText != null)
            winnerText.text = $"<size=150%>🏆</size>\n<b><color=#FFD700>{winnerName}</color></b>\n<color=#FFFFFF>WINS!</color>\n\n<color=#88FF88><size=70%>Score: {score} pts</size></color>";
        if (smashButton != null) smashButton.SetActive(false);

        if (NetworkManager.Singleton.IsServer)
        {
            if (rematchButton != null)
            {
                rematchButton.SetActive(true);
                UpdateRematchButtonLabel();
            }
        }
        else
        {
            if (rematchButton != null) rematchButton.SetActive(false);
        }

        if (winnerAnimator != null) winnerAnimator.SetTrigger("Show");
    }

    private void UpdateRematchButtonLabel()
    {
        if (rematchButtonLabel == null && rematchButton != null)
            rematchButtonLabel = rematchButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (rematchButtonLabel == null) return;

        bool isLast = LobbyAndRelayManager.Instance != null && LobbyAndRelayManager.Instance.IsLastLevel();
        rematchButtonLabel.text = isLast ? backToLobbyText : nextLevelText;
    }

    private IEnumerator BounceIn(Transform t)
    {
        t.localScale = Vector3.zero;
        float elapsed = 0, dur = 0.5f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / dur;
            float s = p < 0.55f ? Mathf.Lerp(0f, 1.25f, p / 0.55f)
                    : p < 0.8f ? Mathf.Lerp(1.25f, 0.9f, (p - 0.55f) / 0.25f)
                                 : Mathf.Lerp(0.9f, 1.0f, (p - 0.8f) / 0.2f);
            t.localScale = Vector3.one * s;
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    public void HideWinner()
    {
        if (winnerPanel != null) winnerPanel.SetActive(false);
        if (smashButton != null) smashButton.SetActive(true);
        if (rematchButton != null) rematchButton.SetActive(false);
    }

    public void OnClickRematch() // ทำงานเมื่อกดปุ่ม "ไปด่านต่อไป"
    {
        Debug.Log("[SmashUI] กดปุ่มเปลี่ยนด่านแล้ว! กำลังตรวจสอบสิทธิ์...");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[SmashUI] ไม่พบ NetworkManager (คุณอาจไม่ได้ต่อเน็ตหรือเริ่มเกมจาก Lobby)");
            return;
        }

        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[SmashUI] คุณคือ Host! สั่งให้ LobbyManager โหลดด่านต่อไป...");

            if (LobbyAndRelayManager.Instance != null)
            {
                LobbyAndRelayManager.Instance.HostLoadNextLevel();
            }
            else
            {
                Debug.LogError("[SmashUI] ❌ พัง! ไม่พบ LobbyAndRelayManager! คุณได้เริ่มเกมจากด่าน LobbyScene หรือไม่?");
            }
        }
        else
        {
            Debug.LogWarning("[SmashUI] คุณเป็นแค่ Client ไม่มีสิทธิ์กดปุ่มนี้ (ปุ่มนี้ควรจะซ่อนอยู่)");
        }
    }

    public void OnCliclPlaySmashSound()
    {
        GlobalAudioManager.Instance.PlayClickSound();
    }

    // Backward compat: Game_Level2 scene's UnityEvent references "OnClickNextLevel"
    public void OnClickNextLevel()
    {
        OnClickRematch();
    }
}