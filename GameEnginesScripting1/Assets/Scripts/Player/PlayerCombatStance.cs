using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 互斥架势：键盘 1 空手（出拳）、键盘 2 持剑。早于 <see cref="PlayerUnarmedPunch"/> / <see cref="PlayerSwordMode"/> 执行，便于同一帧先定架势。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public class PlayerCombatStance : MonoBehaviour
{
#if ENABLE_INPUT_SYSTEM
    [SerializeField] Key unarmedStanceKey = Key.Digit1;
    [SerializeField] Key swordStanceKey = Key.Digit2;
#else
    [SerializeField] KeyCode unarmedStanceKey = KeyCode.Alpha1;
    [SerializeField] KeyCode swordStanceKey = KeyCode.Alpha2;
#endif

    PlayerUnarmedPunch _punch;
    PlayerSwordMode _sword;

    void Awake()
    {
        _punch = GetComponent<PlayerUnarmedPunch>();
        _sword = GetComponent<PlayerSwordMode>();
    }

    void Start()
    {
        ApplyUnarmedOnly();
    }

    void Update()
    {
        if (WasUnarmedStancePressed())
            ApplyUnarmedOnly();
        if (WasSwordStancePressed())
            ApplySwordOnly();
    }

    void ApplyUnarmedOnly()
    {
        if (_sword != null)
            _sword.SetSwordStance(false);
        if (_punch != null)
            _punch.SetUnarmedStanceActive(true);
    }

    void ApplySwordOnly()
    {
        if (_punch != null)
            _punch.SetUnarmedStanceActive(false);
        if (_sword != null)
            _sword.SetSwordStance(true);
    }

    bool WasUnarmedStancePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[unarmedStanceKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(unarmedStanceKey);
#endif
    }

    bool WasSwordStancePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[swordStanceKey].wasPressedThisFrame;
#else
        return Input.GetKeyDown(swordStanceKey);
#endif
    }
}
