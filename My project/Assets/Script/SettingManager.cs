using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SettingManager : MonoBehaviour
{
    public static SettingManager Instance { get; private set; }

    private const string MasterVolumeKey = "SettingManager.MasterVolume";
    private const string SoundEffectVolumeKey = "SettingManager.SoundEffectVolume";
    private const string MusicVolumeKey = "SettingManager.MusicVolume";
    private const string CrosshairIndexKey = "SettingManager.CrosshairIndex";
    private const string MouseSensitivityKey = "SettingManager.MouseSensitivity";

    [Header("Persistence")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Default Values")]
    [SerializeField] [Range(0f, 1f)] private float defaultMasterVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float defaultSoundEffectVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float defaultMusicVolume = 1f;
    [SerializeField] private float defaultMouseSensitivity = 1f;
    [SerializeField] private int defaultCrosshairIndex;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource[] musicSources;
    [SerializeField] private AudioSource[] soundEffectSources;

    [Header("Crosshair")]
    [SerializeField] private string crosshairResourcesFolder = "Scope";
    [SerializeField] private Sprite[] crosshairSprites;
    [SerializeField] private Image crosshairPreviewImage;
    [SerializeField] private SniperWeapon[] sniperWeapons;

    [Header("Optional UI Bindings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider soundEffectVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_Dropdown crosshairDropdown;

    [Header("In-Game Settings Panel")]
    [SerializeField] private GameObject settingsPanelRoot;
    [SerializeField] private GameObject settingsPanelPrefab;
    [SerializeField] private bool openWithEscape = true;
    [SerializeField] private bool pauseGameWhileOpen = true;
    [SerializeField] private bool lockCursorWhenClosed = true;

    private readonly List<ManagedAudioSource> managedMusicSources = new List<ManagedAudioSource>();
    private readonly List<ManagedAudioSource> managedSoundEffectSources = new List<ManagedAudioSource>();
    public float MasterVolume { get; private set; }
    public float SoundEffectVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float MouseSensitivity { get; private set; }
    public int CrosshairIndex { get; private set; }

    private bool isSettingsOpen;
    private float previousTimeScale = 1f;

    public bool IsSettingsOpen => isSettingsOpen;
    public static bool IsOpen => Instance != null && Instance.IsSettingsOpen;
    private Transform panelContainer;

    public static float CurrentMouseSensitivity => Instance != null ? Instance.MouseSensitivity : 1f;
    public static float CurrentMasterVolume => Instance != null ? Instance.MasterVolume : 1f;
    public static float CurrentSoundEffectVolume => Instance != null ? Instance.SoundEffectVolume : 1f;
    public static float CurrentMusicVolume => Instance != null ? Instance.MusicVolume : 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        LoadCrosshairSprites();
        LoadSettings();
        RegisterSerializedAudioSources();
        BindUiEvents();
        PrepareSettingsPanel();
        ApplyAllSettings();
        RefreshUi();
        SetSettingsPanelVisible(false, false);
    }

    private void Update()
    {
        if (!openWithEscape)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            Debug.LogWarning("SettingManager: Keyboard.current is null");
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("SettingManager: ESC pressed, toggling settings panel");
            ToggleSettingsPanel();
        }
    }

    private void OnDestroy()
    {
        if (pauseGameWhileOpen && isSettingsOpen)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }

        if (isSettingsOpen)
        {
            Cursor.visible = false;
            if (lockCursorWhenClosed)
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        if (Instance == this)
        {
            Instance = null;
        }

        UnbindUiEvents();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
        Debug.Log($"SettingManager: Master Volume set to {MasterVolume:F2}");
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.Save();
    }

    public void SetSoundEffectVolume(float value)
    {
        SoundEffectVolume = Mathf.Clamp01(value);
        ApplyAudioGroupVolume(managedSoundEffectSources, SoundEffectVolume);
        Debug.Log($"SettingManager: Sound Effect Volume set to {SoundEffectVolume:F2}");
        PlayerPrefs.SetFloat(SoundEffectVolumeKey, SoundEffectVolume);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplyAudioGroupVolume(managedMusicSources, MusicVolume);
        Debug.Log($"SettingManager: Music Volume set to {MusicVolume:F2}");
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.Save();
    }

    public void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Max(0.01f, value);
        Debug.Log($"SettingManager: Mouse Sensitivity set to {MouseSensitivity:F2}");
        PlayerPrefs.SetFloat(MouseSensitivityKey, MouseSensitivity);
        PlayerPrefs.Save();
    }

    public void SetCrosshair(int index)
    {
        Debug.Log($"SettingManager: SetCrosshair({index}) called. Sprites count: {(crosshairSprites?.Length ?? 0)}");
        
        if (crosshairSprites == null || crosshairSprites.Length == 0)
        {
            Debug.LogWarning("SettingManager: No crosshair sprites loaded");
            CrosshairIndex = 0;
            return;
        }

        CrosshairIndex = Mathf.Clamp(index, 0, crosshairSprites.Length - 1);
        Debug.Log($"SettingManager: CrosshairIndex set to {CrosshairIndex}");
        ApplyCrosshair();
        PlayerPrefs.SetInt(CrosshairIndexKey, CrosshairIndex);
        PlayerPrefs.Save();
    }

    public void ToggleSettingsPanel()
    {
        SetSettingsPanelVisible(!isSettingsOpen);
    }

    public void OpenSettingsPanel()
    {
        SetSettingsPanelVisible(true);
    }

    public void CloseSettingsPanel()
    {
        SetSettingsPanelVisible(false);
    }

    public float GetScaledSoundEffectVolume(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * MasterVolume * SoundEffectVolume;
    }

    public float GetScaledMusicVolume(float baseVolume = 1f)
    {
        return Mathf.Clamp01(baseVolume) * MasterVolume * MusicVolume;
    }

    public Sprite GetCurrentCrosshairSprite()
    {
        if (crosshairSprites == null || crosshairSprites.Length == 0)
        {
            return null;
        }

        return crosshairSprites[Mathf.Clamp(CrosshairIndex, 0, crosshairSprites.Length - 1)];
    }

    public void RegisterMusicSource(AudioSource source)
    {
        RegisterAudioSource(source, managedMusicSources);
        ApplyAudioGroupVolume(managedMusicSources, MusicVolume);
    }

    public void RegisterSoundEffectSource(AudioSource source)
    {
        RegisterAudioSource(source, managedSoundEffectSources);
        ApplyAudioGroupVolume(managedSoundEffectSources, SoundEffectVolume);
    }

    public void UnregisterMusicSource(AudioSource source)
    {
        UnregisterAudioSource(source, managedMusicSources);
    }

    public void UnregisterSoundEffectSource(AudioSource source)
    {
        UnregisterAudioSource(source, managedSoundEffectSources);
    }

    private void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, defaultMasterVolume);
        SoundEffectVolume = PlayerPrefs.GetFloat(SoundEffectVolumeKey, defaultSoundEffectVolume);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, defaultMusicVolume);
        MouseSensitivity = PlayerPrefs.GetFloat(MouseSensitivityKey, defaultMouseSensitivity);

        int maxCrosshairIndex = Mathf.Max(0, (crosshairSprites?.Length ?? 1) - 1);
        CrosshairIndex = Mathf.Clamp(PlayerPrefs.GetInt(CrosshairIndexKey, defaultCrosshairIndex), 0, maxCrosshairIndex);
    }

    private void ApplyAllSettings()
    {
        AudioListener.volume = MasterVolume;
        ApplyAudioGroupVolume(managedSoundEffectSources, SoundEffectVolume);
        ApplyAudioGroupVolume(managedMusicSources, MusicVolume);
        ApplyCrosshair();
    }

    private void SetSettingsPanelVisible(bool visible, bool saveCursorState = true)
    {
        isSettingsOpen = visible;
        Debug.Log($"SettingManager: SetSettingsPanelVisible({visible}). Panel root is {(settingsPanelRoot != null ? "assigned" : "NULL")}");

        if (settingsPanelRoot != null)
        {
            settingsPanelRoot.SetActive(visible);
            Debug.Log($"SettingManager: Panel activated: {visible}");
        }
        else
        {
            Debug.LogWarning("SettingManager: settingsPanelRoot is not assigned");
        }

        if (pauseGameWhileOpen)
        {
            if (visible)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            }
        }

        if (saveCursorState)
        {
            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : (lockCursorWhenClosed ? CursorLockMode.Locked : Cursor.lockState);
        }
    }

    private void PrepareSettingsPanel()
    {
        if (settingsPanelRoot != null)
        {
            panelContainer = settingsPanelRoot.transform.parent;
            return;
        }

        if (settingsPanelPrefab == null)
        {
            return;
        }

        GameObject panelInstance = Instantiate(settingsPanelPrefab);
        panelInstance.name = settingsPanelPrefab.name;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            panelInstance.transform.SetParent(canvas.transform, false);
        }

        settingsPanelRoot = panelInstance;
        panelContainer = panelInstance.transform.parent;
    }

    private void ApplyCrosshair()
    {
        Sprite sprite = GetCurrentCrosshairSprite();
        Debug.Log($"SettingManager: ApplyCrosshair() - Current sprite: {(sprite != null ? sprite.name : "NULL")}");
        
        if (sprite == null)
        {
            Debug.LogWarning("SettingManager: Current crosshair sprite is null");
            return;
        }

        if (crosshairPreviewImage != null)
        {
            crosshairPreviewImage.sprite = sprite;
            Debug.Log("SettingManager: Applied sprite to crosshairPreviewImage");
        }

        Debug.Log($"SettingManager: Applying to {sniperWeapons.Length} sniper weapons");
        for (int i = 0; i < sniperWeapons.Length; i++)
        {
            SniperWeapon sniperWeapon = sniperWeapons[i];
            if (sniperWeapon == null)
            {
                Debug.LogWarning($"SettingManager: SniperWeapon[{i}] is null");
                continue;
            }

            sniperWeapon.SetScopeOverlaySprite(sprite);
            Debug.Log($"SettingManager: Applied crosshair sprite '{sprite.name}' to SniperWeapon[{i}]");
        }
    }

    private void LoadCrosshairSprites()
    {
        if (crosshairSprites != null && crosshairSprites.Length > 0)
        {
            System.Array.Sort(crosshairSprites, CompareSpritesByNumericName);
            return;
        }

        if (string.IsNullOrWhiteSpace(crosshairResourcesFolder))
        {
            crosshairSprites = new Sprite[0];
            return;
        }

        crosshairSprites = Resources.LoadAll<Sprite>(crosshairResourcesFolder.Trim().Replace("\\", "/"));
        System.Array.Sort(crosshairSprites, CompareSpritesByNumericName);
    }

    private static int CompareSpritesByNumericName(Sprite left, Sprite right)
    {
        if (left == null || right == null)
        {
            return 0;
        }

        bool leftParsed = int.TryParse(left.name, out int leftValue);
        bool rightParsed = int.TryParse(right.name, out int rightValue);

        if (leftParsed && rightParsed)
        {
            return leftValue.CompareTo(rightValue);
        }

        return string.Compare(left.name, right.name, System.StringComparison.OrdinalIgnoreCase);
    }

    private void RegisterSerializedAudioSources()
    {
        for (int i = 0; i < musicSources.Length; i++)
        {
            RegisterAudioSource(musicSources[i], managedMusicSources);
        }

        for (int i = 0; i < soundEffectSources.Length; i++)
        {
            RegisterAudioSource(soundEffectSources[i], managedSoundEffectSources);
        }
    }

    private void RegisterAudioSource(AudioSource source, List<ManagedAudioSource> targetList)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < targetList.Count; i++)
        {
            if (targetList[i].Source == source)
            {
                return;
            }
        }

        targetList.Add(new ManagedAudioSource(source));
    }

    private void UnregisterAudioSource(AudioSource source, List<ManagedAudioSource> targetList)
    {
        for (int i = targetList.Count - 1; i >= 0; i--)
        {
            if (targetList[i].Source == source)
            {
                targetList.RemoveAt(i);
            }
        }
    }

    private static void ApplyAudioGroupVolume(List<ManagedAudioSource> targetList, float groupVolume)
    {
        for (int i = targetList.Count - 1; i >= 0; i--)
        {
            ManagedAudioSource managedSource = targetList[i];
            if (managedSource.Source == null)
            {
                targetList.RemoveAt(i);
                continue;
            }

            managedSource.Source.volume = managedSource.BaseVolume * groupVolume;
        }
    }

    private void BindUiEvents()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (soundEffectVolumeSlider != null)
        {
            soundEffectVolumeSlider.onValueChanged.AddListener(SetSoundEffectVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
        }

        if (crosshairDropdown != null)
        {
            crosshairDropdown.onValueChanged.AddListener(SetCrosshair);
        }
    }

    private void UnbindUiEvents()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
        }

        if (soundEffectVolumeSlider != null)
        {
            soundEffectVolumeSlider.onValueChanged.RemoveListener(SetSoundEffectVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.RemoveListener(SetMusicVolume);
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.onValueChanged.RemoveListener(SetMouseSensitivity);
        }

        if (crosshairDropdown != null)
        {
            crosshairDropdown.onValueChanged.RemoveListener(SetCrosshair);
        }
    }

    private void RefreshUi()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(MasterVolume);
        }

        if (soundEffectVolumeSlider != null)
        {
            soundEffectVolumeSlider.SetValueWithoutNotify(SoundEffectVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(MusicVolume);
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.SetValueWithoutNotify(MouseSensitivity);
        }

        if (crosshairDropdown != null)
        {
            PopulateCrosshairDropdown();
            crosshairDropdown.SetValueWithoutNotify(CrosshairIndex);
        }
    }

    private void PopulateCrosshairDropdown()
    {
        if (crosshairDropdown == null || crosshairSprites == null)
        {
            return;
        }

        if (crosshairDropdown.options.Count == crosshairSprites.Length)
        {
            return;
        }

        crosshairDropdown.ClearOptions();
        List<string> optionNames = new List<string>(crosshairSprites.Length);
        for (int i = 0; i < crosshairSprites.Length; i++)
        {
            optionNames.Add(crosshairSprites[i] != null ? crosshairSprites[i].name : (i + 1).ToString());
        }

        crosshairDropdown.AddOptions(optionNames);
    }

    private sealed class ManagedAudioSource
    {
        public ManagedAudioSource(AudioSource source)
        {
            Source = source;
            BaseVolume = source != null ? source.volume : 1f;
        }

        public AudioSource Source { get; }
        public float BaseVolume { get; }
    }
}