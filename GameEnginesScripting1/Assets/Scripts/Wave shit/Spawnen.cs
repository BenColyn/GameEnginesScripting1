using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public class Spawnen : MonoBehaviour
{
    [Header("Wave Settings")]
    public GameObject spawn;
    public GameObject enemyPrefab;
    [SerializeField] private float maxZ = 20.0f;
    [SerializeField] private float maxX = 20.0f;
    private int waveNumber;
    private int enemySpawnAmount=0;
    private int enemiesKilled;
    
    [Header("UI & Reset")]
    public GameObject player;
    public Transform plPosition;
    public GameObject deathScreenUI;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        WaveTrigger trigger = GetComponent<WaveTrigger>();
        bool spawn = trigger.GetSpawnWave();
        
        if (spawn == true)
        {
            StartWave();
            trigger.SetSpawnWave(false);

        }
    }
    void Spawn()
    {
        if (enemyPrefab != null)
        {
            float spawnRadius = 100f;
            Vector2 randomCirclePoint = UnityEngine.Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = new Vector3( transform.position.x + randomCirclePoint.x, 0f, transform.position.z + randomCirclePoint.y );
            GameObject spawnedEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            EnemyMovement movementScript = spawnedEnemy.GetComponent<EnemyMovement>();
            EnemyAttack attackScript = spawnedEnemy.GetComponent<EnemyAttack>();
            EnemyDie dieScript = spawnedEnemy.GetComponent<EnemyDie>();
            if (movementScript != null)
            {
                movementScript.SetTartget(player);
                movementScript.SetTargetPosition(plPosition);
            }
            if(attackScript != null)
            {
                attackScript.SetTartget(player);
            }
            if (dieScript != null)
            {
                dieScript.spawner = this;
            }
        }
    }
    void StartWave()
    {
        enemySpawnAmount += 2;
        enemiesKilled = 0;
        for (int i = 0; i < enemySpawnAmount; i++)
        {
            Spawn();

        }
    }
  /*  public void NextWave()
    {
        waveNumber++;
        enemySpawnAmount += 2;
        enemiesKilled = 0;
        
    }*/
    public void ResetPlayerPosition()
    {
        if (player != null && spawn != null)
        {
           
            CharacterController controller = player.GetComponent<CharacterController>();

            if (controller != null) controller.enabled = false;

            // Die Position setzen
            player.transform.position = spawn.transform.position;
            player.transform.rotation = spawn.transform.rotation;

            if (controller != null) controller.enabled = true;

            Debug.Log("Spieler zurück zum Start teleportiert!");
        }
    }
    public void OnEnemyKilled()
    {
        enemiesKilled++;
        Debug.Log("+1");
        if (enemiesKilled >= enemySpawnAmount)
        {
            ResetPlayerPosition();
           
        }
    }
    public void ShowDeathScreen()
    {
        deathScreenUI.SetActive(true);
        Time.timeScale = 0f; 

        
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    
    public void ResetGame()
    {
        Time.timeScale = 1f;
        enemiesKilled = 0;
        waveNumber = 0;

        GameObject[] restlicheGegner = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject gegner in restlicheGegner)
        {
            Destroy(gegner);
        }
        ResetPlayerPosition();
        

        deathScreenUI.SetActive(false);
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

    }


}
 