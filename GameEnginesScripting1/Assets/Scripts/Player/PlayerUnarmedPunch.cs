using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 左键交替挥拳：肩–上臂–前臂–拳三层骨骼叠加旋转（LateUpdate，避开 Animator 覆盖）；可选 SphereCast。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1100)]
public class PlayerUnarmedPunch : MonoBehaviour
{
    public EnemyDie enemyDie;
    [SerializeField] float punchCooldown = 1f;
    [SerializeField] float punchDuration = 0.38f;

    [Header("Right arm punch offsets (local euler × swing curve)")]
    [SerializeField] Vector3 rightUpperArmPunchEuler = new Vector3(62f, -28f, 42f);
    [SerializeField] Vector3 rightLowerArmPunchEuler = new Vector3(48f, 8f, -12f);
    [SerializeField] Vector3 rightHandPunchEuler = new Vector3(22f, 14f, 10f);

    [Header("Left arm punch offsets")]
    [SerializeField] Vector3 leftUpperArmPunchEuler = new Vector3(-62f, 28f, -42f);
    [SerializeField] Vector3 leftLowerArmPunchEuler = new Vector3(-48f, -8f, 12f);
    [SerializeField] Vector3 leftHandPunchEuler = new Vector3(-22f, -14f, -10f);

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

    Transform _leftUpperArm;
    Transform _leftLowerArm;
    Transform _leftHand;
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

        Transform upper = _punchUseLeft ? _leftUpperArm : _rightUpperArm;
        Transform lower = _punchUseLeft ? _leftLowerArm : _rightLowerArm;
        Transform hand = _punchUseLeft ? _leftHand : _rightHand;

        if (upper == null || lower == null || hand == null)
        {
            _punching = false;
            return;
        }

        _punchElapsed += Time.deltaTime;
        float u = punchDuration > 1e-5f ? Mathf.Clamp01(_punchElapsed / punchDuration) : 1f;
        float curve = Mathf.Sin(u * Mathf.PI);

        Vector3 eu = _punchUseLeft ? leftUpperArmPunchEuler : rightUpperArmPunchEuler;
        Vector3 el = _punchUseLeft ? leftLowerArmPunchEuler : rightLowerArmPunchEuler;
        Vector3 eh = _punchUseLeft ? leftHandPunchEuler : rightHandPunchEuler;

        Quaternion bu = upper.localRotation;
        Quaternion bl = lower.localRotation;
        Quaternion bh = hand.localRotation;

        upper.localRotation = bu * Quaternion.Euler(eu * curve);
        lower.localRotation = bl * Quaternion.Euler(el * curve);
        hand.localRotation = bh * Quaternion.Euler(eh * curve);

        if (sphereCastOnPunch && !_hitSent && u >= hitPhase)
        {
            TryPunchHit();
            _hitSent = true;
        }

        if (u >= 1f)
            _punching = false;
    }

    bool HasCompleteArm(bool left)
    {
        if (left)
            return _leftUpperArm != null && _leftLowerArm != null && _leftHand != null;
        return _rightUpperArm != null && _rightLowerArm != null && _rightHand != null;
    }

    bool ArmChainFullyResolved()
    {
        return _leftUpperArm != null && _leftLowerArm != null && _leftHand != null
               && _rightUpperArm != null && _rightLowerArm != null && _rightHand != null;
    }

    void ResolveArmChain()
    {
        if (_animator != null && _animator.isHuman)
        {
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (_leftUpperArm == null)
            _leftUpperArm = FindChildRecursive(transform, "Left_UpperArm");
        if (_leftLowerArm == null)
            _leftLowerArm = FindChildRecursive(transform, "Left_LowerArm")
                            ?? FindChildRecursive(transform, "Left_Forearm");
        if (_leftHand == null)
            _leftHand = FindChildRecursive(transform, "Left_Hand")
                        ?? FindChildRecursive(transform, "L_Hand")
                        ?? FindChildRecursive(transform, "hand_l");

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
            {
                EnemyDie hitEnemy = hit.collider.GetComponent<EnemyDie>();
                if (hitEnemy != null)
                {
                    hitEnemy.Die();
                }
                else
                {
                    Debug.LogWarning("Getroffen, aber das Objekt hat kein EnemyDie-Skript!");
                }

                Debug.Log($"[Punch] Hit {hit.collider.name}", hit.collider);
             
            }
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
