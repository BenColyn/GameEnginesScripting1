using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    private float time;
    public float fireRate;
    public GameObject bulletPrefab;
    public GameObject spawnPoint;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        EnemyMovement enemyMovement = GetComponent<EnemyMovement>();
        bool inRange = enemyMovement.GetInRange();
        if (inRange == true)
        {
            time += Time.deltaTime;
            if (time > fireRate)
            {
                time = 0;
                SpawnBullet();
            }
        }
    }
    private void SpawnBullet()
    {
        GameObject bulletClone = Instantiate(bulletPrefab, spawnPoint.transform.position, Quaternion.identity);
        Vector3 dir = transform.forward;
        bulletClone.GetComponent<EnemyProjectileFly>().SetDirection(dir);
    }
}
