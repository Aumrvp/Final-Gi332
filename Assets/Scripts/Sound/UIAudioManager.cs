using UnityEngine;
using UnityEngine.SceneManagement; // ต้องใช้ namespace นี้สำหรับดักจับ Scene

public class GlobalAudioManager : MonoBehaviour
{
    public static GlobalAudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Data")]
    [SerializeField] private GlobalAudioData audioData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // ตัวซ้ำใน Scene: หยุด AudioSource ของตัวเองทันทีกัน Overlap แล้วทำลายทิ้ง
            // ไม่สมัคร sceneLoaded เพื่อให้มีตัวเดียวที่จัดการเพลง (Persistent Instance)
            if (musicSource != null) musicSource.Stop();
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // ยกเลิก subscription เฉพาะตัว persistent ตัวจริง ป้องกัน Memory Leak
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    // เมธอดนี้จะถูกเรียกอัตโนมัติเมื่อ Unity (หรือ Netcode) โหลด Scene ใหม่เสร็จ
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (audioData == null) return;

        // 1. ไปถาม Data ว่า Scene นี้ต้องใช้เพลงอะไร
        AudioClip newBGM = audioData.GetBGMForScene(scene.name);

        // 2. สั่งเปลี่ยนเพลง
        PlayBGM(newBGM);
    }

    // ================= BGM CONTROL =================

    public void PlayBGM(AudioClip clip)
    {
        if (musicSource == null) return;

        if (clip == null)
        {
            StopBGM();
            return;
        }

        // Logic สำคัญ: ถ้าเพลงที่ให้เล่น คือเพลงเดียวกับที่กำลังเล่นอยู่ ไม่ต้องทำอะไร (ป้องกันเพลง restart ตัวเองตอนกด Rematch ด่านเดิม)
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        // หยุดเพลงเก่าให้ขาดก่อน เพื่อกันเพลงเก่ากับใหม่ซ้อนกัน (เช่น พอกด Play จาก MainMenu ไป Game_Level1)
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.volume = audioData.bgmVolume;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void StopBGM()
    {
        musicSource.Stop();
    }

    // ================= SFX CONTROL (เหมือนเดิม) =================

    public void PlayClickSound()
    {
        if (audioData.buttonClick != null)
            sfxSource.PlayOneShot(audioData.buttonClick, audioData.sfxVolume);
    }

    // ... (เมธอด SFX อื่นๆ คงไว้เหมือนเดิมได้เลยครับ)
}