using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatAudioController : MonoBehaviour
{
    [Header("Звук оружия")]
    [Tooltip("Базовая громкость звука выстрела. Итоговая громкость дополнительно меняется случайным разбросом и дистанцией для вражеских выстрелов.")]
    [SerializeField, Range(0f, 1f)] private float shotBaseVolume = 0.85f;
    [Tooltip("Случайный разброс высоты тона выстрела. Помогает избежать одинакового звучания при частой стрельбе. Рекомендуемый диапазон: 0.03-0.12.")]
    [SerializeField, Range(0f, 0.5f)] private float shotPitchRandomRange = 0.08f;
    [Tooltip("Случайный разброс громкости выстрела. Помогает сделать серию выстрелов менее монотонной. Рекомендуемый диапазон: 0.05-0.15.")]
    [SerializeField, Range(0f, 0.5f)] private float shotVolumeRandomRange = 0.12f;
    [Tooltip("Количество одновременных аудио-голосов для выстрелов. Чем выше значение, тем меньше шанс, что частые выстрелы будут обрывать друг друга. Рекомендуемый диапазон: 4-8.")]
    [SerializeField, Min(1)] private int shotAudioVoices = 4;

    [Header("Позиционирование выстрелов врага")]
    [Tooltip("Насколько звук выстрела врага становится пространственным. 0 = полностью 2D, 1 = полностью 3D.")]
    [SerializeField, Range(0f, 1f)] private float enemyShotSpatialBlend = 0.8f;
    [Tooltip("Дистанция до игрока, на которой выстрел врага считается близким и звучит громче.")]
    [SerializeField, Min(0.1f)] private float enemyShotNearDistance = 2.5f;
    [Tooltip("Дистанция до игрока, на которой выстрел врага считается дальним и звучит тише.")]
    [SerializeField, Min(0.2f)] private float enemyShotFarDistance = 18f;
    [Tooltip("Множитель громкости для близких выстрелов врага. Значение 1 сохраняет исходную громкость.")]
    [SerializeField, Range(0f, 2f)] private float enemyShotNearVolumeMultiplier = 1.05f;
    [Tooltip("Множитель громкости для дальних выстрелов врага. Значения ниже 1 делают дальние выстрелы тише.")]
    [SerializeField, Range(0f, 1f)] private float enemyShotFarVolumeMultiplier = 0.35f;
    [Tooltip("Максимальная сила стерео-панорамы по горизонтали для выстрелов врага.")]
    [SerializeField, Range(0f, 1f)] private float enemyShotPanStrength = 0.75f;
    [Tooltip("Горизонтальная дистанция, на которой панорама выстрела врага достигает максимума.")]
    [SerializeField, Min(0.1f)] private float enemyShotPanDistance = 12f;
    [Tooltip("Добавка к высоте тона для близких выстрелов врага. Небольшое положительное значение делает близкие выстрелы резче.")]
    [SerializeField, Range(-0.5f, 0.5f)] private float enemyShotNearPitchOffset = 0.06f;
    [Tooltip("Добавка к высоте тона для дальних выстрелов врага. Небольшое отрицательное значение делает дальние выстрелы глуше.")]
    [SerializeField, Range(-0.5f, 0.5f)] private float enemyShotFarPitchOffset = -0.08f;

    private AudioSource[] shotAudioSources;
    private int nextShotAudioSourceIndex;
    private Transform playerTransform;

    private void Awake()
    {
        EnsureWeaponAudioSources();
    }

    private void OnValidate()
    {
        shotAudioVoices = Mathf.Max(1, shotAudioVoices);
        enemyShotNearDistance = Mathf.Max(0.1f, enemyShotNearDistance);
        enemyShotFarDistance = Mathf.Max(enemyShotNearDistance + 0.1f, enemyShotFarDistance);
    }

    public void SetPlayerTransform(Transform newPlayerTransform)
    {
        playerTransform = newPlayerTransform;
    }

    public void PlayWeaponShot(WeaponDataSO weaponData, Vector3 shotWorldPosition, CombatFaction sourceFaction)
    {
        if (weaponData == null || weaponData.fireSound == null)
        {
            return;
        }

        EnsureWeaponAudioSources();
        if (shotAudioSources == null || shotAudioSources.Length == 0)
        {
            return;
        }

        AudioSource source = shotAudioSources[nextShotAudioSourceIndex];
        nextShotAudioSourceIndex = (nextShotAudioSourceIndex + 1) % shotAudioSources.Length;
        if (source == null)
        {
            return;
        }

        float randomPitch = 1f + Random.Range(-shotPitchRandomRange, shotPitchRandomRange);
        float randomVolume = 1f + Random.Range(-shotVolumeRandomRange, shotVolumeRandomRange);
        float volumeScale = Mathf.Clamp01(shotBaseVolume * randomVolume);

        source.spatialBlend = 0f;
        source.panStereo = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 500f;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        source.transform.position = shotWorldPosition;

        if (sourceFaction == CombatFaction.Enemy && playerTransform != null)
        {
            Vector3 playerPosition = playerTransform.position;
            float distance = Vector2.Distance(playerPosition, shotWorldPosition);
            float nearDistance = Mathf.Max(0.1f, enemyShotNearDistance);
            float farDistance = Mathf.Max(nearDistance + 0.1f, enemyShotFarDistance);
            float distanceLerp = Mathf.InverseLerp(nearDistance, farDistance, distance);

            float distanceVolume = Mathf.Lerp(enemyShotNearVolumeMultiplier, enemyShotFarVolumeMultiplier, distanceLerp);
            volumeScale *= Mathf.Clamp(distanceVolume, 0f, 2f);

            float pitchOffset = Mathf.Lerp(enemyShotNearPitchOffset, enemyShotFarPitchOffset, distanceLerp);
            randomPitch += pitchOffset;

            float panByX = Mathf.Clamp((shotWorldPosition.x - playerPosition.x) / Mathf.Max(0.1f, enemyShotPanDistance), -1f, 1f);
            source.panStereo = panByX * Mathf.Clamp01(enemyShotPanStrength);
            source.spatialBlend = Mathf.Clamp01(enemyShotSpatialBlend);
            source.minDistance = nearDistance;
            source.maxDistance = farDistance;
            source.spread = 25f;
        }

        source.pitch = Mathf.Clamp(randomPitch, 0.5f, 2f);
        source.PlayOneShot(weaponData.fireSound, volumeScale);
    }

    private void EnsureWeaponAudioSources()
    {
        int voices = Mathf.Max(1, shotAudioVoices);
        if (shotAudioSources != null && shotAudioSources.Length == voices && AllSourcesValid())
        {
            return;
        }

        Transform audioRoot = transform.Find("WeaponAudio");
        if (audioRoot == null)
        {
            audioRoot = new GameObject("WeaponAudio").transform;
            audioRoot.SetParent(transform, false);
            audioRoot.localPosition = Vector3.zero;
        }

        shotAudioSources = new AudioSource[voices];
        nextShotAudioSourceIndex = 0;

        for (int i = 0; i < voices; i++)
        {
            Transform sourceTransform = audioRoot.Find("WeaponShotSource_" + i);
            if (sourceTransform == null)
            {
                GameObject sourceObject = new GameObject("WeaponShotSource_" + i);
                sourceTransform = sourceObject.transform;
                sourceTransform.SetParent(audioRoot, false);
            }

            AudioSource source = sourceTransform.GetComponent<AudioSource>();
            if (source == null)
            {
                source = sourceTransform.gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            shotAudioSources[i] = source;
        }
    }

    private bool AllSourcesValid()
    {
        for (int i = 0; i < shotAudioSources.Length; i++)
        {
            if (shotAudioSources[i] == null)
            {
                return false;
            }
        }

        return true;
    }
}
