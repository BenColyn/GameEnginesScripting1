using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
    [SerializeField] Vector3 swordLocalEulerAngles;
    [Tooltip("After spawn, set sword subtree layers to match this character root (helps cameras that only render the character layer).")]
    [SerializeField] bool syncSwordLayerWithCharacterRoot = true;
    [Tooltip("Log once on first successful spawn: hand path, renderer count, sword root name.")]
    [SerializeField] bool logSwordSpawnDiagnostics;

    [Header("Greatsword swing (Animator trigger on SwordHold layer)")]
    [Tooltip("持剑时鼠标左键触发 HumanM@Attack2H01（StarterAssetsThirdPerson 中 SwordAttack / SwordAttack2H）。")]
    [SerializeField] bool enableGreatswordSwing = true;
    [SerializeField] string swordAttackTriggerParameter = "SwordAttack";
    [SerializeField] float minSwingInterval = 0.55f;

    [Header("Enemy melee (same as unarmed punch)")]
    [Tooltip("持剑左键时沿用 PlayerUnarmedPunch 的 SphereCast，对带 EnemyDie / IDamageable 的目标与空手一致。")]
    [SerializeField] bool applyMeleeEnemyHitOnPrimaryAttack = true;

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

    PlayerUnarmedPunch _punch;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _punch = GetComponent<PlayerUnarmedPunch>();
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
        if (applyMeleeEnemyHitOnPrimaryAttack && _punch != null)
            _punch.TryMeleeHitFromView();
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
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            return true;
        return false;
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
    }

    void OnDestroy()
    {
        RemoveSwordVisual();
    }
}
