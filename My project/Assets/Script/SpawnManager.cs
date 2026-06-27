using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    private static SpawnManager activeInstance;
    private static int pendingStartingLevel = 0;

    [SerializeField] private GameObject monsterPrefab;

    [SerializeField] private float spawnPerimeterRadius = 15f;
    [SerializeField] private Vector2 spawnPerimeterCenter = Vector2.zero;
    [SerializeField] private float initialDelay = 2f;

    [SerializeField] private float initialSpawnInterval = 2f;
    [SerializeField] private float waveDuration = 60f;
    [SerializeField] private float intervalDecreasePerWave = 0.2f;

    [Header("Level Settings")]
    [SerializeField] private int totalLevels = 3;
    [SerializeField] private float initialDelayAdjustmentPerLevel = -0.2f;
    [SerializeField] private float initialSpawnIntervalAdjustmentPerLevel = 1f;
    [SerializeField] private float waveDurationAdjustmentPerLevel = 10f;
    [SerializeField] private float intervalDecreaseAdjustmentPerLevel = -0.05f;
    [SerializeField] private float baseMonsterSpeed = 5f;
    [SerializeField] private float monsterSpeedMultiplierPerLevel = 2f;

    [SerializeField] private bool spawnFromSceneCorners = true;
    [SerializeField] private Camera spawnCamera;
    [SerializeField] private float spawnZ = 0f;

    [SerializeField] private Vegetable[] vegetableTargets;
    [SerializeField] private bool autoFindVegetablesIfListEmpty = true;
    [SerializeField] private Vector3 fixedTargetPoint;

    private int currentWave = 0;
    private int currentLevel = 0;
    private float currentSpawnInterval;
    private float currentInitialDelay;
    private float currentInitialSpawnInterval;
    private float currentWaveDuration;
    private float currentIntervalDecreasePerWave;
    private float waveTimer = 0f;
    private float spawnTimer = 0f;
    private bool isSpawning = false;
    private int monstersPerSpawn = 1;
    private int totalMonstersSpawned = 0;

    private void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogWarning("Duplicate SpawnManager detected and disabled.", this);
            enabled = false;
            return;
        }

        activeInstance = this;
        ResetRuntimeState();
    }

    private void Start()
    {
        StartCoroutine(BeginSpawning());
    }

    private void ResetRuntimeState()
    {
        currentWave = 0;
        currentLevel = Mathf.Clamp(pendingStartingLevel, 0, Mathf.Max(0, totalLevels - 1));
        ApplyLevelSettings();
        currentSpawnInterval = currentInitialSpawnInterval;
        waveTimer = 0f;
        spawnTimer = 0f;
        monstersPerSpawn = 1;
        totalMonstersSpawned = 0;
        isSpawning = false;
    }

    private IEnumerator BeginSpawning()
    {
        // Wait for initial delay
        yield return new WaitForSeconds(currentInitialDelay);
        isSpawning = true;

        // Spawn one batch immediately when spawning starts.
        SpawnWaveMonsters();
        spawnTimer = 0f;
    }

    private void Update()
    {
        if (!isSpawning) return;

        waveTimer += Time.deltaTime;
        spawnTimer += Time.deltaTime;

        // Check if current wave is complete
        if (waveTimer >= currentWaveDuration)
        {
            StartNewWave();
        }

        // Spawn monsters based on interval; catch up if a frame takes longer than interval.
        while (spawnTimer >= currentSpawnInterval)
        {
            SpawnWaveMonsters();
            spawnTimer -= currentSpawnInterval;

            // Safety break to avoid huge burst if timer spikes unexpectedly.
            if (spawnTimer > currentSpawnInterval * 10f)
            {
                spawnTimer = 0f;
                break;
            }
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
        Vector3 spawnPosition;
        Vector3 targetPoint;
        bool useFixed = false;
        Transform selectedVegetableTarget = GetRandomAliveVegetableTarget();

        if (spawnFromSceneCorners)
        {
            int edgeIndex;
            spawnPosition = GetRandomScreenEdgeSpawnPosition(out edgeIndex);
            targetPoint = GetRandomOppositeEdgeTargetPoint(edgeIndex);
        }
        else
        {
            spawnPosition = GetRandomPerimeterSpawnPosition();
            targetPoint = fixedTargetPoint;
        }

        // Fallback when no alive vegetables are available.
        if (selectedVegetableTarget == null)
        {
            useFixed = true;
        }

        GameObject monsterInstance = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);
        Monster monsterScript = monsterInstance.GetComponent<Monster>();

        if (monsterScript != null)
        {
            monsterScript.SetMovementSpeed(GetDifficultySpeed());
            monsterScript.SetTarget(selectedVegetableTarget, targetPoint, useFixed);
        }
        else
        {
            Debug.LogError("Spawned monster prefab does not contain a Monster script.", monsterInstance);
        }

        totalMonstersSpawned++;
    }

    private Vector3 GetRandomPerimeterSpawnPosition()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 spawnPosition = spawnPerimeterCenter + new Vector2(
            Mathf.Cos(angle) * spawnPerimeterRadius,
            Mathf.Sin(angle) * spawnPerimeterRadius
        );
        return new Vector3(spawnPosition.x, spawnPosition.y, spawnZ);
    }

    private Transform GetRandomAliveVegetableTarget()
    {
        List<Transform> aliveTargets = new List<Transform>();

        if (vegetableTargets != null && vegetableTargets.Length > 0)
        {
            foreach (Vegetable vegetable in vegetableTargets)
            {
                if (vegetable != null && vegetable.IsAlive)
                {
                    aliveTargets.Add(vegetable.transform);
                }
            }
        }

        if (aliveTargets.Count == 0 && autoFindVegetablesIfListEmpty)
        {
            Vegetable[] allVegetables = FindObjectsOfType<Vegetable>();
            if (allVegetables != null)
            {
                foreach (Vegetable vegetable in allVegetables)
                {
                    if (vegetable != null && vegetable.IsAlive)
                    {
                        aliveTargets.Add(vegetable.transform);
                    }
                }
            }
        }

        if (aliveTargets.Count == 0)
        {
            return null;
        }

        int randomAliveIndex = Random.Range(0, aliveTargets.Count);
        return aliveTargets[randomAliveIndex];
    }

    private Vector3 GetRandomScreenEdgeSpawnPosition(out int edgeIndex)
    {
        // Spawn only from left/right edges.
        edgeIndex = Random.value < 0.5f ? 1 : 3;
        return GetRandomPointOnScreenEdge(edgeIndex);
    }

    private Vector3 GetRandomOppositeEdgeTargetPoint(int edgeIndex)
    {
        switch (edgeIndex)
        {
            case 1: return GetRandomPointOnScreenEdge(3); // right -> left
            case 3: return GetRandomPointOnScreenEdge(1); // left -> right
            default: return GetRandomPointOnScreenEdge(3);
        }
    }

    private Vector3 GetRandomPointOnScreenEdge(int edgeIndex)
    {
        if (spawnCamera == null)
        {
            spawnCamera = Camera.main;
        }

        if (spawnCamera != null)
        {
            float distance = Mathf.Abs(spawnCamera.transform.position.z - spawnZ);
            float t = Random.Range(0f, 1f);
            Vector3 viewportPoint;

            switch (edgeIndex)
            {
                case 0: viewportPoint = new Vector3(t, 1f, distance); break; // top
                case 1: viewportPoint = new Vector3(1f, t, distance); break; // right
                case 2: viewportPoint = new Vector3(t, 0f, distance); break; // bottom
                case 3: viewportPoint = new Vector3(0f, t, distance); break; // left
                default: viewportPoint = new Vector3(t, 1f, distance); break;
            }

            Vector3 worldPoint = spawnCamera.ViewportToWorldPoint(viewportPoint);
            worldPoint.z = spawnZ;
            return worldPoint;
        }

        // Camera fallback: random point on edge of a square around spawnPerimeterCenter.
        float halfSize = spawnPerimeterRadius;
        float randomOffset = Random.Range(-halfSize, halfSize);

        switch (edgeIndex)
        {
            case 0: return new Vector3(spawnPerimeterCenter.x + randomOffset, spawnPerimeterCenter.y + halfSize, spawnZ); // top
            case 1: return new Vector3(spawnPerimeterCenter.x + halfSize, spawnPerimeterCenter.y + randomOffset, spawnZ); // right
            case 2: return new Vector3(spawnPerimeterCenter.x + randomOffset, spawnPerimeterCenter.y - halfSize, spawnZ); // bottom
            case 3: return new Vector3(spawnPerimeterCenter.x - halfSize, spawnPerimeterCenter.y + randomOffset, spawnZ); // left
            default: return new Vector3(spawnPerimeterCenter.x, spawnPerimeterCenter.y + halfSize, spawnZ);
        }
    }

    private void StartNewWave()
    {
        currentWave++;

        ApplyLevelSettings();
        waveTimer = 0f;
        spawnTimer = 0f;

        // Increase difficulty
        currentSpawnInterval = Mathf.Max(0.5f, currentInitialSpawnInterval - (currentWave * currentIntervalDecreasePerWave));
        monstersPerSpawn = 1 + (currentWave / 5); // Increase every 5 waves

        Debug.Log($"Wave {currentWave} started at Level {currentLevel + 1}/{Mathf.Max(1, totalLevels)}! Spawn Interval: {currentSpawnInterval:F2}s, Monsters per spawn: {monstersPerSpawn}");
    }

    private float GetDifficultySpeed()
    {
        // Increase monster speed by multiplier on each level up.
        float safeMultiplier = Mathf.Max(1f, monsterSpeedMultiplierPerLevel);
        return Mathf.Max(0.1f, baseMonsterSpeed) * Mathf.Pow(safeMultiplier, currentLevel);
    }

    private void ApplyLevelSettings()
    {
        int clampedLevel = Mathf.Clamp(currentLevel, 0, Mathf.Max(0, totalLevels - 1));
        currentInitialDelay = Mathf.Max(0f, initialDelay + (clampedLevel * initialDelayAdjustmentPerLevel));
        currentInitialSpawnInterval = Mathf.Max(0.5f, initialSpawnInterval + (clampedLevel * initialSpawnIntervalAdjustmentPerLevel));
        currentWaveDuration = Mathf.Max(1f, waveDuration + (clampedLevel * waveDurationAdjustmentPerLevel));
        currentIntervalDecreasePerWave = Mathf.Max(0f, intervalDecreasePerWave + (clampedLevel * intervalDecreaseAdjustmentPerLevel));
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
        currentLevel = 0;
        ApplyLevelSettings();
        waveTimer = 0f;
        spawnTimer = 0f;
        monstersPerSpawn = 1;
        currentSpawnInterval = currentInitialSpawnInterval;
        totalMonstersSpawned = 0;
        StartCoroutine(BeginSpawning());
    }

    public int GetCurrentWave() => currentWave;
    public int GetCurrentLevel() => currentLevel;
    public int GetTotalLevels() => Mathf.Max(1, totalLevels);
    public int GetTotalMonstersSpawned() => totalMonstersSpawned;
    public static void SetPendingStartingLevel(int level)
    {
        pendingStartingLevel = Mathf.Max(0, level);
    }

    public static void ResetPendingStartingLevel()
    {
        pendingStartingLevel = 0;
    }

    public float GetSpawnIntervalForWave(int wave)
    {
        int clampedLevel = Mathf.Clamp(currentLevel, 0, Mathf.Max(0, totalLevels - 1));
        float levelAdjustedInitialInterval = Mathf.Max(0.5f, initialSpawnInterval + (clampedLevel * initialSpawnIntervalAdjustmentPerLevel));
        float levelAdjustedIntervalDecrease = Mathf.Max(0f, intervalDecreasePerWave + (clampedLevel * intervalDecreaseAdjustmentPerLevel));
        return Mathf.Max(0.5f, levelAdjustedInitialInterval - (wave * levelAdjustedIntervalDecrease));
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }
}
