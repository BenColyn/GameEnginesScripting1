using UnityEngine;
using UnityEngine.InputSystem.Controls;

public class EnemyAttack : MonoBehaviour
{
    [SerializeField] GameObject target;
    [Range(0f,35f)]
    [SerializeField] private float angle = 8.0f;
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
            
            if (target == null)
            {
                return;
            }
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
        Vector3 baseDir = (target.transform.position - transform.position).normalized * Time.deltaTime;
        Quaternion spreadRotation = Quaternion.Euler( Random.Range(-angle, angle), Random.Range(-angle, angle), 0);
        Vector3 finalDir = spreadRotation * baseDir;
        bulletClone.GetComponent<EnemyProjectileFly>().SetDirection(finalDir);
    }
    public void SetTartget(GameObject inTarget)
    {
        this.target = inTarget;
    }
}
