using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct SceneBGM
{
    [Tooltip("ชื่อ Scene ต้องพิมพ์ให้ตรงกับใน Build Settings เป๊ะๆ")]
    public string sceneName;
    public AudioClip bgmClip;
}

[CreateAssetMenu(fileName = "GlobalAudioData", menuName = "Audio/Global Audio Data")]
public class GlobalAudioData : ScriptableObject
{
    [Header("Background Music Settings")]
    [Tooltip("เพลงที่จะเล่นเป็นค่าเริ่มต้น ถ้าหาชื่อ Scene ไม่เจอ")]
    public AudioClip defaultBGM;

    [Tooltip("ตั้งค่าเพลงประจำด่านต่างๆ")]
    public List<SceneBGM> sceneBGMList;

    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    [Header("Gameplay SFX")]
    public AudioClip countdownTick;
    public AudioClip countdownGo;

    [Header("UI SFX")]
    public AudioClip buttonClick;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // ฟังก์ชันสำหรับค้นหาเพลงจากชื่อ Scene
    public AudioClip GetBGMForScene(string sceneName)
    {
        foreach (var item in sceneBGMList)
        {
            if (item.sceneName == sceneName)
            {
                return item.bgmClip;
            }
        }
        return defaultBGM; // ถ้าไม่เจอให้คืนค่า default
    }
}