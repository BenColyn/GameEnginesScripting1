using UnityEngine;

public class EnemyProjectileFly : MonoBehaviour
{
    [SerializeField] float bulletSpeed = 50.0f;
    [SerializeField] float lifeTime = 1f;
    public GameObject target;
    private Vector3 direction;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Destroy(gameObject, bulletSpeed * lifeTime);
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
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target"))
        {
            // Wir suchen den WaveSpawner in der Szene und rufen die Methode auf
            Spawnen spawner = FindFirstObjectByType<Spawnen>();
            if (spawner != null)
            {
                spawner.ShowDeathScreen();
            }

        }
        Destroy(gameObject);
        
    }
}
