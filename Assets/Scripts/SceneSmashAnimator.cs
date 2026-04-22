using UnityEngine;

[RequireComponent(typeof(Animator))]
public class SceneSmashAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("ความเร็วต่ำสุดของแอนิเมชัน")]
    public float minAnimSpeed = 1.0f;
    [Tooltip("ความเร็วสูงสุดของแอนิเมชัน (รัวปุ่ม)")]
    public float maxAnimSpeed = 3.5f;

    [Header("Energy & Decay System")]
    public float energyPerClick = 1.0f;
    public float energyDecayRate = 2.5f;
    public float maxEnergy = 10f;

    private Animator _animator;
    private float _currentEnergy = 0f;

    // Cache Hash เพื่อ Performance ที่ดี (ไม่สร้างขยะใน Memory)
    private readonly int _animSpeedMultiplierHash = Animator.StringToHash("SmashSpeedMultiplier");
    private readonly int _smashTriggerHash = Animator.StringToHash("DoSmash");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // เปิด Method นี้เป็น Public เพื่อให้ UI Manager เรียกใช้ได้
    public void AddSmashEnergy()
    {
        _currentEnergy += energyPerClick;
        _currentEnergy = Mathf.Clamp(_currentEnergy, 0f, maxEnergy);

        // สั่งเล่นแอนิเมชันทุบ
        _animator.SetTrigger(_smashTriggerHash);
    }

    private void Update()
    {
        if (_currentEnergy > 0)
        {
            // ลดค่าพลังงานลงตามเวลา (Decay)
            _currentEnergy -= energyDecayRate * Time.deltaTime;
            _currentEnergy = Mathf.Max(_currentEnergy, 0f);
        }

        UpdateAnimatorSpeed();
    }

    private void UpdateAnimatorSpeed()
    {
        float normalizedEnergy = _currentEnergy / maxEnergy;
        float targetSpeed = Mathf.Lerp(minAnimSpeed, maxAnimSpeed, normalizedEnergy);

        _animator.SetFloat(_animSpeedMultiplierHash, targetSpeed);
    }
}