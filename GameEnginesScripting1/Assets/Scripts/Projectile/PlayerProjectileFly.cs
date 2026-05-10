using UnityEngine;

/// <summary>
/// 玩家子弹直线飞行；忽略发射者碰撞；击中物体后销毁。含轨迹与命中火花。
/// </summary>
public class PlayerProjectileFly : MonoBehaviour
{
    [SerializeField] float bulletSpeed = 85f;
    [SerializeField] float maxLifetime = 4f;
    [Tooltip("生成后短时间内不响应触发器，避免枪口贴地/贴身处立刻销毁。")]
    [SerializeField] float triggerImmuneSeconds = 0.08f;
    [SerializeField] bool addTrailIfMissing = true;

    Transform _shooterRoot;
    Vector3 _direction = Vector3.forward;
    float _triggerImmuneUntil;
    TrailRenderer _trail;

    void Awake()
    {
        if (addTrailIfMissing && GetComponent<TrailRenderer>() == null)
        {
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.12f;
            _trail.minVertexDistance = 0.01f;
            _trail.startWidth = 0.07f;
            _trail.endWidth = 0.01f;
            _trail.numCornerVertices = 2;
            _trail.numCapVertices = 1;
            _trail.material = WeaponShootEffects.GetTrailMaterial();
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = g;
        }
    }

    void Start()
    {
        Destroy(gameObject, maxLifetime);
    }

    void Update()
    {
        transform.position += _direction * (bulletSpeed * Time.deltaTime);
    }

    public void Launch(Transform shooterRoot, Vector3 worldDirection)
    {
        _shooterRoot = shooterRoot;
        _direction = worldDirection.sqrMagnitude > 1e-8f ? worldDirection.normalized : Vector3.forward;
        _triggerImmuneUntil = Time.time + triggerImmuneSeconds;
        if (_trail != null)
            _trail.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time < _triggerImmuneUntil)
            return;

        if (_shooterRoot != null && other.transform.root == _shooterRoot.root)
            return;

        Vector3 hitPos = other.ClosestPoint(transform.position);
        Vector3 n = (transform.position - hitPos);
        if (n.sqrMagnitude < 1e-6f)
            n = -_direction;
        else
            n.Normalize();

        WeaponShootEffects.PlayImpactSpark(hitPos, n);
        Destroy(gameObject);
    }
}
