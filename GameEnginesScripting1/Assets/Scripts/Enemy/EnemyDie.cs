using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.VFX;

public class EnemyDie : MonoBehaviour
{
    public Spawnen spawner;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void Die()
    {
        if (spawner != null)
        {
            spawner.OnEnemyKilled(); 
        }
        Destroy(gameObject);
    }
}
