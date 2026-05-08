using System;
using System.Collections.Generic;
using SpaceFrontier.Player;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TargetingController : MonoBehaviour
{
    [Header("Визуал цели")]
    [Tooltip("Спрайт рамки цели. Если поле пустое, компонент создаст простую runtime-рамку автоматически.")]
    [SerializeField] private Sprite targetFrameSourceSprite;
    [Tooltip("Цвет рамки вокруг выбранной цели.")]
    [SerializeField] private Color targetFrameColor = new Color(0.45f, 0.75f, 1f, 0.95f);
    [Tooltip("Дополнительный отступ рамки от видимых границ цели в мировых единицах. Обычно 0.2-0.5.")]
    [SerializeField, Min(0f)] private float targetFramePadding = 0.35f;

    [Header("Линия цели")]
    [Tooltip("Цвет линии от корабля игрока до выбранной цели.")]
    [SerializeField] private Color targetLineColor = new Color(0.9f, 0.96f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float targetLineAlpha = 0.42f;
    [Tooltip("Толщина линии цели в мировых единицах. Обычно 0.02-0.06.")]
    [SerializeField, Min(0.01f)] private float targetLineWidth = 0.035f;
    [Tooltip("Sorting Order линии цели. Увеличьте значение, если линия скрывается под спрайтами.")]
    [SerializeField] private int targetLineSortingOrder = 1;
    [Tooltip("Если включено, линия цели отображается пунктиром.")]
    [SerializeField] private bool targetLineDashed;
    [Tooltip("Длина видимого сегмента пунктира. Используется только при включенном пунктире.")]
    [SerializeField, Min(0.02f)] private float targetLineDashSize = 0.35f;
    [Tooltip("Длина промежутка между сегментами пунктира. Используется только при включенном пунктире.")]
    [SerializeField, Min(0.01f)] private float targetLineGapSize = 0.2f;

    [Header("Выбор цели")]
    [Tooltip("Дополнительный отступ зоны клика вокруг видимых границ цели в мировых единицах. Увеличьте, если по мелким врагам трудно попасть мышью.")]
    [SerializeField, Min(0f)] private float targetWorldClickPadding = 0.25f;
    [Header("Подсказка атаки")]
    [SerializeField] private bool showAttackAssist;
    [SerializeField] private Color attackAssistOkColor = new Color(0.42f, 1f, 0.56f, 0.95f);
    [SerializeField] private Color attackAssistWarningColor = new Color(1f, 0.72f, 0.28f, 0.95f);
    [SerializeField, Min(0.2f)] private float attackAssistTextHeight = 1.4f;
    [SerializeField, Min(2f)] private int attackAssistFontSize = 4;
    [Header("Визуал сектора стрельбы")]
    [SerializeField] private bool showWeaponArcVisual = true;
    [SerializeField, Min(16)] private int weaponArcSegments = 72;
    [SerializeField, Min(0.005f)] private float weaponArcLineWidth = 0.03f;
    [SerializeField] private Color weaponArcColor = new Color(0.5f, 0.95f, 1f, 0.5f);
    [SerializeField] private int weaponArcSortingOrder = 18;

    private IReadOnlyList<EnemyShip> enemies;
    private PlayerShip player;
    private Camera mainCamera;
    private Transform worldRoot;
    private Action<string, string> logMessage;
    private Func<string, string> localize;

    private EnemyShip targetEnemy;
    private EnemyBaseLair targetBase;
    private bool manualTargetSelection;
    private GameObject targetFrameObject;
    private SpriteRenderer targetFrameRenderer;
    private Sprite runtimeTargetFrameSprite;
    private LineRenderer targetLineRenderer;
    private Material targetingMaterial;
    private Texture2D dashedLineTexture;
    private ShipEquipmentState equipmentState;
    private TextMeshPro attackAssistText;
    private LineRenderer weaponArcRenderer;

    internal EnemyShip TargetEnemy => targetEnemy;
    public EnemyBaseLair TargetBase => targetBase;
    public bool HasPlayerTarget => TryGetPlayerTargetPosition(out _);
    public Vector3 PlayerTargetPosition
    {
        get
        {
            TryGetPlayerTargetPosition(out Vector3 position);
            return position;
        }
    }

    internal void Initialize(
        PlayerShip playerShip,
        IReadOnlyList<EnemyShip> enemyList,
        Transform worldRootTransform,
        Camera camera,
        Action<string, string> logCallback,
        Func<string, string> localizeCallback)
    {
        player = playerShip;
        enemies = enemyList;
        worldRoot = worldRootTransform;
        mainCamera = camera;
        logMessage = logCallback;
        localize = localizeCallback;
    }

    internal void SetEnemies(IReadOnlyList<EnemyShip> enemyList)
    {
        enemies = enemyList;
    }

    internal void SetPlayer(PlayerShip playerShip)
    {
        player = playerShip;
    }

    internal void SetEquipmentState(ShipEquipmentState state)
    {
        equipmentState = state;
    }

    public void SetCamera(Camera camera)
    {
        mainCamera = camera;
    }

    public void SetWorldRoot(Transform root)
    {
        worldRoot = root;
        if (targetFrameObject != null)
        {
            targetFrameObject.transform.SetParent(GetVisualParent(), true);
        }
        if (targetLineRenderer != null)
        {
            targetLineRenderer.transform.SetParent(GetVisualParent(), true);
        }
    }

    internal void SetTargetEnemy(EnemyShip enemy)
    {
        if (manualTargetSelection)
        {
            // Приоритет ручного выбора игрока: авто-обновления не должны перезаписывать цель,
            // пока вручную выбранная цель еще валидна.
            bool manualEnemyAlive = targetEnemy != null && targetEnemy.IsAlive();
            bool manualBaseAlive = targetBase != null && targetBase.IsAlive;
            if (manualEnemyAlive || manualBaseAlive)
            {
                return;
            }
        }

        targetEnemy = enemy != null && enemy.IsAlive() ? enemy : null;
        if (targetEnemy != null)
        {
            targetBase = null;
        }

        UpdateTargetState();
    }

    public void ClearTarget()
    {
        targetEnemy = null;
        targetBase = null;
        manualTargetSelection = false;
        UpdateTargetState();
        SetTargetingVisualsActive(false);
    }

    internal bool TrySelectFromOverview(EnemyShip enemy)
    {
        if (enemy == null || !enemy.IsAlive())
        {
            return false;
        }

        targetBase = null;
        targetEnemy = enemy;
        manualTargetSelection = true;
        UpdateTargetState();
        LogTargetLocked(enemy.Id);
        return true;
    }

    public bool TrySelectFromWorld(Vector3 worldPoint)
    {
        EnemyShip selectedEnemy = null;
        float bestDistanceSqr = float.MaxValue;

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyShip enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive())
                {
                    continue;
                }

                Bounds bounds;
                if (!TryCalculateTargetBounds(enemy.Transform, out bounds))
                {
                    continue;
                }

                bounds.Expand(new Vector3(targetWorldClickPadding, targetWorldClickPadding, 0f));
                if (!bounds.Contains(worldPoint))
                {
                    continue;
                }

                float distanceSqr = ((Vector2)bounds.center - (Vector2)worldPoint).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    selectedEnemy = enemy;
                }
            }
        }

        if (selectedEnemy != null)
        {
            targetBase = null;
            targetEnemy = selectedEnemy;
            manualTargetSelection = true;
            UpdateTargetState();
            LogTargetLocked(selectedEnemy.Id);
            return true;
        }

        EnemyBaseLair selectedBase = TrySelectBaseAtWorldPoint(worldPoint);
        if (selectedBase == null)
        {
            return false;
        }

        targetEnemy = null;
        targetBase = selectedBase;
        manualTargetSelection = true;
        UpdateTargetState();
        LogTargetLocked(selectedBase.name);
        return true;
    }

    public bool HasManualTargetSelection => manualTargetSelection;

    public void AutoAcquireNearestEnemyInRange(float maxRange)
    {
        if (manualTargetSelection || player == null || player.Transform == null || enemies == null)
        {
            return;
        }

        float clampedRange = Mathf.Max(0.1f, maxRange);
        float bestSqr = clampedRange * clampedRange;
        EnemyShip best = null;
        Vector2 playerPosition = player.Transform.position;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyShip enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive() || enemy.Transform == null)
            {
                continue;
            }

            float distanceSqr = ((Vector2)enemy.Transform.position - playerPosition).sqrMagnitude;
            if (distanceSqr <= bestSqr)
            {
                bestSqr = distanceSqr;
                best = enemy;
            }
        }

        if (best != null)
        {
            SetTargetEnemy(best);
        }
    }

    public bool TryGetPlayerTargetPosition(out Vector3 targetPosition)
    {
        if (TryGetCurrentTargetTransform(out Transform targetTransform))
        {
            targetPosition = targetTransform.position;
            return true;
        }

        targetPosition = Vector3.zero;
        return false;
    }

    public bool TryGetCurrentTargetTransform(out Transform targetTransform)
    {
        if (targetEnemy != null && targetEnemy.IsAlive() && targetEnemy.Transform != null)
        {
            targetTransform = targetEnemy.Transform;
            return true;
        }

        if (targetBase != null && targetBase.IsAlive)
        {
            targetTransform = targetBase.transform;
            return true;
        }

        targetTransform = null;
        return false;
    }

    public void UpdateTargetState()
    {
        if (targetEnemy != null && !targetEnemy.IsAlive())
        {
            targetEnemy = null;
            manualTargetSelection = false;
        }
        if (targetBase != null && !targetBase.IsAlive)
        {
            targetBase = null;
            manualTargetSelection = false;
        }

        if (enemies == null)
        {
            return;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyShip enemy = enemies[i];
            if (enemy != null && enemy.TargetRenderer != null)
            {
                enemy.TargetRenderer.gameObject.SetActive(enemy == targetEnemy);
            }
        }
    }

    public void TickVisuals()
    {
        UpdateTargetState();
        RotateTargetRing();

        Transform targetTransform = null;
        bool hasTarget = player != null && player.Transform != null && TryGetCurrentTargetTransform(out targetTransform);
        if (!hasTarget)
        {
            SetTargetingVisualsActive(false);
            return;
        }

        EnsureTargetingVisuals();

        Bounds targetBounds;
        if (!TryCalculateTargetBounds(targetTransform, out targetBounds))
        {
            targetBounds = new Bounds(targetTransform.position, Vector3.one);
        }

        if (targetFrameRenderer != null && targetFrameRenderer.sprite != null)
        {
            Vector3 size = targetBounds.size + new Vector3(targetFramePadding, targetFramePadding, 0f);
            size.x = Mathf.Max(0.6f, size.x);
            size.y = Mathf.Max(0.6f, size.y);
            Vector3 spriteSize = targetFrameRenderer.sprite.bounds.size;
            targetFrameObject.SetActive(true);
            targetFrameObject.transform.position = new Vector3(targetBounds.center.x, targetBounds.center.y, 0f);
            targetFrameObject.transform.rotation = Quaternion.identity;
            targetFrameObject.transform.localScale = new Vector3(
                spriteSize.x > 0.001f ? size.x / spriteSize.x : 1f,
                spriteSize.y > 0.001f ? size.y / spriteSize.y : 1f,
                1f);
            targetFrameRenderer.color = targetFrameColor;
        }

        if (targetLineRenderer != null)
        {
            Vector3 start = player.Transform.position;
            Vector3 end = targetBounds.center;
            start.z = 0f;
            end.z = 0f;
            ConfigureLineRenderer(
                targetLineRenderer,
                GetResolvedTargetLineColor(),
                targetLineWidth,
                targetLineSortingOrder,
                targetLineDashed,
                targetLineDashSize,
                targetLineGapSize,
                start,
                end);
        }

        UpdateAttackAssist(targetBounds.center);
        UpdateWeaponArcVisual();
    }

    private Color GetResolvedTargetLineColor()
    {
        Color color = targetLineColor;
        color.a = Mathf.Clamp01(targetLineAlpha);
        return color;
    }

    private void OnDestroy()
    {
        if (runtimeTargetFrameSprite != null)
        {
            Destroy(runtimeTargetFrameSprite);
        }
        if (targetingMaterial != null)
        {
            Destroy(targetingMaterial);
        }
        if (dashedLineTexture != null)
        {
            Destroy(dashedLineTexture);
        }
        if (attackAssistText != null)
        {
            Destroy(attackAssistText.gameObject);
        }
        if (weaponArcRenderer != null)
        {
            Destroy(weaponArcRenderer.gameObject);
        }
    }

    private EnemyBaseLair TrySelectBaseAtWorldPoint(Vector3 worldPosition)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
        EnemyBaseLair selectedBase = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D collider = hits[i];
            if (collider == null)
            {
                continue;
            }

            EnemyBaseLair baseLair = collider.GetComponentInParent<EnemyBaseLair>();
            if (baseLair == null || !baseLair.IsAlive)
            {
                continue;
            }

            Bounds bounds;
            if (!TryCalculateTargetBounds(baseLair.transform, out bounds))
            {
                continue;
            }

            bounds.Expand(new Vector3(targetWorldClickPadding, targetWorldClickPadding, 0f));
            if (!bounds.Contains(worldPosition))
            {
                continue;
            }

            float distance = ((Vector2)bounds.center - (Vector2)worldPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                selectedBase = baseLair;
            }
        }

        return selectedBase;
    }

    private void EnsureTargetingVisuals()
    {
        Transform parent = GetVisualParent();
        if (targetFrameObject == null)
        {
            targetFrameObject = new GameObject("TargetFrame");
            targetFrameObject.transform.SetParent(parent, false);
            targetFrameRenderer = targetFrameObject.AddComponent<SpriteRenderer>();
            targetFrameRenderer.sortingOrder = 40;
        }

        if (targetFrameRenderer != null && targetFrameRenderer.sprite == null)
        {
            targetFrameRenderer.sprite = GetTargetFrameSprite();
        }

        if (targetLineRenderer == null)
        {
            GameObject lineObject = new GameObject("TargetLine");
            lineObject.transform.SetParent(parent, false);
            targetLineRenderer = lineObject.AddComponent<LineRenderer>();
            targetLineRenderer.positionCount = 2;
            targetLineRenderer.useWorldSpace = true;
            targetLineRenderer.alignment = LineAlignment.View;
            targetLineRenderer.textureMode = LineTextureMode.Stretch;
            targetLineRenderer.numCapVertices = 4;
            targetLineRenderer.sortingOrder = targetLineSortingOrder;
            targetLineRenderer.material = GetTargetingMaterial();
        }

        if (attackAssistText == null && showAttackAssist)
        {
            GameObject textObject = new GameObject("TargetAttackAssist");
            textObject.transform.SetParent(parent, false);
            attackAssistText = textObject.AddComponent<TextMeshPro>();
            attackAssistText.alignment = TextAlignmentOptions.Center;
            attackAssistText.fontSize = attackAssistFontSize;
            attackAssistText.enableWordWrapping = false;
            attackAssistText.raycastTarget = false;
            MeshRenderer renderer = attackAssistText.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 55;
            }
        }

        if (weaponArcRenderer == null && showWeaponArcVisual)
        {
            GameObject arcObject = new GameObject("WeaponArcVisual");
            arcObject.transform.SetParent(parent, false);
            weaponArcRenderer = arcObject.AddComponent<LineRenderer>();
            weaponArcRenderer.useWorldSpace = true;
            weaponArcRenderer.alignment = LineAlignment.View;
            weaponArcRenderer.textureMode = LineTextureMode.Stretch;
            weaponArcRenderer.numCapVertices = 2;
            weaponArcRenderer.sortingOrder = weaponArcSortingOrder;
            weaponArcRenderer.material = GetTargetingMaterial();
        }
    }

    private Transform GetVisualParent()
    {
        return worldRoot != null ? worldRoot : transform;
    }

    private void RotateTargetRing()
    {
        if (enemies == null)
        {
            return;
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyShip enemy = enemies[i];
            if (enemy != null && enemy.TargetRenderer != null && enemy == targetEnemy)
            {
                enemy.TargetRenderer.transform.Rotate(0f, 0f, 65f * Time.deltaTime);
            }
        }
    }

    private void SetTargetingVisualsActive(bool active)
    {
        if (targetFrameObject != null)
        {
            targetFrameObject.SetActive(active);
        }
        if (targetLineRenderer != null)
        {
            targetLineRenderer.gameObject.SetActive(active);
        }
        if (attackAssistText != null)
        {
            attackAssistText.gameObject.SetActive(active && showAttackAssist);
        }
        if (weaponArcRenderer != null)
        {
            weaponArcRenderer.gameObject.SetActive(active && showWeaponArcVisual);
        }
    }

    private void UpdateAttackAssist(Vector3 targetPosition)
    {
        if (!showAttackAssist)
        {
            if (attackAssistText != null)
            {
                attackAssistText.gameObject.SetActive(false);
            }
            return;
        }

        EnsureTargetingVisuals();
        if (attackAssistText == null || player == null || player.Transform == null)
        {
            return;
        }

        if (!TryGetAttackAssistMetrics(targetPosition, out float distance, out float range, out float angle, out float allowedAngle))
        {
            attackAssistText.gameObject.SetActive(false);
            return;
        }

        bool inRange = distance <= range + 0.01f;
        bool inAngle = allowedAngle >= 359.9f || angle <= allowedAngle * 0.5f + 0.1f;
        attackAssistText.color = inRange && inAngle ? attackAssistOkColor : attackAssistWarningColor;
        attackAssistText.text = "R " + distance.ToString("0.0") + "/" + range.ToString("0.0") +
                                "  A " + angle.ToString("0") + "°/" + allowedAngle.ToString("0") + "°";
        Vector3 anchor = player.Transform.position;
        attackAssistText.transform.position = new Vector3(anchor.x, anchor.y + attackAssistTextHeight, 0f);
        attackAssistText.gameObject.SetActive(true);
    }

    private bool TryGetAttackAssistMetrics(Vector3 targetPosition, out float distance, out float range, out float angle, out float allowedAngle)
    {
        distance = 0f;
        range = 0f;
        angle = 0f;
        allowedAngle = 360f;

        if (player == null || player.Transform == null || equipmentState == null || equipmentState.InstalledWeapons == null)
        {
            return false;
        }

        Vector2 toTarget = (Vector2)(targetPosition - player.Transform.position);
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        distance = toTarget.magnitude;
        Vector2 forward = player.Transform.up;
        angle = Vector2.Angle(forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.up, toTarget.normalized);

        int weaponCount = equipmentState.InstalledWeapons.Count;
        bool hasWeapon = false;
        float maxRange = 0f;
        float maxAngle = 0f;
        for (int i = 0; i < weaponCount; i++)
        {
            WeaponDataSO weapon = equipmentState.InstalledWeapons[i];
            if (weapon == null)
            {
                continue;
            }

            float weaponRange = weapon.maxRange > 0f
                ? weapon.maxRange
                : (weapon.projectileMaxDistance > 0f ? weapon.projectileMaxDistance : 6f);
            maxRange = Mathf.Max(maxRange, weaponRange);
            maxAngle = Mathf.Max(maxAngle, Mathf.Clamp(weapon.firingAngle, 0f, 360f));
            hasWeapon = true;
        }

        if (!hasWeapon)
        {
            return false;
        }

        range = Mathf.Max(0.1f, maxRange);
        allowedAngle = Mathf.Max(1f, maxAngle);
        return true;
    }

    private void UpdateWeaponArcVisual()
    {
        if (!showWeaponArcVisual)
        {
            if (weaponArcRenderer != null)
            {
                weaponArcRenderer.gameObject.SetActive(false);
            }
            return;
        }

        EnsureTargetingVisuals();
        if (weaponArcRenderer == null || player == null || player.Transform == null)
        {
            return;
        }

        if (!TryGetWeaponArcSpec(out float range, out float allowedAngle))
        {
            weaponArcRenderer.gameObject.SetActive(false);
            return;
        }

        Vector2 forward = player.Transform.up.sqrMagnitude > 0.0001f ? (Vector2)player.Transform.up : Vector2.up;
        Vector2 center = player.Transform.position;
        float clampedAngle = Mathf.Clamp(allowedAngle, 1f, 360f);
        int segments = Mathf.Max(16, weaponArcSegments);
        weaponArcRenderer.startWidth = weaponArcLineWidth;
        weaponArcRenderer.endWidth = weaponArcLineWidth;
        weaponArcRenderer.startColor = weaponArcColor;
        weaponArcRenderer.endColor = weaponArcColor;
        weaponArcRenderer.sortingOrder = weaponArcSortingOrder;
        weaponArcRenderer.gameObject.SetActive(true);

        if (clampedAngle >= 359.5f)
        {
            weaponArcRenderer.loop = true;
            weaponArcRenderer.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments;
                float radians = t * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                weaponArcRenderer.SetPosition(i, new Vector3(center.x + dir.x * range, center.y + dir.y * range, 0f));
            }
            return;
        }

        weaponArcRenderer.loop = false;
        int arcPoints = segments + 1;
        int totalPoints = arcPoints + 2;
        weaponArcRenderer.positionCount = totalPoints;
        float half = clampedAngle * 0.5f;
        Vector2 leftDir = (Quaternion.Euler(0f, 0f, -half) * forward).normalized;
        weaponArcRenderer.SetPosition(0, new Vector3(center.x, center.y, 0f));
        weaponArcRenderer.SetPosition(1, new Vector3(center.x + leftDir.x * range, center.y + leftDir.y * range, 0f));

        for (int i = 0; i < arcPoints; i++)
        {
            float t = i / (float)(arcPoints - 1);
            float angle = Mathf.Lerp(-half, half, t);
            Vector2 dir = (Quaternion.Euler(0f, 0f, angle) * forward).normalized;
            weaponArcRenderer.SetPosition(i + 1, new Vector3(center.x + dir.x * range, center.y + dir.y * range, 0f));
        }

        weaponArcRenderer.SetPosition(totalPoints - 1, new Vector3(center.x, center.y, 0f));
    }

    private bool TryGetWeaponArcSpec(out float range, out float allowedAngle)
    {
        range = 0f;
        allowedAngle = 360f;
        if (equipmentState == null || equipmentState.InstalledWeapons == null)
        {
            return false;
        }

        bool hasWeapon = false;
        float maxRange = 0f;
        float maxAngle = 0f;
        for (int i = 0; i < equipmentState.InstalledWeapons.Count; i++)
        {
            WeaponDataSO weapon = equipmentState.InstalledWeapons[i];
            if (weapon == null)
            {
                continue;
            }

            float weaponRange = weapon.maxRange > 0f
                ? weapon.maxRange
                : (weapon.projectileMaxDistance > 0f ? weapon.projectileMaxDistance : 6f);
            maxRange = Mathf.Max(maxRange, weaponRange);
            maxAngle = Mathf.Max(maxAngle, Mathf.Clamp(weapon.firingAngle, 0f, 360f));
            hasWeapon = true;
        }

        if (!hasWeapon)
        {
            return false;
        }

        range = Mathf.Max(0.1f, maxRange);
        allowedAngle = Mathf.Max(1f, maxAngle);
        return true;
    }

    private void LogTargetLocked(string targetName)
    {
        if (logMessage == null)
        {
            return;
        }

        string prefix = localize != null ? localize("log_target_locked") : "Цель захвачена: ";
        logMessage(prefix + targetName, "info");
    }

    private Sprite GetTargetFrameSprite()
    {
        if (runtimeTargetFrameSprite != null)
        {
            return runtimeTargetFrameSprite;
        }

        if (targetFrameSourceSprite != null && targetFrameSourceSprite.texture != null)
        {
            Texture2D texture = targetFrameSourceSprite.texture;
            runtimeTargetFrameSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                targetFrameSourceSprite.pixelsPerUnit);
            runtimeTargetFrameSprite.name = "TargetFrame_Runtime";
            return runtimeTargetFrameSprite;
        }

        runtimeTargetFrameSprite = CreateGeneratedTargetFrameSprite();
        return runtimeTargetFrameSprite;
    }

    private static Sprite CreateGeneratedTargetFrameSprite()
    {
        const int size = 64;
        const int thickness = 5;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool border = x < thickness || x >= size - thickness || y < thickness || y >= size - thickness;
                bool cornerCutout = (x > thickness && x < size - thickness - 1 && y > thickness && y < size - thickness - 1);
                pixels[y * size + x] = border && !cornerCutout ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "TargetFrame_Generated";
        return sprite;
    }

    private Material GetTargetingMaterial()
    {
        if (targetingMaterial != null)
        {
            return targetingMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        targetingMaterial = shader != null ? new Material(shader) : null;
        return targetingMaterial;
    }

    private Texture2D GetDashedLineTexture()
    {
        if (dashedLineTexture != null)
        {
            return dashedLineTexture;
        }

        dashedLineTexture = new Texture2D(2, 1, TextureFormat.RGBA32, false);
        dashedLineTexture.filterMode = FilterMode.Point;
        dashedLineTexture.wrapMode = TextureWrapMode.Repeat;
        dashedLineTexture.SetPixel(0, 0, Color.white);
        dashedLineTexture.SetPixel(1, 0, new Color(1f, 1f, 1f, 0f));
        dashedLineTexture.Apply();
        return dashedLineTexture;
    }

    private void ConfigureLineRenderer(
        LineRenderer renderer,
        Color color,
        float width,
        int sortingOrder,
        bool dashed,
        float dashSize,
        float gapSize,
        Vector3 start,
        Vector3 end)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.gameObject.SetActive(true);
        renderer.startColor = color;
        renderer.endColor = color;
        renderer.startWidth = width;
        renderer.endWidth = width;
        renderer.sortingOrder = sortingOrder;
        renderer.SetPosition(0, start);
        renderer.SetPosition(1, end);

        if (renderer.material == null)
        {
            renderer.material = GetTargetingMaterial();
        }

        if (renderer.material == null)
        {
            return;
        }

        if (dashed)
        {
            renderer.textureMode = LineTextureMode.Tile;
            renderer.material.mainTexture = GetDashedLineTexture();
            float segment = Mathf.Max(0.01f, dashSize + gapSize);
            float lineLength = Vector3.Distance(start, end);
            renderer.material.mainTextureScale = new Vector2(Mathf.Max(1f, lineLength / segment), 1f);
        }
        else
        {
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.material.mainTexture = null;
            renderer.material.mainTextureScale = Vector2.one;
        }
    }

    private static bool TryCalculateTargetBounds(Transform targetRoot, out Bounds bounds)
    {
        bounds = default;
        if (targetRoot == null)
        {
            return false;
        }

        SpriteRenderer[] renderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(false);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null || IsNonTargetBodyRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider2D[] colliders = targetRoot.GetComponentsInChildren<Collider2D>(false);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        bounds = new Bounds(targetRoot.position, Vector3.one);
        return true;
    }

    private static bool IsNonTargetBodyRenderer(SpriteRenderer renderer)
    {
        string lowerName = renderer.name.ToLowerInvariant();
        return lowerName.Contains("target") ||
               lowerName.Contains("aura") ||
               lowerName.Contains("shield") ||
               lowerName.Contains("thruster") ||
               lowerName.Contains("engine_fire") ||
               lowerName.Contains("enginefire");
    }
}
