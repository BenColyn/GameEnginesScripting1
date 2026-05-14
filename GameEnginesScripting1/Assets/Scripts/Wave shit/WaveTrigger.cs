using UnityEngine;

public class WaveTrigger : MonoBehaviour
{
    private bool spawnWave;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Target"))
        {
            spawnWave = true;
            
        }
         
    }
    public bool GetSpawnWave()
    {
        return spawnWave;
    }
    public void SetSpawnWave(bool spawnWave)
    {
        this.spawnWave = spawnWave;
    }
}
