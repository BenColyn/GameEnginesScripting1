using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 持枪状态下鼠标左键射击；弹匣 12 发，备弹最多 48（合计携带上限 60）。R 换弹。
/// </summary>
[DisallowMultipleComponent]
public class PlayerWeaponAttack : MonoBehaviour
{
    [SerializeField] PlayerWeaponLoadout loadout;
    [SerializeField] GameObject bulletPrefab;

    [Header("Ammo — classic display mag 12 / pool 60")]
    [SerializeField] int magazineCapacity = 12;
    [SerializeField] int maxReserveAmmo = 48;

    [Header("Fire")]
    [SerializeField] float fireCooldown = 0.1f;

    [Header("Aim")]
    [Tooltip("若为空则使用 Camera.main")]
    [SerializeField] Camera aimCamera;

    int _ammoInMagazine;
    int _reserveAmmo;
    float _nextFireTime;

    public int AmmoInMagazine => _ammoInMagazine;
    public int ReserveAmmo => _reserveAmmo;
    public int MagazineCapacity => magazineCapacity;
    public int MaxCarriedAmmo => magazineCapacity + maxReserveAmmo;

    void Awake()
    {
        if (loadout == null)
            loadout = GetComponent<PlayerWeaponLoadout>();
        if (aimCamera == null)
            aimCamera = Camera.main;
    }

    void Start()
    {
        _ammoInMagazine = magazineCapacity;
        _reserveAmmo = maxReserveAmmo;
    }

    void Update()
    {
        if (loadout == null || bulletPrefab == null)
            return;

        if (TryReloadInput())
            TryReload();

        if (!loadout.IsGunEquipped)
            return;

        if (!IsFireHeld() || Time.time < _nextFireTime)
            return;

        if (_ammoInMagazine <= 0)
            return;

        if (!loadout.TryGetMuzzleWorldPosition(out Vector3 origin))
            return;

        if (aimCamera == null)
            aimCamera = Camera.main;
        Vector3 dir = aimCamera != null ? aimCamera.transform.forward : transform.forward;

        Quaternion rot = Quaternion.identity;
        if (dir.sqrMagnitude > 1e-6f)
            rot = Quaternion.LookRotation(dir);

        var bolt = Instantiate(bulletPrefab, origin, rot);
        var fly = bolt.GetComponent<PlayerProjectileFly>();
        if (fly != null)
            fly.Launch(transform.root, dir);

        _ammoInMagazine--;
        _nextFireTime = Time.time + fireCooldown;
    }

    static bool IsFireHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.leftButton.isPressed;
#endif
        return Input.GetMouseButton(0);
    }

    bool TryReloadInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.R);
    }

    void TryReload()
    {
        if (_reserveAmmo <= 0 || _ammoInMagazine >= magazineCapacity)
            return;

        int need = magazineCapacity - _ammoInMagazine;
        int load = Mathf.Min(need, _reserveAmmo);
        _ammoInMagazine += load;
        _reserveAmmo -= load;
    }
}
