using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 剑身水平对齐参考：角色前向（随转身变化）或固定世界 +Z（Unity 蓝轴在水平面投影）。
/// </summary>
public enum SwordBladeAlignReference
{
    CharacterHorizontalForward = 0,
    WorldHorizontalForwardZ = 1,
}

/// <summary>
/// 刀剑持剑状态：由 <see cref="PlayerCombatStance"/> 驱动；开启时在指定手部骨骼下实例化剑并驱动 SwordHold 层与 SwordMode 参数。
/// 挂接在 LateUpdate 完成，避免与 Humanoid 骨骼写入顺序冲突；手部优先 Inspector 挂点，其次 Humanoid 骨骼，再按层级名称（如 Right_Hand）查找。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10001)]
public class PlayerSwordMode : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] string swordHoldLayerName = "SwordHold";
    [SerializeField] string swordModeParameter = "SwordMode";
    [SerializeField] string brawlerLayerName = "BrawlerPunch";
    [SerializeField] string hookLeftTrigger = "BrawlerHookLeft";
    [SerializeField] string hookRightTrigger = "BrawlerHookRight";

    [Header("Sword")]
    [Tooltip("Must be a GameObject source (e.g. Human_Sword.fbx model root). Prefabs whose YAML root is only PrefabInstance (variant wrapper) will break Instantiate.")]
    [SerializeField] GameObject swordPrefab;
    [Tooltip("Optional empty transform parented to the mesh hand; most reliable attach point.")]
    [SerializeField] Transform gripAttachOverride;
    [Tooltip("If set, search under this character for a child transform with this exact name when Humanoid bone lookup is unavailable or implausible.")]
    [SerializeField] string hierarchyHandBoneName = "Right_Hand";
    [Tooltip("Fallback only when Humanoid RightHand and hierarchy name both fail. Prefab YAML ints can mismatch Unity versions.")]
    [SerializeField] HumanBodyBones handBone = HumanBodyBones.RightHand;
    [SerializeField] Vector3 swordLocalPosition;
    [Tooltip("相对右手骨骼的本地欧拉角。开启 alignBladeWithCharacterForward 时先应用此值再对齐到水平前向。")]
    [SerializeField] Vector3 swordLocalEulerAngles = new Vector3(0f, 90f, 0f);
    [Tooltip("将剑身长轴对齐到水平参考方向；关闭则仅使用 swordLocalEulerAngles。")]
    [SerializeField] bool alignBladeWithCharacterForward;
    [Tooltip("水平对齐使用的参考：角色前向，或固定世界 +Z（Scene 视图蓝轴、Vector3.forward）。")]
    [SerializeField] SwordBladeAlignReference bladeAlignReference = SwordBladeAlignReference.CharacterHorizontalForward;
    [Tooltip("剑根物体上表示剑身长度方向的局部向量（未归一化也可），常见为 (1,0,0) 或 (0,0,1)，视 FBX 根轴向而定。")]
    [SerializeField] Vector3 bladeLocalAxis = new Vector3(1f, 0f, 0f);
    [Tooltip("首次生成剑时，从子级 MeshFilter / SkinnedMeshRenderer 的 mesh.bounds 中选取最长轴作为剑身方向（写入 sword 根局部空间），避免与具体 FBX 轴向不一致。")]
    [SerializeField] bool resolveBladeAxisFromMesh = true;
    [Tooltip("After spawn, set sword subtree layers to match this character root (helps cameras that only render the character layer).")]
    [SerializeField] bool syncSwordLayerWithCharacterRoot = true;
    [Tooltip("Log once on first successful spawn: hand path, renderer count, sword root name.")]
    [SerializeField] bool logSwordSpawnDiagnostics;

    [Header("Greatsword swing (Animator trigger on SwordHold layer)")]
    [Tooltip("持剑时鼠标左键触发 HumanM@Attack2H01（StarterAssetsThirdPerson 中 SwordAttack / SwordAttack2H）。")]
    [SerializeField] bool enableGreatswordSwing = true;
    [SerializeField] string swordAttackTriggerParameter = "SwordAttack";
    [SerializeField] float minSwingInterval = 0.55f;

    Animator _animator;
    int _swordLayer = -1;
    int _brawlerLayer = -1;
    int _swordModeHash;
    int _swordAttackHash;
    float _nextSwingAllowedTime;
    bool _swordModeActive;
    GameObject _swordInstance;
    bool _warnedMissingSwordLayer;
    bool _warnedMissingHand;
    bool _warnedNullSwordPrefab;
    bool _warnedSpawnResolveFailed;
    bool _loggedSpawnDiagnosticsOnce;
    bool _bladeLocalAxisFromMeshReady;
    Vector3 _bladeLocalAxisResolved = Vector3.right;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _swordModeHash = Animator.StringToHash(swordModeParameter);
        _swordAttackHash = Animator.StringToHash(swordAttackTriggerParameter);
        ResolveLayers();
        SetSwordStance(false);
    }

    void OnEnable()
    {
        ResolveLayers();
    }

    void ResolveLayers()
    {
        if (_animator == null)
            return;
        if (!string.IsNullOrEmpty(swordHoldLayerName))
            _swordLayer = _animator.GetLayerIndex(swordHoldLayerName);
        if (!string.IsNullOrEmpty(brawlerLayerName))
            _brawlerLayer = _animator.GetLayerIndex(brawlerLayerName);
    }

    /// <summary>由架势协调器调用：true 持剑，false 收起。</summary>
    public void SetSwordStance(bool active)
    {
        SetSwordModeActive(active);
    }

    void Update()
    {
        if (!enableGreatswordSwing || !_swordModeActive || _animator == null)
            return;
        if (Time.time < _nextSwingAllowedTime)
            return;
        if (!WasPrimaryAttackPressedThisFrame())
            return;
        _animator.SetTrigger(_swordAttackHash);
        _nextSwingAllowedTime = Time.time + minSwingInterval;
    }

    void LateUpdate()
    {
        if (_animator == null)
            return;

        if (_swordModeActive && _swordLayer >= 0)
            _animator.SetLayerWeight(_swordLayer, 1f);

        if (_swordModeActive && _brawlerLayer >= 0)
            _animator.SetLayerWeight(_brawlerLayer, 0f);

        if (_swordModeActive)
            TryAttachOrRefreshSwordInLateUpdate();
    }

    void SetSwordModeActive(bool active)
    {
        _swordModeActive = active;

        if (_animator != null)
        {
            _animator.SetBool(_swordModeHash, active);

            if (_swordLayer < 0 && !_warnedMissingSwordLayer)
            {
                _warnedMissingSwordLayer = true;
                var c = _animator.runtimeAnimatorController;
                string cname = c != null ? c.name : "null";
                Debug.LogWarning(
                    $"[PlayerSwordMode] No Animator layer named '{swordHoldLayerName}' (GetLayerIndex=-1). Controller='{cname}'. " +
                    "Add SwordHold layer to StarterAssetsThirdPerson (or matching) controller.",
                    this);
            }

            if (_swordLayer >= 0)
                _animator.SetLayerWeight(_swordLayer, active ? 1f : 0f);

            if (_brawlerLayer >= 0)
            {
                if (active)
                {
                    _animator.SetLayerWeight(_brawlerLayer, 0f);
                    if (!string.IsNullOrEmpty(hookLeftTrigger))
                        _animator.ResetTrigger(hookLeftTrigger);
                    if (!string.IsNullOrEmpty(hookRightTrigger))
                        _animator.ResetTrigger(hookRightTrigger);
                }
                else
                    _animator.SetLayerWeight(_brawlerLayer, 0f);
            }

            if (!active && !string.IsNullOrEmpty(swordAttackTriggerParameter))
                _animator.ResetTrigger(_swordAttackHash);
        }

        if (!active)
            RemoveSwordVisual();
    }

    void TryAttachOrRefreshSwordInLateUpdate()
    {
        if (!_swordModeActive || _animator == null)
            return;

        if (swordPrefab == null)
        {
            if (!_warnedNullSwordPrefab)
            {
                _warnedNullSwordPrefab = true;
                Debug.LogWarning("[PlayerSwordMode] swordPrefab is not assigned.", this);
            }
            return;
        }

        Transform hand = ResolveHandTransform();
        if (hand == null)
        {
            if (!_warnedMissingHand)
            {
                _warnedMissingHand = true;
                Debug.LogWarning(
                    "[PlayerSwordMode] No hand transform (override, hierarchy name, or Humanoid bone). Check gripAttachOverride / hierarchyHandBoneName / avatar.",
                    this);
            }
            return;
        }

        if (_swordInstance == null)
        {
            _swordInstance = Instantiate(swordPrefab, hand, false);
            if (_swordInstance == null)
            {
                if (!_warnedSpawnResolveFailed)
                {
                    _warnedSpawnResolveFailed = true;
                    Debug.LogError(
                        "[PlayerSwordMode] Instantiate returned null. Re-assign swordPrefab in the Inspector (e.g. Human_Sword.fbx root).",
                        this);
                }
                return;
            }

            _warnedSpawnResolveFailed = false;
            _bladeLocalAxisFromMeshReady = false;
            EnsureSwordRenderersEnabled(_swordInstance.transform);
            if (syncSwordLayerWithCharacterRoot)
                ApplyLayerRecursive(_swordInstance.transform, gameObject.layer);

            if (logSwordSpawnDiagnostics && !_loggedSpawnDiagnosticsOnce)
            {
                _loggedSpawnDiagnosticsOnce = true;
                int rendererCount = _swordInstance.GetComponentsInChildren<Renderer>(true).Length;
                Debug.Log(
                    $"[PlayerSwordMode] Spawned sword '{_swordInstance.name}' parentHand='{GetTransformPath(hand)}' rendererCount={rendererCount}",
                    this);
            }
        }

        if (_swordInstance.transform.parent != hand)
            _swordInstance.transform.SetParent(hand, false);

        _swordInstance.transform.localPosition = swordLocalPosition;
        _swordInstance.transform.localRotation = Quaternion.Euler(swordLocalEulerAngles);

        if (resolveBladeAxisFromMesh && !_bladeLocalAxisFromMeshReady)
        {
            if (!TryResolveBladeLocalAxisFromMeshes(_swordInstance.transform, out _bladeLocalAxisResolved))
                _bladeLocalAxisResolved = bladeLocalAxis.sqrMagnitude > 1e-10f ? bladeLocalAxis.normalized : Vector3.right;
            _bladeLocalAxisFromMeshReady = true;
        }

        if (alignBladeWithCharacterForward)
            AlignBladeAlongHorizontalReference();
    }

    void AlignBladeAlongHorizontalReference()
    {
        if (_swordInstance == null)
            return;

        Vector3 raw = bladeAlignReference == SwordBladeAlignReference.WorldHorizontalForwardZ
            ? Vector3.forward
            : transform.forward;
        Vector3 desired = Vector3.ProjectOnPlane(raw, Vector3.up);
        if (desired.sqrMagnitude < 1e-8f)
            desired = raw;
        desired.Normalize();

        Vector3 bladeLocal = GetBladeDirectionInSwordLocal();
        Vector3 bladeWorld = _swordInstance.transform.TransformDirection(bladeLocal);
        if (bladeWorld.sqrMagnitude < 1e-10f)
            return;
        bladeWorld.Normalize();

        if (Vector3.Angle(bladeWorld, desired) < 0.1f)
            return;

        Quaternion delta = Quaternion.FromToRotation(bladeWorld, desired);
        _swordInstance.transform.rotation = delta * _swordInstance.transform.rotation;
    }

    Vector3 GetBladeDirectionInSwordLocal()
    {
        if (resolveBladeAxisFromMesh && _bladeLocalAxisFromMeshReady)
            return _bladeLocalAxisResolved;
        return bladeLocalAxis.sqrMagnitude > 1e-10f ? bladeLocalAxis.normalized : Vector3.right;
    }

    /// <summary>
    /// 在 sword 根局部空间中给出剑身长度方向：取子网格 AABB 最长轴（世界方向再映射回 sword 根局部），与 swordLocalEulerAngles 无关。
    /// </summary>
    static bool TryResolveBladeLocalAxisFromMeshes(Transform swordRoot, out Vector3 bladeLocalInSwordRoot)
    {
        bladeLocalInSwordRoot = Vector3.right;
        if (swordRoot == null)
            return false;

        float bestMajor = 0f;
        Vector3 bestInSwordLocal = Vector3.right;

        void Consider(Transform meshOwner, Bounds localBounds)
        {
            Vector3 ext = localBounds.extents;
            float major = Mathf.Max(ext.x, ext.y, ext.z);
            if (major < 1e-6f || major < bestMajor)
                return;

            bestMajor = major;
            Vector3 axisInMeshOwnerLocal;
            if (ext.x >= ext.y && ext.x >= ext.z)
                axisInMeshOwnerLocal = Vector3.right;
            else if (ext.y >= ext.z)
                axisInMeshOwnerLocal = Vector3.up;
            else
                axisInMeshOwnerLocal = Vector3.forward;

            Vector3 worldDir = meshOwner.TransformDirection(axisInMeshOwnerLocal);
            bestInSwordLocal = swordRoot.InverseTransformDirection(worldDir).normalized;
        }

        var meshFilters = swordRoot.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;
            Consider(mf.transform, mf.sharedMesh.bounds);
        }

        var skinned = swordRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
        {
            SkinnedMeshRenderer smr = skinned[i];
            if (smr == null || smr.sharedMesh == null)
                continue;
            Consider(smr.transform, smr.sharedMesh.bounds);
        }

        if (bestMajor < 1e-6f || bestInSwordLocal.sqrMagnitude < 1e-10f)
            return false;

        bladeLocalInSwordRoot = bestInSwordLocal;
        return true;
    }

    Transform ResolveHandTransform()
    {
        if (gripAttachOverride != null)
            return gripAttachOverride;

        Transform root = transform;

        if (_animator != null && _animator.isHuman)
        {
            Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null && IsLikelyHandAnchor(root, rightHand))
                return rightHand;
        }

        if (!string.IsNullOrEmpty(hierarchyHandBoneName))
        {
            Transform byName = FindChildRecursive(root, hierarchyHandBoneName);
            if (byName != null && IsLikelyHandAnchor(root, byName))
                return byName;
        }

        if (_animator != null && _animator.isHuman)
        {
            Transform rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
                return rightHand;
        }

        if (!string.IsNullOrEmpty(hierarchyHandBoneName))
        {
            Transform byName = FindChildRecursive(root, hierarchyHandBoneName);
            if (byName != null)
                return byName;
        }

        return _animator != null ? _animator.GetBoneTransform(handBone) : null;
    }

    bool WasPrimaryAttackPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    /// <summary>
    /// Rejects foot/leg bones when serialized HumanBodyBones int drifts across Unity versions (wrong GetBoneTransform).
    /// </summary>
    static bool IsLikelyHandAnchor(Transform characterRoot, Transform candidate)
    {
        if (characterRoot == null || candidate == null)
            return false;
        Vector3 delta = candidate.position - characterRoot.position;
        float height = Vector3.Dot(delta, characterRoot.up);
        float horizontal = Vector3.Magnitude(delta - characterRoot.up * height);
        return height >= 0.18f && height <= 2.4f && horizontal <= 1.35f;
    }

    static void EnsureSwordRenderersEnabled(Transform swordRoot)
    {
        var renderers = swordRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = true;
    }

    static void ApplyLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            ApplyLayerRecursive(root.GetChild(i), layer);
    }

    static string GetTransformPath(Transform t)
    {
        if (t == null)
            return "";
        if (t.parent == null)
            return t.name;
        return GetTransformPath(t.parent) + "/" + t.name;
    }

    static Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    void RemoveSwordVisual()
    {
        if (_swordInstance == null)
            return;
        Destroy(_swordInstance);
        _swordInstance = null;
        _loggedSpawnDiagnosticsOnce = false;
        _bladeLocalAxisFromMeshReady = false;
    }

    void OnDestroy()
    {
        RemoveSwordVisual();
    }
}
