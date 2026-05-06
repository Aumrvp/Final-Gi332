using UnityEngine;
using UnityEngine.EventSystems;

// สคริปต์นี้ทำหน้าที่แค่ "รับรู้การคลิก" แล้วบอก Manager (Single Responsibility)
public class UIButtonSound : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    // เมื่อเมาส์คลิก (เทียบเท่า OnClick)
    public void OnPointerClick(PointerEventData eventData)
    {
        // เปลี่ยนมาเรียกใช้ GlobalAudioManager ตัวใหม่ของเรา
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.PlayClickSound();
        }
        else
        {
            Debug.LogWarning("[UIButtonSound] ไม่พบ GlobalAudioManager ใน Scene!");
        }
    }

    // เมื่อเอาเมาส์ไปชี้ (Hover)
    public void OnPointerEnter(PointerEventData eventData)
    {
        // หากในอนาคตคุณเพิ่ม PlayHoverSound() ใน GlobalAudioManager แล้ว ค่อยเอาคอมเมนต์ออกครับ
        /*
        if (GlobalAudioManager.Instance != null)
        {
            GlobalAudioManager.Instance.PlayHoverSound();
        }
        */
    }
}