using UnityEngine;
using System.Collections;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] private GameObject monsterPrefab;

    [SerializeField] private float spawnPerimeterRadius = 15f;
    [SerializeField] private Vector2 spawnPerimeterCenter = Vector2.zero;
    [SerializeField] private float initialDelay = 5f;

    [SerializeField] private float initialSpawnInterval = 2f;
    [SerializeField] private float waveDuration = 60f;
    [SerializeField] private float intervalDecreasePerWave = 0.2f;

    [SerializeField] private Transform vegetableTarget;
    [SerializeField] private bool useFixedTarget = false;
    [SerializeField] private Vector3 fixedTargetPoint;

    private int currentWave = 0;
    private float currentSpawnInterval;
    private float waveTimer = 0f;
    private float spawnTimer = 0f;
    private bool isSpawning = false;
    private int monstersPerSpawn = 1;
    private int totalMonstersSpawned = 0;

    private void Start()
    {
        currentSpawnInterval = initialSpawnInterval;
        StartCoroutine(BeginSpawning());
    }

    private IEnumerator BeginSpawning()
    {
        // Wait for initial delay
        yield return new WaitForSeconds(initialDelay);
        isSpawning = true;
    }

    private void Update()
    {
        if (!isSpawning) return;

        waveTimer += Time.deltaTime;
        spawnTimer += Time.deltaTime;

        // Check if current wave is complete
        if (waveTimer >= waveDuration)
        {
            StartNewWave();
        }

        // Spawn monsters based on interval
        if (spawnTimer >= currentSpawnInterval)
        {
            SpawnWaveMonsters();
            spawnTimer = 0f;
        }
    }

    private void SpawnWaveMonsters()
    {
        if (monsterPrefab == null)
        {
            Debug.LogError("Monster prefab is not assigned!");
            return;
        }

        for (int i = 0; i < monstersPerSpawn; i++)
        {
            SpawnSingleMonster();
        }
    }

    private void SpawnSingleMonster()
    {
        // Random point on spawn perimeter
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 spawnPosition = spawnPerimeterCenter + new Vector2(
            Mathf.Cos(angle) * spawnPerimeterRadius,
            Mathf.Sin(angle) * spawnPerimeterRadius
        );

        GameObject monsterInstance = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);
        Monster monsterScript = monsterInstance.GetComponent<Monster>();

        if (monsterScript != null)
        {
            monsterScript.SetMovementSpeed(GetDifficultySpeed());
            monsterScript.SetTarget(vegetableTarget, fixedTargetPoint, useFixedTarget);
        }
        else
        {
            Debug.LogError("Spawned monster prefab does not contain a Monster script.", monsterInstance);
        }

        totalMonstersSpawned++;
    }

    private void StartNewWave()
    {
        currentWave++;
        waveTimer = 0f;
        spawnTimer = 0f;

        // Increase difficulty
        currentSpawnInterval = Mathf.Max(0.5f, initialSpawnInterval - (currentWave * intervalDecreasePerWave));
        monstersPerSpawn = 1 + (currentWave / 5); // Increase every 5 waves

        Debug.Log($"Wave {currentWave} started! Spawn Interval: {currentSpawnInterval:F2}s, Monsters per spawn: {monstersPerSpawn}");
    }

    private float GetDifficultySpeed()
    {
        // Increase monster speed as waves progress
        float baseSpeed = 5f;
        return baseSpeed + (currentWave * 0.3f);
    }

    public void StopSpawning()
    {
        isSpawning = false;
    }

    public void ResumeSpawning()
    {
        isSpawning = true;
    }

    public void ResetWaves()
    {
        StopSpawning();
        currentWave = 0;
        waveTimer = 0f;
        spawnTimer = 0f;
        monstersPerSpawn = 1;
        currentSpawnInterval = initialSpawnInterval;
        totalMonstersSpawned = 0;
        StartCoroutine(BeginSpawning());
    }

    public int GetCurrentWave() => currentWave;
    public int GetTotalMonstersSpawned() => totalMonstersSpawned;
    public float GetSpawnIntervalForWave(int wave) => Mathf.Max(0.5f, initialSpawnInterval - (wave * intervalDecreasePerWave));
}
