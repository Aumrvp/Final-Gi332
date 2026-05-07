using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Pool Settings")]
    [SerializeField] private int sfxPoolSize = 10;

    [Header("Mixer Settings")]
    [SerializeField] private AudioMixerGroup bgmGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    private AudioSource _bgmSource;
    private List<AudioSource> _sfxPool = new List<AudioSource>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePool()
    {
        // สร้าง BGM Source และส่งเข้า BGM Group
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.outputAudioMixerGroup = bgmGroup;

        // สร้าง SFX Pool และส่งเข้า SFX Group
        for (int i = 0; i < sfxPoolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = sfxGroup;
            _sfxPool.Add(source);
        }
    }

    // สำหรับเล่น BGM (เพลงประกอบ)
    public void PlayBGM(AudioData data)
    {
        data.Play(_bgmSource);
    }

    // สำหรับเล่น SFX (เสียงสั้นๆ)
    public void PlaySFX(AudioData data)
    {
        // หา Source ที่ว่างอยู่ (ไม่ได้เล่น)
        AudioSource source = _sfxPool.Find(s => !s.isPlaying);

        // ถ้าเต็มจริงๆ ให้ใช้ตัวแรก (หรือขยาย Pool เพิ่มได้)
        if (source == null) source = _sfxPool[0];

        data.Play(source);
    }
}