using UnityEngine;

public class EnemyProjectileFly : MonoBehaviour
{
    [SerializeField] float bulletSpeed = 50.0f;
    private Vector3 direction;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        this.Move();
    }
    private void Move()
    {
        transform.Translate(this.direction * Time.deltaTime * bulletSpeed);
    }
    public void SetDirection(Vector3 inDirection)
    {
        direction = inDirection.normalized;
    }
}
