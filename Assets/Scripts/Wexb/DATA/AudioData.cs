using UnityEngine;

[CreateAssetMenu(fileName = "NewAudioData", menuName = "Audio/Audio Data")]
public class AudioData : ScriptableObject
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    public bool loop = false;

    // Helper method เพื่อสุ่ม Pitch เล็กน้อย (ทำให้เสียงไม่น่าเบื่อเวลาได้ยินซ้ำๆ)
    public void Play(AudioSource source)
    {
        if (clip == null) return;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch + Random.Range(-0.05f, 0.05f); // เพิ่มความ Dynamic
        source.loop = loop;
        source.Play();
    }
}