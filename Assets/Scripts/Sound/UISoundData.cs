using UnityEngine;

// สร้างเมนูคลิกขวาใน Project Window เพื่อสร้างไฟล์ Data นี้ได้เลย
[CreateAssetMenu(fileName = "UISoundData", menuName = "Audio/UISound Data")]
public class UISoundData : ScriptableObject
{
    [Header("UI Sound Effects")]
    public AudioClip buttonClick;
    public AudioClip buttonHover;
    public AudioClip errorClick;

    [Range(0f, 1f)]
    public float defaultVolume = 1f;
}