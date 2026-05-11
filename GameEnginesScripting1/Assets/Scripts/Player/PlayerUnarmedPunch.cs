using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 左键交替挥拳：肩→上臂→前臂→手，沿角色水平前向的世界轴 AngleAxis 叠加（LateUpdate）；左右共用一套角度与同向摆臂（双拳均朝前）；可选 SphereCast。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1100)]
public class PlayerUnarmedPunch : MonoBehaviour
{
    [SerializeField] float punchCooldown = 1f;
    [SerializeField] float punchDuration = 0.38f;

    [Header("Forward swing — world axis from character horizontal forward")]
    [Tooltip("整体挥击方向反了时改为 -1。")]
    [SerializeField] float axisSignFlip = 1f;

    [Tooltip("各段绕侧向轴的最大转角（度），× Sin(π·进度)；左右臂共用幅度与同向摆臂。")]
    [SerializeField] float shoulderSwingDegrees = 22f;
    [SerializeField] float upperArmSwingDegrees = 38f;
    [SerializeField] float lowerArmSwingDegrees = 18f;
    [SerializeField] float handSwingDegrees = 10f;

    [Header("Hit detect (optional)")]
    [SerializeField] bool sphereCastOnPunch = true;
    [SerializeField] LayerMask hitLayers = ~0;
    [SerializeField] float sphereRadius = 0.14f;
    [SerializeField] float sphereDistance = 1.1f;
    [SerializeField] float hitPhase = 0.45f;

    [Header("Animator (optional)")]
    [SerializeField] string punchLeftTrigger = "";
    [SerializeField] string punchRightTrigger = "";

    Animator _animator;

    Transform _leftShoulder;
    Transform _leftUpperArm;
    Transform _leftLowerArm;
    Transform _leftHand;
    Transform _rightShoulder;
    Transform _rightUpperArm;
    Transform _rightLowerArm;
    Transform _rightHand;

    float _nextPunchTime;
    bool _nextIsLeft = true;

    bool _punching;
    float _punchElapsed;
    bool _punchUseLeft;
    bool _hitSent;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        ResolveArmChain();
    }

    void Start()
    {
        ResolveArmChain();
    }

    void Update()
    {
        if (!ArmChainFullyResolved() && Time.frameCount % 2 == 0)
            ResolveArmChain();

        if (_punching)
            return;

        if (Time.time < _nextPunchTime)
            return;

        if (!WasPunchPressed())
            return;

        bool preferLeft = _nextIsLeft;
        if (!HasCompleteArm(preferLeft))
            preferLeft = !preferLeft;

        if (!HasCompleteArm(preferLeft))
            return;

        bool useLeft = preferLeft;
        _nextIsLeft = !useLeft;

        TryAnimatorTrigger(useLeft);
        _punching = true;
        _punchElapsed = 0f;
        _punchUseLeft = useLeft;
        _hitSent = false;
        _nextPunchTime = Time.time + punchCooldown;
    }

    void LateUpdate()
    {
        if (!_punching)
            return;

        Transform shoulder = _punchUseLeft ? _leftShoulder : _rightShoulder;
        Transform upper = _punchUseLeft ? _leftUpperArm : _rightUpperArm;
        Transform lower = _punchUseLeft ? _leftLowerArm : _rightLowerArm;
        Transform hand = _punchUseLeft ? _leftHand : _rightHand;

        if (shoulder == null || upper == null || lower == null || hand == null)
        {
            _punching = false;
            return;
        }

        _punchElapsed += Time.deltaTime;
        float u = punchDuration > 1e-5f ? Mathf.Clamp01(_punchElapsed / punchDuration) : 1f;
        float curve = Mathf.Sin(u * Mathf.PI);

        Vector3 swingForward = GetHorizontalForward();
        Vector3 swingAxis = GetSwingAxis(swingForward);
        if (swingAxis.sqrMagnitude < 1e-8f)
            swingAxis = transform.right;

        // 左右骨骼在世界里镜像，对两侧用同一转角符号才能让双拳都朝角色前方摆；不再对右臂取反号。
        const float forwardSwingSign = -1f;
        float sign = forwardSwingSign * axisSignFlip;

        ApplyWorldSwing(shoulder, shoulderSwingDegrees * curve * sign, swingAxis);
        ApplyWorldSwing(upper, upperArmSwingDegrees * curve * sign, swingAxis);
        ApplyWorldSwing(lower, lowerArmSwingDegrees * curve * sign, swingAxis);
        ApplyWorldSwing(hand, handSwingDegrees * curve * sign, swingAxis);

        if (sphereCastOnPunch && !_hitSent && u >= hitPhase)
        {
            TryPunchHit();
            _hitSent = true;
        }

        if (u >= 1f)
            _punching = false;
    }

    static void ApplyWorldSwing(Transform bone, float angleDegrees, Vector3 axis)
    {
        if (bone == null || Mathf.Abs(angleDegrees) < 1e-4f)
            return;
        Quaternion delta = Quaternion.AngleAxis(angleDegrees, axis.normalized);
        bone.rotation = delta * bone.rotation;
    }

    Vector3 GetHorizontalForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f)
            f = Vector3.forward;
        return f.normalized;
    }

    Vector3 GetSwingAxis(Vector3 horizontalForward)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, horizontalForward);
        if (axis.sqrMagnitude < 1e-8f)
            axis = transform.right;
        return axis.normalized;
    }

    bool HasCompleteArm(bool left)
    {
        if (left)
            return _leftShoulder != null && _leftUpperArm != null && _leftLowerArm != null && _leftHand != null;
        return _rightShoulder != null && _rightUpperArm != null && _rightLowerArm != null && _rightHand != null;
    }

    bool ArmChainFullyResolved()
    {
        return HasCompleteArm(true) && HasCompleteArm(false);
    }

    void ResolveArmChain()
    {
        if (_animator != null && _animator.isHuman)
        {
            _leftShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (_leftShoulder == null)
            _leftShoulder = FindChildRecursive(transform, "Left_Shoulder")
                            ?? FindChildRecursive(transform, "Left_Clavicle")
                            ?? FindChildRecursive(transform, "L_Clavicle");
        if (_leftUpperArm == null)
            _leftUpperArm = FindChildRecursive(transform, "Left_UpperArm");
        if (_leftLowerArm == null)
            _leftLowerArm = FindChildRecursive(transform, "Left_LowerArm")
                            ?? FindChildRecursive(transform, "Left_Forearm");
        if (_leftHand == null)
            _leftHand = FindChildRecursive(transform, "Left_Hand")
                        ?? FindChildRecursive(transform, "L_Hand")
                        ?? FindChildRecursive(transform, "hand_l");

        if (_rightShoulder == null)
            _rightShoulder = FindChildRecursive(transform, "Right_Shoulder")
                             ?? FindChildRecursive(transform, "Right_Clavicle")
                             ?? FindChildRecursive(transform, "R_Clavicle");
        if (_rightUpperArm == null)
            _rightUpperArm = FindChildRecursive(transform, "Right_UpperArm");
        if (_rightLowerArm == null)
            _rightLowerArm = FindChildRecursive(transform, "Right_LowerArm")
                             ?? FindChildRecursive(transform, "Right_Forearm");
        if (_rightHand == null)
            _rightHand = FindChildRecursive(transform, "Right_Hand")
                         ?? FindChildRecursive(transform, "R_Hand")
                         ?? FindChildRecursive(transform, "hand_r");
    }

    void TryAnimatorTrigger(bool left)
    {
        if (_animator == null)
            return;

        string name = left ? punchLeftTrigger : punchRightTrigger;
        if (string.IsNullOrEmpty(name))
            return;

        foreach (var p in _animator.parameters)
        {
            if (p.name != name || p.type != AnimatorControllerParameterType.Trigger)
                continue;
            _animator.SetTrigger(name);
            return;
        }
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

/// <summary>Optional hook for可破坏目标 / 敌人血量。</summary>
public interface IDamageable
{
    void ApplyDamage(int amount);
}
