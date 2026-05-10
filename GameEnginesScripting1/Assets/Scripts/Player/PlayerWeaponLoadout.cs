using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 数字键 1 = 空手（武器隐藏），数字键 2 = 持枪（武器显示于右手，枪口对齐世界向下）。
/// Holster 字段保留以备将来「背枪可见」；当前空手模式不显示背后武器。
/// </summary>
[DisallowMultipleComponent]
public class PlayerWeaponLoadout : MonoBehaviour
{
    public enum WeaponSlot
    {
        UnarmedHolstered = 1,
        GunEquipped = 2
    }

    [SerializeField] GameObject weaponPrefab;
    [SerializeField] Transform holsterBoneOverride;
    [SerializeField] Transform handBoneOverride;

    [Header("Holster (unused while unarmed hides weapon; kept for future holster display)")]
    [Tooltip("当前逻辑：按 1 时武器隐藏，不挂背部。这些值保留供以后扩展背枪显示。")]
    [SerializeField] Vector3 holsterLocalPosition = new Vector3(0.06f, 0.1f, -0.16f);
    [SerializeField] Vector3 holsterLocalEulerAngles = new Vector3(0f, 190f, 95f);

    [Header("Grip fine-tune (Right hand local space, applied before barrel align)")]
    [SerializeField] Vector3 handLocalPosition = Vector3.zero;
    [SerializeField] Vector3 handLocalEulerAngles = Vector3.zero;

    [Header("Weapon model definition")]
    [Tooltip("枪口指向（武器本地空间单位向量）。程序化枪默认与几何一致：沿 -Y 朝下。")]
    [SerializeField] Vector3 barrelDirectionLocal = new Vector3(0f, -1f, 0f);
    [Tooltip("相对武器根的握把参考点；枪口对齐绕该点旋转，使掌心贴合骨骼原点时再用位移微调。")]
    [SerializeField] Vector3 gripLocalOffset = Vector3.zero;

    [Tooltip("沿 barrelDirectionLocal（握把→枪口）从握把参考点到枪口发射点的距离。")]
    [SerializeField] float muzzleDistanceFromGrip = 0.3f;

    [Header("Optional animator (add bool \"HoldingGun\" in controller for poses)")]
    [SerializeField] string holdingGunParameter = "HoldingGun";

    WeaponSlot _activeSlot = WeaponSlot.GunEquipped;
    Transform _weapon;

    public bool IsGunEquipped => _activeSlot == WeaponSlot.GunEquipped && _weapon != null && _weapon.gameObject.activeSelf;

    public Transform WeaponTransform => _weapon;

    /// <summary>枪口世界坐标（沿枪管从握把点伸出）。未持枪或未激活时返回 false。</summary>
    public bool TryGetMuzzleWorldPosition(out Vector3 origin)
    {
        origin = default;
        if (_weapon == null || !_weapon.gameObject.activeSelf || _activeSlot != WeaponSlot.GunEquipped)
            return false;

        Vector3 axis = barrelDirectionLocal.sqrMagnitude > 1e-6f
            ? barrelDirectionLocal.normalized
            : Vector3.down;
        Vector3 localMuzzle = gripLocalOffset + axis * muzzleDistanceFromGrip;
        origin = _weapon.TransformPoint(localMuzzle);
        return true;
    }

    Transform _holsterBone;
    Transform _handBone;
    Animator _animator;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        ResolveBones();
        EnsureWeaponInstance();
        ApplySlot(_activeSlot, force: true);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame)
                ApplySlot(WeaponSlot.UnarmedHolstered);
            else if (kb.digit2Key.wasPressedThisFrame)
                ApplySlot(WeaponSlot.GunEquipped);
            return;
        }
#endif
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ApplySlot(WeaponSlot.UnarmedHolstered);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            ApplySlot(WeaponSlot.GunEquipped);
    }

    void ResolveBones()
    {
        if (holsterBoneOverride != null)
            _holsterBone = holsterBoneOverride;
        if (handBoneOverride != null)
            _handBone = handBoneOverride;

        if (_animator != null && _animator.isHuman)
        {
            if (_holsterBone == null)
                _holsterBone = _animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_handBone == null)
                _handBone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (_holsterBone == null)
            _holsterBone = FindChildRecursive(transform, "Chest");
        if (_handBone == null)
            _handBone = FindChildRecursive(transform, "Right_Hand");
    }

    void EnsureWeaponInstance()
    {
        if (_weapon != null)
            return;

        if (weaponPrefab != null)
        {
            _weapon = Instantiate(weaponPrefab, transform).transform;
            _weapon.name = "WeaponVisual";
        }
        else
        {
            _weapon = BuildProceduralGun().transform;
            _weapon.name = "WeaponVisual";
        }

        foreach (var c in _weapon.GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    /// <summary>
    /// 武器根 = 握把参考点；枪管沿本地 -Y 延伸（枪口朝向本地 -Y）。
    /// </summary>
    static GameObject BuildProceduralGun()
    {
        var root = new GameObject("ProceduralGun");

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        body.transform.localScale = new Vector3(0.06f, 0.1f, 0.07f);

        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Barrel";
        barrel.transform.SetParent(root.transform, false);
        barrel.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        barrel.transform.localRotation = Quaternion.identity;
        barrel.transform.localScale = new Vector3(0.045f, 0.16f, 0.045f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.15f, 0.15f, 0.16f);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            r.sharedMaterial = mat;

        return root;
    }

    void ApplySlot(WeaponSlot slot, bool force = false)
    {
        if (!force && slot == _activeSlot)
            return;

        _activeSlot = slot;
        EnsureWeaponInstance();

        if (slot == WeaponSlot.UnarmedHolstered)
        {
            _weapon.gameObject.SetActive(false);
            _weapon.SetParent(transform, false);
            SetAnimatorHolding(false);
            return;
        }

        _weapon.gameObject.SetActive(true);

        Transform parent = _handBone != null ? _handBone : transform;
        _weapon.SetParent(parent, false);
        _weapon.localPosition = handLocalPosition;
        _weapon.localRotation = Quaternion.Euler(handLocalEulerAngles);

        AlignGunBarrelToWorldDown();

        SetAnimatorHolding(true);
    }

    void AlignGunBarrelToWorldDown()
    {
        Vector3 axis = barrelDirectionLocal.sqrMagnitude > 1e-6f
            ? barrelDirectionLocal.normalized
            : Vector3.down;

        Vector3 gripWorld = _weapon.TransformPoint(gripLocalOffset);
        Vector3 barrelWorld = _weapon.TransformDirection(axis);
        if (barrelWorld.sqrMagnitude < 1e-8f)
            return;

        Quaternion q = Quaternion.FromToRotation(barrelWorld, Vector3.down);
        _weapon.rotation = q * _weapon.rotation;
        _weapon.position = gripWorld + q * (_weapon.position - gripWorld);
    }

    void SetAnimatorHolding(bool holding)
    {
        if (_animator == null || string.IsNullOrEmpty(holdingGunParameter))
            return;

        foreach (var p in _animator.parameters)
        {
            if (p.name != holdingGunParameter || p.type != AnimatorControllerParameterType.Bool)
                continue;
            _animator.SetBool(holdingGunParameter, holding);
            return;
        }
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindChildRecursive(root.GetChild(i), name);
            if (f != null)
                return f;
        }

        return null;
    }
}
