using UnityEngine;
using UnityEngine.InputSystem;

public class SpacebarSmashInput : MonoBehaviour
{
    [SerializeField] private Key smashKey = Key.Space;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[smashKey].wasPressedThisFrame)
            SmashUIManager.Instance?.OnPressSmashButton();
    }
}
