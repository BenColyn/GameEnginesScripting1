using UnityEngine;

/// <summary>
/// 玩家子弹直线飞行；忽略发射者碰撞；击中物体后销毁。
/// </summary>
public class PlayerProjectileFly : MonoBehaviour
{
    [SerializeField] float bulletSpeed = 85f;
    [SerializeField] float maxLifetime = 4f;

    Transform _shooterRoot;
    Vector3 _direction;

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
    }

    void OnTriggerEnter(Collider other)
    {
        if (_shooterRoot != null && other.transform.root == _shooterRoot.root)
            return;
        Destroy(gameObject);
    }
}
