using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class SniperWeapon : MonoBehaviour
{
    public int magazineSize = 10;
    [SerializeField]
    private int currentAmmo = 10;

    public Text ammoText;
    public TMP_Text ammoTextTMP;
    public GameObject scopeOverlay;
    public RectTransform scopeOverlayRectTransform;
    public Image scopeOverlayImage;
    public RawImage scopeRawImage;
    public Camera scopeCamera;
    public SpriteRenderer scopeOverlaySpriteRenderer;
    public SpriteRenderer weaponSpriteRenderer;
    public Renderer weaponMeshRenderer;
    public string scopeSpriteFolder = "mock_scope";
    public int scopeSpriteIndex = 0;
    public bool alwaysShowScope = true;

    public float normalFOV = 60f;
    public float scopedFOV = 30f;
    public float minZoomFOV = 15f;
    public float maxZoomFOV = 40f;
    public float zoomSpeed = 5f;
    public LayerMask monsterLayerMask = ~0;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootClickSfx;
    [SerializeField] private AudioClip emptyClickSfx;
    [SerializeField] private AudioClip reloadSfx;
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;

    private Camera _camera;
    private bool _isScoped;
    private Sprite[] scopeSprites;
    private RectTransform _scopeOverlayRectTransform;
    private Canvas _scopeOverlayCanvas;
    private const string AmmoTextFormat = "{0}/{1}";

    public int CurrentAmmo
    {
        get => currentAmmo;
        private set
        {
            currentAmmo = Mathf.Clamp(value, 0, magazineSize);
            UpdateAmmoUI();
        }
    }

    public int MagazineSize
    {
        get => magazineSize;
        set
        {
            magazineSize = Mathf.Max(1, value);
            CurrentAmmo = currentAmmo;
            UpdateAmmoUI();
        }
    }

    private void Awake()
    {
        _camera = Camera.main;
        if (_camera != null)
        {
            _camera.fieldOfView = normalFOV;
        }

        scopeSprites = LoadSpritesFromFolder(scopeSpriteFolder);
        SetScopeOverlaySprite(scopeSpriteIndex);
        SetScopeOverlay(alwaysShowScope);
        if (alwaysShowScope)
        {
            _isScoped = true;
        }
        InitializeScopeOverlayRectTransform();

        CurrentAmmo = currentAmmo;
        UpdateAmmoUI();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void InitializeScopeOverlayRectTransform()
    {
        if (scopeOverlayRectTransform != null)
        {
            _scopeOverlayRectTransform = scopeOverlayRectTransform;
        }
        else if (scopeOverlay != null)
        {
            _scopeOverlayRectTransform = scopeOverlay.GetComponent<RectTransform>();
        }

        if (_scopeOverlayRectTransform != null)
        {
            _scopeOverlayCanvas = _scopeOverlayRectTransform.GetComponentInParent<Canvas>();
        }
    }

    private void Update()
    {
        HandleShootInput();
        HandleScopeInput();
        HandleZoomInput();
        HandleReloadInput();
        UpdateScopeOverlayPosition();
    }

    private void HandleShootInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
    }

    private void HandleScopeInput()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            EnterScope();
        }

        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            ExitScope();
        }
    }

    private void HandleZoomInput()
    {
        if (!_isScoped)
            return;

        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (scrollValue == 0f)
            return;

        Camera targetCamera = scopeCamera != null ? scopeCamera : _camera;
        if (targetCamera == null)
            return;

        float targetFOV = targetCamera.fieldOfView - scrollValue * zoomSpeed;
        targetCamera.fieldOfView = Mathf.Clamp(targetFOV, minZoomFOV, maxZoomFOV);
    }

    private void HandleReloadInput()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            ReloadMagazine();
        }
    }

    private void Shoot()
    {
        PlaySfx(shootClickSfx);

        if (CurrentAmmo <= 0)
        {
            if (emptyClickSfx != null)
            {
                PlaySfx(emptyClickSfx);
            }
            Debug.Log("SniperWeapon: No ammo. Press R to reload.");
            return;
        }

        CurrentAmmo--;
        Debug.Log($"SniperWeapon: Fired. Ammo left {CurrentAmmo}/{MagazineSize}");

        Camera aimCamera = scopeCamera != null ? scopeCamera : (_camera != null ? _camera : Camera.main);
        if (aimCamera == null)
        {
            return;
        }

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector2 worldPoint = aimCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, Mathf.Abs(aimCamera.transform.position.z)));
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint, monsterLayerMask);

        Monster hitMonster = null;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            Monster candidate = hits[i].GetComponentInParent<Monster>();
            if (candidate != null && candidate.IsAlive)
            {
                hitMonster = candidate;
                break;
            }
        }

        if (hitMonster != null)
        {
            hitMonster.TakeHit();
        }

    }

    private void EnterScope()
    {
        _isScoped = true;
        Camera targetCamera = scopeCamera != null ? scopeCamera : _camera;
        if (targetCamera != null)
        {
            targetCamera.fieldOfView = Mathf.Clamp(scopedFOV, minZoomFOV, maxZoomFOV);
        }

        SetScopeOverlay(true);
        if (scopeCamera != null)
        {
            scopeCamera.enabled = true;
        }
        Debug.Log("SniperWeapon: Scoped in.");
    }

    private void ExitScope()
    {
        _isScoped = false;
        if (_camera != null)
        {
            _camera.fieldOfView = normalFOV;
        }

        if (!alwaysShowScope)
        {
            SetScopeOverlay(false);
        }

        if (scopeCamera != null)
        {
            scopeCamera.enabled = false;
        }
        Debug.Log("SniperWeapon: Scoped out.");
    }

    public void ReloadMagazine()
    {
        CurrentAmmo = MagazineSize;
        PlaySfx(reloadSfx);
        Debug.Log($"SniperWeapon: Reloaded. Ammo {CurrentAmmo}/{MagazineSize}");
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, sfxVolume);
            return;
        }

        Vector3 soundPosition = _camera != null ? _camera.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, soundPosition, sfxVolume);
    }

    private void UpdateAmmoUI()
    {
        string ammoTextValue = string.Format(AmmoTextFormat, CurrentAmmo, MagazineSize);

        if (ammoTextTMP != null)
        {
            ammoTextTMP.text = ammoTextValue;
        }
        else if (ammoText != null)
        {
            ammoText.text = ammoTextValue;
        }
    }

    public Sprite[] LoadSpritesFromFolder(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            return null;

        folderName = NormalizeResourcesPath(folderName);
        if (string.IsNullOrEmpty(folderName))
            return null;

        string[] candidates = GetResourcePathCandidates(folderName);
        foreach (string candidate in candidates)
        {
            Debug.Log($"SniperWeapon: Trying Resources path '{candidate}'.");

            Sprite[] sprites = Resources.LoadAll<Sprite>(candidate);
            Debug.Log($"SniperWeapon: Resources.LoadAll<Sprite>('{candidate}') returned {(sprites == null ? 0 : sprites.Length)} item(s).");
            if (sprites != null && sprites.Length > 0)
            {
                return sprites;
            }

            Object resourceObject = Resources.Load(candidate);
            Debug.Log($"SniperWeapon: Resources.Load('{candidate}') returned {(resourceObject == null ? "null" : resourceObject.GetType().Name)}.");
            if (resourceObject is Sprite singleSprite)
            {
                return new[] { singleSprite };
            }

            if (resourceObject is Texture2D texture)
            {
                Sprite createdSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                Debug.Log($"SniperWeapon: Created sprite from Texture2D '{texture.name}'.");
                return new[] { createdSprite };
            }

            Texture2D directTexture = Resources.Load<Texture2D>(candidate);
            Debug.Log($"SniperWeapon: Resources.Load<Texture2D>('{candidate}') returned {(directTexture == null ? "null" : directTexture.name)}.");
            if (directTexture != null)
            {
                Sprite createdSprite = Sprite.Create(directTexture, new Rect(0, 0, directTexture.width, directTexture.height), new Vector2(0.5f, 0.5f));
                return new[] { createdSprite };
            }
        }

        Debug.LogWarning($"SniperWeapon: Could not load any sprite or texture from Resources paths '{string.Join(", ", GetResourcePathCandidates(folderName))}'.");
        return null;
    }

    private string[] GetResourcePathCandidates(string path)
    {
        var candidates = new System.Collections.Generic.List<string> { path };
        string fileName = System.IO.Path.GetFileName(path);

        if (!string.Equals(fileName, path, System.StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(fileName);
        }

        if (fileName.EndsWith("s", System.StringComparison.OrdinalIgnoreCase))
        {
            string singular = System.IO.Path.ChangeExtension(fileName, null).TrimEnd('s');
            if (!string.IsNullOrEmpty(singular) && !candidates.Contains(singular))
            {
                string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(parent))
                {
                    candidates.Add(parent + "/" + singular);
                }
                candidates.Add(singular);
            }
        }

        return candidates.ToArray();
    }

    private string NormalizeResourcesPath(string path)
    {
        path = path.Trim().Replace("\\", "/");

        const string resourcesPrefix = "Assets/Art/Scope/";
        if (path.StartsWith(resourcesPrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(resourcesPrefix.Length);
        }

        if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase))
        {
            path = System.IO.Path.ChangeExtension(path, null).Replace("\\", "/");
        }

        return path;
    }

    public Sprite GetSprite(int index)
    {
        if (scopeSprites == null || index < 0 || index >= scopeSprites.Length)
            return null;

        return scopeSprites[index];
    }

    public void SetScopeOverlaySprite(int index)
    {
        if (scopeSprites == null)
        {
            scopeSprites = LoadSpritesFromFolder(scopeSpriteFolder);
        }

        Sprite sprite = GetSprite(index);
        if (sprite == null)
        {
            Debug.LogWarning($"SniperWeapon: No sprite found at index {index} in folder '{scopeSpriteFolder}'.");
            return;
        }

        Debug.Log($"SniperWeapon: Applying sprite '{sprite.name}' to scope overlay.");
        bool applied = false;

        if (scopeOverlayImage != null)
        {
            scopeOverlayImage.sprite = sprite;
            applied = true;
            Debug.Log("SniperWeapon: Applied sprite to scopeOverlayImage.");
        }

        if (!applied && scopeOverlaySpriteRenderer != null)
        {
            scopeOverlaySpriteRenderer.sprite = sprite;
            applied = true;
            Debug.Log("SniperWeapon: Applied sprite to scopeOverlaySpriteRenderer.");
        }

        if (!applied && scopeOverlay != null)
        {
            Image image = scopeOverlay.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
                applied = true;
                Debug.Log("SniperWeapon: Applied sprite to scopeOverlay GameObject Image component.");
            }

            if (!applied)
            {
                SpriteRenderer overlayRenderer = scopeOverlay.GetComponent<SpriteRenderer>();
                if (overlayRenderer != null)
                {
                    overlayRenderer.sprite = sprite;
                    applied = true;
                    Debug.Log("SniperWeapon: Applied sprite to scopeOverlay GameObject SpriteRenderer.");
                }
            }
        }

        if (!applied && scopeOverlaySpriteRenderer != null)
        {
            scopeOverlaySpriteRenderer.sprite = sprite;
            applied = true;
            Debug.Log("SniperWeapon: Applied sprite to scopeOverlaySpriteRenderer.");
        }

        if (!applied)
        {
            Debug.LogWarning("SniperWeapon: Could not apply loaded sprite. Assign scopeOverlayImage, scopeOverlaySpriteRenderer, or a scopeOverlay GameObject with Image/SpriteRenderer.");
        }
    }

    public void ApplySpriteToWeapon(int index)
    {
        if (weaponSpriteRenderer == null)
            return;

        Sprite sprite = GetSprite(index);
        if (sprite == null)
            return;

        weaponSpriteRenderer.sprite = sprite;
    }

    public void ApplyTextureToWeapon(int index)
    {
        if (weaponMeshRenderer == null)
            return;

        Sprite sprite = GetSprite(index);
        if (sprite == null)
            return;

        Texture texture = sprite.texture;
        if (weaponMeshRenderer.material != null)
        {
            weaponMeshRenderer.material.mainTexture = texture;
        }
    }

    private void SetScopeOverlay(bool active)
    {
        if (scopeOverlay != null)
        {
            scopeOverlay.SetActive(active);
        }

        if (scopeRawImage != null)
        {
            scopeRawImage.gameObject.SetActive(active);
        }
    }

    private void UpdateScopeOverlayPosition()
    {
        if (!_isScoped && !alwaysShowScope)
            return;

        if (_scopeOverlayRectTransform == null && scopeOverlay != null)
        {
            _scopeOverlayRectTransform = scopeOverlay.GetComponent<RectTransform>();
            if (_scopeOverlayRectTransform != null)
            {
                _scopeOverlayCanvas = _scopeOverlayRectTransform.GetComponentInParent<Canvas>();
            }
        }

        if (_scopeOverlayRectTransform != null)
        {
            Vector2 uiPosition;
            Camera renderCamera = null;
            if (_scopeOverlayCanvas != null && _scopeOverlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                renderCamera = _scopeOverlayCanvas.worldCamera ?? Camera.main;
            }

            RectTransform parentRect = _scopeOverlayRectTransform.parent as RectTransform;
            if (parentRect == null)
            {
                parentRect = _scopeOverlayRectTransform;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                Mouse.current.position.ReadValue(),
                renderCamera,
                out uiPosition))
            {
                _scopeOverlayRectTransform.anchoredPosition = uiPosition;
            }
            else
            {
                _scopeOverlayRectTransform.position = Mouse.current.position.ReadValue();
            }

            return;
        }

        if (scopeOverlay != null && Camera.main != null)
        {
            Vector3 mousePosition = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Camera.main.nearClipPlane + 0.1f));
            scopeOverlay.transform.position = worldPos;
        }
    }
}