using System.Collections.Generic;
using UnityEngine;

public class HealthPointManager : MonoBehaviour
{
    [Header("Health Settings")]
    [Min(0)] public int HealthPoint = 3;

    [Header("Heart Sprite Setup")]
    [SerializeField] private GameObject heartSpritePrefab;
    [SerializeField] private Transform heartContainer;
    [SerializeField] private float heartOffsetPosX = 100f;
    [SerializeField] private bool useFirstHeartInContainerAsTemplate = true;
    [SerializeField] private bool hideTemplateHeartAfterBuild = true;

    [Header("Vegetable Tracking")]
    [SerializeField] private bool autoFindVegetablesIfListEmpty = true;
    [SerializeField] private Vegetable[] trackedVegetables;

    [Header("Sync")]
    [SerializeField] private bool continuousSync = true;
    [SerializeField] [Min(0.05f)] private float syncIntervalSeconds = 0.25f;

    private readonly List<GameObject> heartInstances = new List<GameObject>();
    private readonly HashSet<Vegetable> countedDeadVegetables = new HashSet<Vegetable>();
    private GameObject resolvedHeartTemplate;

    private int currentHealthPoint;
    private float nextSyncTime;

    public int CurrentHealthPoint => currentHealthPoint;
    public int RemainingHearts => Mathf.Clamp(currentHealthPoint, 0, HealthPoint);

    private void OnEnable()
    {
        Vegetable.VegetableDied += OnVegetableDied;
        InitializeTrackedVegetables(true);
        SyncWithAlreadyDeadVegetables();
    }

    private void OnDisable()
    {
        Vegetable.VegetableDied -= OnVegetableDied;
    }

    private void Start()
    {
        BuildHeartSprites();
        InitializeTrackedVegetables(true);
        SyncWithAlreadyDeadVegetables();
    }

    private void Update()
    {
        if (!continuousSync)
        {
            return;
        }

        if (Time.time < nextSyncTime)
        {
            return;
        }

        nextSyncTime = Time.time + syncIntervalSeconds;

        int previousHealthPoint = currentHealthPoint;
        SyncWithAlreadyDeadVegetables();

        if (previousHealthPoint != currentHealthPoint)
        {
            RefreshHeartSprites();
        }
    }

    private void BuildHeartSprites()
    {
        ClearHeartSprites();

        resolvedHeartTemplate = ResolveHeartTemplate();

        if (resolvedHeartTemplate == null)
        {
            Debug.LogWarning("HealthPointManager: No heart template found. Assign Heart Sprite Prefab or place one heart under Heart Container.", this);
            return;
        }

        Transform parent = heartContainer != null ? heartContainer : transform;
        Vector3 baseLocalPosition = resolvedHeartTemplate.transform.localPosition;
        Vector2 baseAnchoredPosition = Vector2.zero;
        bool useAnchoredPosition = resolvedHeartTemplate.transform is RectTransform;

        if (useAnchoredPosition)
        {
            baseAnchoredPosition = ((RectTransform)resolvedHeartTemplate.transform).anchoredPosition;
        }

        for (int i = 0; i < HealthPoint; i++)
        {
            GameObject heart = Instantiate(resolvedHeartTemplate, parent);
            heart.SetActive(true);

            if (useAnchoredPosition && heart.transform is RectTransform)
            {
                RectTransform heartRect = (RectTransform)heart.transform;
                heartRect.anchoredPosition = baseAnchoredPosition + new Vector2(i * heartOffsetPosX, 0f);
            }
            else
            {
                heart.transform.localPosition = baseLocalPosition + new Vector3(i * heartOffsetPosX, 0f, 0f);
            }

            heartInstances.Add(heart);
        }

        HideTemplateHeartIfNeeded();
    }

    private GameObject ResolveHeartTemplate()
    {
        if (heartSpritePrefab != null)
        {
            return heartSpritePrefab;
        }

        if (!useFirstHeartInContainerAsTemplate || heartContainer == null)
        {
            return null;
        }

        for (int i = 0; i < heartContainer.childCount; i++)
        {
            Transform child = heartContainer.GetChild(i);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void HideTemplateHeartIfNeeded()
    {
        if (resolvedHeartTemplate == null || !resolvedHeartTemplate.scene.IsValid())
        {
            return;
        }

        bool templateComesFromContainer =
            heartSpritePrefab == null &&
            useFirstHeartInContainerAsTemplate &&
            heartContainer != null &&
            resolvedHeartTemplate.transform.parent == heartContainer;

        // Always hide runtime container template so only managed clones are visible.
        if (templateComesFromContainer || hideTemplateHeartAfterBuild)
        {
            resolvedHeartTemplate.SetActive(false);
        }
    }

    private void ClearHeartSprites()
    {
        for (int i = 0; i < heartInstances.Count; i++)
        {
            if (heartInstances[i] != null)
            {
                Destroy(heartInstances[i]);
            }
        }

        heartInstances.Clear();
    }

    private void InitializeTrackedVegetables(bool forceRefresh = false)
    {
        if (!autoFindVegetablesIfListEmpty)
        {
            return;
        }

        if (forceRefresh || trackedVegetables == null || trackedVegetables.Length == 0 || HasNullTrackedVegetables())
        {
            trackedVegetables = FindObjectsOfType<Vegetable>();
        }
    }

    private void SyncWithAlreadyDeadVegetables()
    {
        countedDeadVegetables.Clear();
        currentHealthPoint = HealthPoint;
        InitializeTrackedVegetables();

        if (trackedVegetables == null)
        {
            return;
        }

        for (int i = 0; i < trackedVegetables.Length; i++)
        {
            Vegetable vegetable = trackedVegetables[i];
            if (vegetable == null || !vegetable.IsDead)
            {
                continue;
            }

            countedDeadVegetables.Add(vegetable);
            currentHealthPoint--;
        }

        ClampHealthPoint();
        RefreshHeartSprites();
    }

    private void OnVegetableDied(Vegetable deadVegetable)
    {
        if (deadVegetable == null)
        {
            return;
        }

        if (!IsTrackedVegetable(deadVegetable))
        {
            InitializeTrackedVegetables(true);
            if (!IsTrackedVegetable(deadVegetable))
            {
                return;
            }
        }

        if (!countedDeadVegetables.Add(deadVegetable))
        {
            return;
        }

        currentHealthPoint--;
        ClampHealthPoint();
        RefreshHeartSprites();
    }

    private bool IsTrackedVegetable(Vegetable vegetable)
    {
        if (trackedVegetables == null || trackedVegetables.Length == 0)
        {
            return autoFindVegetablesIfListEmpty;
        }

        for (int i = 0; i < trackedVegetables.Length; i++)
        {
            if (trackedVegetables[i] == vegetable)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasNullTrackedVegetables()
    {
        if (trackedVegetables == null)
        {
            return false;
        }

        for (int i = 0; i < trackedVegetables.Length; i++)
        {
            if (trackedVegetables[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    private void ClampHealthPoint()
    {
        currentHealthPoint = Mathf.Clamp(currentHealthPoint, 0, HealthPoint);
    }

    private void RefreshHeartSprites()
    {
        int expectedHearts = RemainingHearts;

        while (heartInstances.Count < expectedHearts)
        {
            AddOneHeartSprite(heartInstances.Count);
        }

        while (heartInstances.Count > expectedHearts)
        {
            RemoveOneHeartSprite();
        }

        HideTemplateHeartIfNeeded();
    }

    private void AddOneHeartSprite(int index)
    {
        if (resolvedHeartTemplate == null)
        {
            resolvedHeartTemplate = ResolveHeartTemplate();
        }

        if (resolvedHeartTemplate == null)
        {
            return;
        }

        Transform parent = heartContainer != null ? heartContainer : transform;
        GameObject heart = Instantiate(resolvedHeartTemplate, parent);
        heart.SetActive(true);

        Vector3 baseLocalPosition = resolvedHeartTemplate.transform.localPosition;
        if (resolvedHeartTemplate.transform is RectTransform && heart.transform is RectTransform)
        {
            RectTransform baseRect = (RectTransform)resolvedHeartTemplate.transform;
            RectTransform heartRect = (RectTransform)heart.transform;
            heartRect.anchoredPosition = baseRect.anchoredPosition + new Vector2(index * heartOffsetPosX, 0f);
        }
        else
        {
            heart.transform.localPosition = baseLocalPosition + new Vector3(index * heartOffsetPosX, 0f, 0f);
        }

        heartInstances.Add(heart);
    }

    private void RemoveOneHeartSprite()
    {
        if (heartInstances.Count == 0)
        {
            return;
        }

        int lastIndex = heartInstances.Count - 1;
        GameObject heart = heartInstances[lastIndex];
        heartInstances.RemoveAt(lastIndex);

        if (heart != null)
        {
            Destroy(heart);
        }
    }
}
