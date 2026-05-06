using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BouncingBallAnimator : MonoBehaviour
{
    [Header("Bounce Settings")]
    [Tooltip("ความสูงสูงสุดของการเด้ง (หน่วยเป็นเมตร)")]
    public float bounceHeight = 1.5f;
    [Tooltip("ระยะเวลาทั้งหมดของการเด้ง 1 ครั้ง (ขึ้น+ลง)")]
    public float bounceDuration = 0.25f;

    private Vector3 _groundLocalPos;
    private Coroutine _bounceCo;
    private bool _subscribed;
    private PlayerScore _localPlayerScore;

    private void Awake()
    {
        _groundLocalPos = transform.localPosition;
    }

    private void Update()
    {
        if (_subscribed) return;
        TrySubscribeToLocalPlayer();
    }

    private void TrySubscribeToLocalPlayer()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        var localClient = nm.LocalClient;
        if (localClient == null || localClient.PlayerObject == null) return;

        if (!localClient.PlayerObject.TryGetComponent(out PlayerScore ps)) return;

        _localPlayerScore = ps;
        _localPlayerScore.OnSmashActionExecuted += HandleSmash;
        _subscribed = true;
    }

    private void OnDestroy()
    {
        if (_localPlayerScore != null)
            _localPlayerScore.OnSmashActionExecuted -= HandleSmash;
    }

    private void HandleSmash()
    {
        if (_bounceCo != null) StopCoroutine(_bounceCo);
        _bounceCo = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float y = 4f * bounceHeight * t * (1f - t);
            transform.localPosition = _groundLocalPos + Vector3.up * y;
            yield return null;
        }
        transform.localPosition = _groundLocalPos;
        _bounceCo = null;
    }
}
