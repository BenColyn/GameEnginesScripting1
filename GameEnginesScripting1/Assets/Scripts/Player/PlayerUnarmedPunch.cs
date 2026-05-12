using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 左键交替出拳：Brawler 上半身层左/右勾拳；两次出拳起点至少间隔 <see cref="minPunchInterval"/>（默认 0.8s）。
/// 多路径 CrossFade/Play + 可选诊断日志（对应「右勾拳不呈现」排查：第二层当前状态、Controller 名、HasState 兜底）。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class PlayerUnarmedPunch : MonoBehaviour
{
    [Tooltip("两次出拳开始时刻之间的最小间隔（秒）。")]
    [SerializeField] float minPunchInterval = 0.8f;

    [Tooltip("左勾拳层权重保持时长，应对齐 Atk_P_1 时长（约 19 帧@30fps）。")]
    [SerializeField] float punchDurationLeft = 0.68f;

    [Tooltip("右勾拳层权重保持时长，应对齐 Atk_P_2 时长（约 29 帧@30fps）。")]
    [SerializeField] float punchDurationRight = 1f;

    [Header("Animator — Brawler upper-body layer")]
    [SerializeField] string brawlerLayerName = "BrawlerPunch";
    [Tooltip("该层根状态机在 Controller 里的名字，通常与层名相同。全路径为 此字段.状态名。")]
    [SerializeField] string brawlerStateMachineRootName = "BrawlerPunch";
    [SerializeField] string stateHookLeft = "BrawlerHookLeft";
    [SerializeField] string stateHookRight = "BrawlerHookRight";
    [SerializeField] string hookLeftTrigger = "BrawlerHookLeft";
    [SerializeField] string hookRightTrigger = "BrawlerHookRight";
    [SerializeField] float punchCrossFade = 0.08f;

    [Header("Diagnostics (plan: verify runtime state / controller / HasState path)")]
    [Tooltip("开启后在 Console 输出：Controller 名、Brawler 层索引、每次出拳后下一帧第二层当前状态；并记录 TryPlayHook 使用的路径分支。")]
    [SerializeField] bool logPunchDiagnostics;

    [Header("Hit detect (optional)")]
    [SerializeField] bool sphereCastOnPunch = true;
    [SerializeField] LayerMask hitLayers = ~0;
    [SerializeField] float sphereRadius = 0.14f;
    [SerializeField] float sphereDistance = 1.1f;
    [SerializeField] float hitPhase = 0.45f;

    Animator _animator;
    int _brawlerLayer = -1;

    float _nextPunchAllowedTime;
    bool _nextIsLeft = true;

    bool _punching;
    float _punchElapsed;
    bool _currentPunchIsLeft = true;
    bool _hitSent;
    bool _queuedPunch;

    bool _warnedPlayFailed;
    bool _loggedControllerOnce;
    bool _warnedMissingBrawlerLayer;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        ResolveBrawlerLayer();
        LogControllerAndLayersOnce();
        WarnIfBrawlerLayerMissing();
    }

    void OnEnable()
    {
        ResolveBrawlerLayer();
        WarnIfBrawlerLayerMissing();
    }

    void ResolveBrawlerLayer()
    {
        if (_animator != null && !string.IsNullOrEmpty(brawlerLayerName))
            _brawlerLayer = _animator.GetLayerIndex(brawlerLayerName);
    }

    void LogControllerAndLayersOnce()
    {
        if (!logPunchDiagnostics || _loggedControllerOnce || _animator == null)
            return;
        _loggedControllerOnce = true;
        var c = _animator.runtimeAnimatorController;
        string cname = c != null ? c.name : "null";
        Debug.Log(
            $"[PlayerUnarmedPunch] Runtime AnimatorController name='{cname}', layerCount={_animator.layerCount}, " +
            $"GetLayerIndex('{brawlerLayerName}')={_brawlerLayer}. (Expected asset: StarterAssetsThirdPerson — confirm in Inspector.)",
            this);
    }

    void WarnIfBrawlerLayerMissing()
    {
        if (_warnedMissingBrawlerLayer || _animator == null || string.IsNullOrEmpty(brawlerLayerName))
            return;
        if (_brawlerLayer >= 0)
            return;
        _warnedMissingBrawlerLayer = true;
        var c = _animator.runtimeAnimatorController;
        string cname = c != null ? c.name : "null";
        Debug.LogWarning(
            $"[PlayerUnarmedPunch] No Animator layer named '{brawlerLayerName}' (GetLayerIndex=-1). Controller='{cname}'. " +
            "Hooks will not play. Assign StarterAssetsThirdPerson (or matching) controller with a BrawlerPunch layer.",
            this);
    }

    void Update()
    {
        if (_punching)
        {
            _punchElapsed += Time.deltaTime;
            float dur = _currentPunchIsLeft ? punchDurationLeft : punchDurationRight;
            float u = dur > 1e-5f ? Mathf.Clamp01(_punchElapsed / dur) : 1f;

            if (sphereCastOnPunch && !_hitSent && u >= hitPhase)
            {
                TryPunchHit();
                _hitSent = true;
            }

            if (u >= 1f)
                EndPunchWindow();
        }

        bool pressed = WasPunchPressed();
        if (pressed && (_punching || Time.time < _nextPunchAllowedTime))
            _queuedPunch = true;

        if (!_punching && Time.time >= _nextPunchAllowedTime && (pressed || _queuedPunch))
        {
            _queuedPunch = false;
            StartPunch();
        }
    }

    void LateUpdate()
    {
        if (!_punching || _animator == null || _brawlerLayer < 0)
            return;
        if (_animator.GetLayerWeight(_brawlerLayer) < 1f)
            _animator.SetLayerWeight(_brawlerLayer, 1f);
    }

    void StartPunch()
    {
        bool useLeft = _nextIsLeft;
        _nextIsLeft = !useLeft;
        _currentPunchIsLeft = useLeft;

        if (_animator != null && _brawlerLayer >= 0)
        {
            _animator.SetLayerWeight(_brawlerLayer, 1f);

            if (!string.IsNullOrEmpty(hookLeftTrigger))
                _animator.ResetTrigger(hookLeftTrigger);
            if (!string.IsNullOrEmpty(hookRightTrigger))
                _animator.ResetTrigger(hookRightTrigger);

            string shortName = useLeft ? stateHookLeft : stateHookRight;
            string root = string.IsNullOrEmpty(brawlerStateMachineRootName)
                ? brawlerLayerName
                : brawlerStateMachineRootName;
            string fullPath = $"{root}.{shortName}";

            string branchNote = "skipped";
            bool hookPlaybackOk = TryPlayHookOnLayer(fullPath, shortName, out branchNote);
            if (!hookPlaybackOk)
            {
                if (!_warnedPlayFailed)
                {
                    _warnedPlayFailed = true;
                    Debug.LogWarning(
                        $"[PlayerUnarmedPunch] HasState was false for known hashes; Play fallbacks did not match '{shortName}' after Update(0). " +
                        $"Last branch note: {branchNote}. Layer={_brawlerLayer} '{brawlerLayerName}'. Enable logPunchDiagnostics for details.",
                        this);
                }
            }

            if (logPunchDiagnostics)
                StartCoroutine(LogBrawlerLayerStateNextFrame(useLeft, shortName, branchNote));
        }

        _punching = true;
        _punchElapsed = 0f;
        _hitSent = false;
        _nextPunchAllowedTime = Time.time + minPunchInterval;
    }

    IEnumerator LogBrawlerLayerStateNextFrame(bool wantedLeft, string shortName, string branchNote)
    {
        yield return null;
        if (_animator == null || _brawlerLayer < 0)
            yield break;

        var info = _animator.GetCurrentAnimatorStateInfo(_brawlerLayer);
        bool isL = info.IsName(stateHookLeft);
        bool isR = info.IsName(stateHookRight);
        Debug.Log(
            $"[PlayerUnarmedPunch] Next-frame Brawler layer: wanted={(wantedLeft ? "Left" : "Right")} ({shortName}), " +
            $"TryPlay branch={branchNote}, layerWeight={_animator.GetLayerWeight(_brawlerLayer):F2}, " +
            $"IsName Left={isL} Right={isR}, shortNameHash={info.shortNameHash}, normTime={info.normalizedTime:F2}",
            this);
    }

    bool TryPlayHookOnLayer(string fullPath, string shortName, out string branchNote)
    {
        branchNote = "none";
        int layer = _brawlerLayer;

        string altLayerPath = string.IsNullOrEmpty(brawlerLayerName) ? null : $"{brawlerLayerName}.{shortName}";
        string[] orderedPaths = altLayerPath != null && altLayerPath != fullPath
            ? new[] { fullPath, altLayerPath, shortName }
            : new[] { fullPath, shortName };

        foreach (var path in orderedPaths)
        {
            if (string.IsNullOrEmpty(path))
                continue;
            int h = Animator.StringToHash(path);
            if (!_animator.HasState(layer, h))
                continue;
            _animator.CrossFadeInFixedTime(path, punchCrossFade, layer, 0f, 0f);
            branchNote = $"CrossFadeInFixedTime(string) path='{path}' (HasState)";
            Diag(branchNote);
            return true;
        }

        int fullHash = Animator.StringToHash(fullPath);
        int shortHash = Animator.StringToHash(shortName);
        if (_animator.HasState(layer, fullHash))
        {
            _animator.CrossFadeInFixedTime(fullHash, punchCrossFade, layer, 0f, 0f);
            branchNote = "CrossFadeInFixedTime(fullHash)";
            Diag(branchNote);
            return true;
        }

        if (_animator.HasState(layer, shortHash))
        {
            _animator.CrossFadeInFixedTime(shortHash, punchCrossFade, layer, 0f, 0f);
            branchNote = "CrossFadeInFixedTime(shortHash)";
            Diag(branchNote);
            return true;
        }

        branchNote = "HasState false for all hashes; trying Play fallbacks";
        Diag(branchNote);

        foreach (var path in orderedPaths)
        {
            if (string.IsNullOrEmpty(path))
                continue;
            _animator.Play(path, layer, 0f);
            _animator.Update(0f);
            var st = _animator.GetCurrentAnimatorStateInfo(layer);
            if (st.IsName(shortName))
            {
                branchNote = $"Play ok path='{path}'";
                Diag(branchNote);
                return true;
            }
        }

        foreach (var path in orderedPaths)
        {
            if (string.IsNullOrEmpty(path))
                continue;
            _animator.CrossFadeInFixedTime(path, punchCrossFade, layer, 0f, 0f);
            _animator.Update(0f);
            var st = _animator.GetCurrentAnimatorStateInfo(layer);
            if (st.IsName(shortName))
            {
                branchNote = $"CrossFade blind (no HasState) path='{path}'";
                Diag(branchNote);
                return true;
            }
        }

        branchNote = "Play + blind CrossFade: state after Update did not IsName(shortName)";
        Diag(branchNote);
        return false;
    }

    void Diag(string msg)
    {
        if (logPunchDiagnostics)
            Debug.Log($"[PlayerUnarmedPunch] TryPlayHook: {msg}", this);
    }

    void EndPunchWindow()
    {
        if (_animator != null && _brawlerLayer >= 0)
            _animator.SetLayerWeight(_brawlerLayer, 0f);
        _punching = false;
    }

    void TryPunchHit()
    {
        Camera cam = Camera.main;
        Vector3 origin = transform.position + Vector3.up * 1.15f;
        Vector3 dir = cam != null ? cam.transform.forward : transform.forward;
        dir.Normalize();

        if (Physics.SphereCast(origin, sphereRadius, dir, out RaycastHit hit, sphereDistance, hitLayers,
                QueryTriggerInteraction.Ignore))
        {
            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null)
                dmg.ApplyDamage(1);
            else
                Debug.Log($"[Punch] Hit {hit.collider.name}", hit.collider);
        }
    }

    static bool WasPunchPressed()
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
}

/// <summary>Optional hook for可破坏目标 / 敌人血量。</summary>
public interface IDamageable
{
    void ApplyDamage(int amount);
}
