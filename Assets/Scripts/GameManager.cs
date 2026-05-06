using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("UI References")]
    public GameObject mainGamePanel;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI countdownText;
    public GameObject smashButtonObj;

    [Header("Game Settings")]
    public float gameDuration = 30f;
    public float countdownDuration = 3f;

    [Header("Countdown Visual Settings")]
    public Color countdownNumberColor = Color.yellow;
    public Color goTextColor = Color.green;
    public float startScale = 3.5f;
    public float endScale = 1.0f;
    public float animDuration = 0.4f;

    [Header("Timer Low-Time FX")]
    public float lowTimeThreshold = 10f;
    public float criticalTimeThreshold = 5f;
    public Color timerNormalColor = new Color(1f, 0.98f, 0.94f, 1f);
    public Color timerWarningColor = new Color(1f, 0.65f, 0.2f, 1f);
    public Color timerCriticalColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    public float timerPulseScale = 1.35f;
    public float timerPulseDuration = 0.35f;

    public NetworkVariable<bool> isCountingDown = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> currentCountdown = new NetworkVariable<float>(3f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> isGameActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> timeRemaining = new NetworkVariable<float>(30f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int _lastCeilTime = -1;
    private Coroutine _countdownAnimCoroutine;
    private int _lastTimerTick = -1;
    private Coroutine _timerPulseCoroutine;
    private Vector3 _timerBaseScale = Vector3.one;
    private Vector3 _timerBasePos = Vector3.zero;

    private void Awake() { Instance = this; }

    public override void OnNetworkSpawn()
    {
        timeRemaining.OnValueChanged += OnTimeChanged;
        currentCountdown.OnValueChanged += OnCountdownChanged;
        isCountingDown.OnValueChanged += OnCountingDownChanged;
        isGameActive.OnValueChanged += OnGameActiveChanged;

        if (timerText != null)
        {
            _timerBaseScale = timerText.transform.localScale;
            _timerBasePos = timerText.transform.localPosition;
            timerText.color = timerNormalColor;
        }

        OnCountingDownChanged(false, isCountingDown.Value);
        OnGameActiveChanged(false, isGameActive.Value);

        if (IsServer)
        {
            currentCountdown.Value = countdownDuration;
            isCountingDown.Value = true;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (isCountingDown.Value)
        {
            if (currentCountdown.Value > 0)
                currentCountdown.Value -= Time.deltaTime;
            else
            {
                isCountingDown.Value = false;
                timeRemaining.Value = gameDuration;
                isGameActive.Value = true;
            }
        }
        else if (isGameActive.Value)
        {
            if (timeRemaining.Value > 0)
                timeRemaining.Value -= Time.deltaTime;
            else
            {
                timeRemaining.Value = 0;
                isGameActive.Value = false;
                DetermineWinner();
            }
        }
    }

    private void OnCountingDownChanged(bool oldValue, bool newValue)
    {
        if (newValue && countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            _lastCeilTime = -1;
        }
    }

    private void OnGameActiveChanged(bool oldValue, bool newValue)
    {
        if (newValue && countdownText != null) countdownText.gameObject.SetActive(false);
        if (newValue && smashButtonObj != null) smashButtonObj.SetActive(true);
    }

    private void OnCountdownChanged(float oldValue, float newValue)
    {
        if (countdownText == null || !isCountingDown.Value) return;

        int ceilTime = Mathf.CeilToInt(newValue);
        if (ceilTime != _lastCeilTime && ceilTime >= 0)
        {
            _lastCeilTime = ceilTime;

            // ล้างค่า Rich Text เก่าและตั้งสีใหม่จาก Inspector
            countdownText.text = ceilTime > 0 ? ceilTime.ToString() : "GO!";
            countdownText.color = ceilTime > 0 ? countdownNumberColor : goTextColor;

            // รันแอนิเมชันแบบใช้ตัวแปร Coroutine เพื่อป้องกันการรันซ้อน
            if (_countdownAnimCoroutine != null) StopCoroutine(_countdownAnimCoroutine);
            _countdownAnimCoroutine = StartCoroutine(AnimateCountdownText());
        }
    }

    private IEnumerator AnimateCountdownText()
    {
        Transform t = countdownText.transform;
        // เช็ค CanvasGroup ถ้าไม่มีให้ข้ามไป (ไม่พัง)
        CanvasGroup cg = countdownText.GetComponent<CanvasGroup>();

        float elapsed = 0;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / animDuration;
            float curve = 1.0f - Mathf.Pow(1.0f - p, 3);

            t.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, curve);
            if (cg != null) cg.alpha = Mathf.Lerp(0.5f, 1f, p);

            yield return null;
        }
        t.localScale = Vector3.one * endScale;
        if (cg != null) cg.alpha = 1f;
    }

    private void OnTimeChanged(float oldValue, float newValue)
    {
        if (timerText == null) return;

        int ceil = Mathf.CeilToInt(newValue);
        timerText.text = $"⏱ {ceil}";

        if (newValue > lowTimeThreshold)
        {
            timerText.color = timerNormalColor;
            _lastTimerTick = -1;
        }
        else if (newValue > criticalTimeThreshold)
        {
            timerText.color = timerWarningColor;
        }
        else
        {
            timerText.color = timerCriticalColor;
        }

        if (newValue <= lowTimeThreshold && ceil > 0 && ceil != _lastTimerTick)
        {
            _lastTimerTick = ceil;
            if (_timerPulseCoroutine != null) StopCoroutine(_timerPulseCoroutine);
            _timerPulseCoroutine = StartCoroutine(PulseTimerText(ceil <= criticalTimeThreshold));
        }
    }

    private IEnumerator PulseTimerText(bool critical)
    {
        if (timerText == null) yield break;
        Transform t = timerText.transform;
        float peak = critical ? timerPulseScale + 0.15f : timerPulseScale;
        float elapsed = 0f;
        while (elapsed < timerPulseDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / timerPulseDuration);
            float ease = 1f - Mathf.Pow(1f - p, 3);
            float s = Mathf.Lerp(peak, 1f, ease);
            float shakeX = critical ? Mathf.Sin(p * Mathf.PI * 8f) * 6f * (1f - p) : 0f;
            t.localScale = _timerBaseScale * s;
            t.localPosition = new Vector3(_timerBasePos.x + shakeX, _timerBasePos.y, _timerBasePos.z);
            yield return null;
        }
        t.localScale = _timerBaseScale;
        t.localPosition = _timerBasePos;
    }

    private void DetermineWinner()
    {
        PlayerScore[] players = FindObjectsByType<PlayerScore>(FindObjectsSortMode.None);
        PlayerScore winner = null;
        int max = -1;
        foreach (var p in players) { if (p.currentScore.Value > max) { max = p.currentScore.Value; winner = p; } }
        if (winner != null) ShowWinnerClientRpc(winner.playerName.Value.ToString(), max);
    }

    [ClientRpc]
    private void ShowWinnerClientRpc(string winnerName, int score) => SmashUIManager.Instance?.ShowWinner(winnerName, score);
}