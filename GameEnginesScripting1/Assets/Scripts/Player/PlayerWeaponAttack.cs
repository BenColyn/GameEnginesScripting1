using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 持枪状态下鼠标左键射击；弹匣 12 发，备弹最多 48（合计携带上限 60）。R 换弹。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class PlayerWeaponAttack : MonoBehaviour
{
    [SerializeField] PlayerWeaponLoadout loadout;
    [SerializeField] GameObject bulletPrefab;

    [Header("Ammo — classic display mag 12 / pool 60")]
    [SerializeField] int magazineCapacity = 12;
    [SerializeField] int maxReserveAmmo = 48;

    [Header("Fire")]
    [SerializeField] float fireCooldown = 0.1f;
    [Tooltip("沿瞄准方向从发射点再伸出一段，避免子弹在地面/身内生成立刻被触发器销毁。")]
    [SerializeField] float spawnForwardOffset = 0.42f;

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
        if (aimCamera == null)
            aimCamera = Camera.main;

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

        if (aimCamera == null)
            aimCamera = Camera.main;
        Vector3 dir = aimCamera != null ? aimCamera.transform.forward : transform.forward;
        if (dir.sqrMagnitude < 1e-6f)
            dir = transform.forward;

        if (!TryResolveFireOrigin(dir, out Vector3 origin))
            return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized);

        var bolt = Instantiate(bulletPrefab, origin, rot);
        var fly = bolt.GetComponent<PlayerProjectileFly>();
        if (fly == null)
            fly = bolt.AddComponent<PlayerProjectileFly>();
        fly.Launch(transform.root, dir);

        WeaponShootEffects.PlayMuzzleFlash(origin, dir);

        _ammoInMagazine--;
        _nextFireTime = Time.time + fireCooldown;
    }

    bool TryResolveFireOrigin(Vector3 aimDir, out Vector3 origin)
    {
        origin = default;
        Vector3 dirN = aimDir.sqrMagnitude > 1e-8f ? aimDir.normalized : transform.forward;

        if (loadout.TryGetMuzzleWorldPosition(out origin))
        {
            origin += dirN * spawnForwardOffset;
            return true;
        }

        // 枪口计算失败时仍允许射击（例如骨骼未就绪）
        if (aimCamera != null)
        {
            Transform c = aimCamera.transform;
            origin = c.position + c.forward * 0.35f + c.up * -0.08f + c.right * 0.06f;
        }
        else
            origin = transform.position + Vector3.up * 1.45f + transform.forward * 0.4f;

        origin += dirN * spawnForwardOffset;
        return true;
    }

    static bool IsFireHeld()
    {
        // 兼容 Input System 与旧版 Input；避免仅一侧可用导致按住左键无反应
        if (Input.GetMouseButton(0))
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        if (UnityEngine.InputSystem.Pointer.current != null &&
            UnityEngine.InputSystem.Pointer.current.press.isPressed)
            return true;
#endif
        return false;
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
