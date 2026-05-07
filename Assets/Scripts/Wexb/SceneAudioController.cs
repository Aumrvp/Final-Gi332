using UnityEngine;

public class SceneAudioController : MonoBehaviour
{
    [Header("Scene Audio Settings")]
    [Tooltip("ใส่ไฟล์ AudioData ของเพลงประกอบฉากนี้")]
    [SerializeField] private AudioData sceneBGM;

    private void Start()
    {
        // พอเริ่ม Scene ปุ๊บ สั่ง AudioManager เล่นเพลงเลย
        if (sceneBGM != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(sceneBGM);
        }
    }
}