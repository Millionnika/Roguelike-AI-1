using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BaseDefenseBattery : MonoBehaviour
{
    [Header("Base Defense")]
    [SerializeField] private CombatFaction faction = CombatFaction.Enemy;
    [SerializeField] private List<WeaponDataSO> weapons = new List<WeaponDataSO>();
    [SerializeField] private List<Transform> muzzles = new List<Transform>();
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.2f;
    [SerializeField] private bool showShotTracer = true;
    [SerializeField] private Color tracerColor = new Color(1f, 0.56f, 0.2f, 0.85f);
    [SerializeField, Min(0.005f)] private float tracerWidth = 0.04f;
    [SerializeField, Min(0.01f)] private float tracerLifetime = 0.08f;
    [SerializeField] private int tracerSortingOrder = 120;
    [SerializeField] private bool forceRotateWeaponVisualToTarget = true;

    private readonly List<WeaponInstance> runtimeWeapons = new List<WeaponInstance>();
    private readonly List<Transform> runtimeAimMounts = new List<Transform>();
    private float targetRefreshTimer;
    private Transform currentTarget;
    private TeamMember teamMember;

    public void ConfigureFaction(CombatFaction ownerFaction)
    {
        faction = ownerFaction;
        EnsureTeamMember();
        teamMember.SetFaction(ownerFaction);
        CombatLayerUtility.ApplyShipLayer(gameObject, ownerFaction);
    }

    public void EnsureWeapon(WeaponDataSO weaponData)
    {
        if (weaponData == null)
        {
            return;
        }

        if (weapons == null)
        {
            weapons = new List<WeaponDataSO>();
        }

        if (weapons.Count == 0)
        {
            weapons.Add(weaponData);
            BuildRuntimeWeapons();
        }
    }

    public void ConfigureLoadout(IReadOnlyList<WeaponDataSO> weaponLoadout, IReadOnlyList<Transform> muzzlePoints)
    {
        if (weapons == null)
        {
            weapons = new List<WeaponDataSO>();
        }
        else
        {
            weapons.Clear();
        }

        if (weaponLoadout != null)
        {
            for (int i = 0; i < weaponLoadout.Count; i++)
            {
                weapons.Add(weaponLoadout[i]);
            }
        }

        if (muzzles == null)
        {
            muzzles = new List<Transform>();
        }
        else
        {
            muzzles.Clear();
        }

        if (muzzlePoints != null)
        {
            for (int i = 0; i < muzzlePoints.Count; i++)
            {
                muzzles.Add(muzzlePoints[i]);
            }
        }

        BuildRuntimeWeapons();
    }

    private void Awake()
    {
        EnsureTeamMember();
        teamMember.SetFaction(faction);
        CombatLayerUtility.ApplyShipLayer(gameObject, faction);
        BuildRuntimeWeapons();
    }

    private void Update()
    {
        if (runtimeWeapons.Count == 0)
        {
            return;
        }

        float dt = Time.deltaTime;
        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            runtimeWeapons[i]?.Tick(dt);
        }

        targetRefreshTimer -= dt;
        if (targetRefreshTimer <= 0f || currentTarget == null)
        {
            targetRefreshTimer = Mathf.Max(0.05f, targetRefreshInterval);
            currentTarget = FindNearestHostileTarget();
        }

        if (currentTarget == null)
        {
            return;
        }

        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            WeaponInstance weapon = runtimeWeapons[i];
            if (weapon == null || weapon.Data == null)
            {
                continue;
            }

            Transform mount = i < runtimeAimMounts.Count ? runtimeAimMounts[i] : null;
            Vector2 aimedForward = AimWeaponAt(weapon, mount, currentTarget.position, dt);
            if (forceRotateWeaponVisualToTarget && mount != null)
            {
                AlignWeaponVisual(mount, aimedForward);
            }
            TryFire(weapon, currentTarget);
        }
    }

    private void BuildRuntimeWeapons()
    {
        runtimeWeapons.Clear();
        runtimeAimMounts.Clear();
        AutoFillMuzzlesIfNeeded();

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponDataSO weapon = weapons[i];
            if (weapon == null)
            {
                continue;
            }

            Transform muzzle = i < muzzles.Count ? muzzles[i] : null;
            if (muzzle == null)
            {
                muzzle = EnsureRuntimeMuzzle(i);
            }

            runtimeWeapons.Add(new WeaponInstance(weapon, transform, muzzle, faction, gameObject));
            runtimeAimMounts.Add(ResolveAimMount(muzzle));
        }
    }

    private Transform EnsureRuntimeMuzzle(int index)
    {
        string mountName = "RuntimeWeaponMount_" + (index + 1);
        Transform mount = transform.Find(mountName);
        if (mount == null)
        {
            GameObject mountObject = new GameObject(mountName);
            mount = mountObject.transform;
            mount.SetParent(transform, false);
            mount.localPosition = Vector3.zero;
            mount.localRotation = Quaternion.identity;
            mount.localScale = Vector3.one;
        }

        Transform muzzle = mount.Find("Muzzle");
        if (muzzle == null)
        {
            GameObject muzzleObject = new GameObject("Muzzle");
            muzzle = muzzleObject.transform;
            muzzle.SetParent(mount, false);
            muzzle.localPosition = Vector3.up * 0.3f;
            muzzle.localRotation = Quaternion.identity;
            muzzle.localScale = Vector3.one;
        }

        return muzzle;
    }

    private void AutoFillMuzzlesIfNeeded()
    {
        if (muzzles.Count > 0)
        {
            return;
        }

        Transform slotsRoot = transform.Find("WeaponSlots");
        if (slotsRoot == null)
        {
            return;
        }

        for (int i = 0; i < slotsRoot.childCount; i++)
        {
            Transform slot = slotsRoot.GetChild(i);
            Transform muzzle = slot.Find("Muzzle");
            if (muzzle == null)
            {
                for (int j = 0; j < slot.childCount; j++)
                {
                    Transform child = slot.GetChild(j);
                    Transform nested = child.Find("Muzzle");
                    if (nested != null)
                    {
                        muzzle = nested;
                        break;
                    }
                }
            }

            if (muzzle != null)
            {
                muzzles.Add(muzzle);
            }
        }
    }

    private Transform FindNearestHostileTarget()
    {
        TeamMember[] allMembers = FindObjectsByType<TeamMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Vector2 origin = transform.position;
        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < allMembers.Length; i++)
        {
            TeamMember member = allMembers[i];
            if (member == null || member.transform == null || member == teamMember)
            {
                continue;
            }

            if (member.Faction == faction || member.Faction == CombatFaction.Neutral)
            {
                continue;
            }

            if (!IsCandidateAlive(member.transform))
            {
                continue;
            }

            if (!CanAnyWeaponReach(member.transform))
            {
                continue;
            }

            float sqr = ((Vector2)member.transform.position - origin).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = member.transform;
            }
        }

        return best;
    }

    private bool CanAnyWeaponReach(Transform target)
    {
        float radius = ResolveTargetRadius(target);
        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            if (IsInsideWeaponArcWithTouch(runtimeWeapons[i], target.position, radius))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCandidateAlive(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        EnemyBaseLair baseLair = target.GetComponentInParent<EnemyBaseLair>();
        if (baseLair != null)
        {
            return baseLair.IsAlive;
        }

        return true;
    }

    private static float ResolveTargetRadius(Transform target)
    {
        Collider2D col = target.GetComponentInChildren<Collider2D>();
        if (col != null)
        {
            return Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
        }

        SpriteRenderer sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            return Mathf.Max(sr.bounds.extents.x, sr.bounds.extents.y);
        }

        return 0f;
    }

    private static bool IsInsideWeaponArcWithTouch(WeaponInstance weapon, Vector3 targetPosition, float targetRadius)
    {
        if (weapon == null || weapon.Data == null)
        {
            return false;
        }

        Vector2 origin = weapon.GetMuzzlePosition();
        Vector2 toTarget = (Vector2)targetPosition - origin;
        float distance = toTarget.magnitude;
        float range = Mathf.Max(0.1f, weapon.EffectiveMaxRange);
        if (distance > range + Mathf.Max(0f, targetRadius))
        {
            return false;
        }

        float arc = Mathf.Clamp(weapon.Data.firingAngle, 0f, 360f);
        if (arc >= 359.9f || distance <= 0.0001f)
        {
            return true;
        }

        Vector2 forward = weapon.GetArcCenterDirection();
        float angleToCenter = Vector2.Angle(forward, toTarget.normalized);
        float halfArc = arc * 0.5f;
        float extraAngle = targetRadius > 0f && distance > 0.0001f
            ? Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp(targetRadius / Mathf.Max(0.0001f, distance), 0f, 1f))
            : 0f;
        return angleToCenter <= halfArc + extraAngle;
    }

    private static Vector2 AimWeaponAt(WeaponInstance weapon, Transform mount, Vector3 targetWorldPosition, float deltaTime)
    {
        if (weapon == null || weapon.Data == null || weapon.MuzzleTransform == null)
        {
            return Vector2.up;
        }

        if (mount == null)
        {
            mount = ResolveAimMount(weapon.MuzzleTransform);
        }

        if (mount == null || mount == weapon.OwnerTransform)
        {
            return weapon.GetForwardDirection();
        }

        Vector2 origin = weapon.GetMuzzlePosition();
        Vector2 toTarget = (Vector2)targetWorldPosition - origin;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return weapon.GetForwardDirection();
        }

        Vector2 desiredForward = toTarget.normalized;
        Vector2 arcCenterForward = weapon.GetArcCenterDirection();
        float arc = Mathf.Clamp(weapon.Data.firingAngle, 0f, 360f);
        if (arc < 359.9f)
        {
            float signedAngle = Vector2.SignedAngle(arcCenterForward, desiredForward);
            float halfArc = arc * 0.5f;
            float clamped = Mathf.Clamp(signedAngle, -halfArc, halfArc);
            desiredForward = (Quaternion.Euler(0f, 0f, clamped) * arcCenterForward).normalized;
        }

        float turnSpeed = Mathf.Max(1f, weapon.Data.aimTurnSpeed);
        float maxRadians = turnSpeed * Mathf.Deg2Rad * Mathf.Max(0f, deltaTime);
        Vector2 currentForward = mount.up.sqrMagnitude > 0.0001f ? (Vector2)mount.up : Vector2.up;
        Vector3 nextForward3 = Vector3.RotateTowards(
            new Vector3(currentForward.x, currentForward.y, 0f).normalized,
            new Vector3(desiredForward.x, desiredForward.y, 0f).normalized,
            maxRadians,
            0f);
        Vector2 nextForward = new Vector2(nextForward3.x, nextForward3.y);
        if (nextForward.sqrMagnitude > 0.0001f)
        {
            mount.up = nextForward.normalized;
        }

        return nextForward.sqrMagnitude > 0.0001f ? nextForward.normalized : desiredForward;
    }

    private static Transform ResolveAimMount(Transform muzzle)
    {
        if (muzzle == null)
        {
            return null;
        }

        Transform current = muzzle;
        while (current != null)
        {
            string name = current.name;
            if (!string.IsNullOrEmpty(name) &&
                (name.IndexOf("weaponmount", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("weaponslot", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return current;
            }

            current = current.parent;
        }

        return muzzle.parent;
    }

    private static void AlignWeaponVisual(Transform mount, Vector2 desiredForward)
    {
        if (mount == null || desiredForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion worldRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(desiredForward.x, desiredForward.y, 0f).normalized);

        // 1) Primary case: dedicated visual node under mount.
        Transform explicitVisual = mount.Find("WeaponVisualInstance");
        if (explicitVisual != null)
        {
            explicitVisual.rotation = worldRotation;
        }

        // 2) Fallback: rotate any sprite-bearing direct children under mount except muzzle helpers.
        for (int i = 0; i < mount.childCount; i++)
        {
            Transform child = mount.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name.IndexOf("muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (child.GetComponentInChildren<SpriteRenderer>(true) != null)
            {
                child.rotation = worldRotation;
            }
        }

        // 3) Extra fallback for hand-edited prefabs:
        // some visuals are placed in WeaponSlot as siblings of WeaponMount.
        Transform slot = mount.parent;
        if (slot == null)
        {
            return;
        }

        for (int i = 0; i < slot.childCount; i++)
        {
            Transform sibling = slot.GetChild(i);
            if (sibling == null || sibling == mount)
            {
                continue;
            }

            if (sibling.name.IndexOf("muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                sibling.name.IndexOf("spawn", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (sibling.GetComponentInChildren<SpriteRenderer>(true) != null)
            {
                sibling.rotation = worldRotation;
            }
        }
    }

    private void TryFire(WeaponInstance weapon, Transform target)
    {
        if (weapon == null || target == null || !weapon.CanFireAt(target.position) || !weapon.BeginFire())
        {
            return;
        }

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            return;
        }

        Vector2 origin = weapon.GetMuzzlePosition();
        Vector2 dir = ((Vector2)target.position - origin).sqrMagnitude > 0.0001f
            ? ((Vector2)target.position - origin).normalized
            : weapon.GetForwardDirection();

        DamageInfo info = new DamageInfo
        {
            Amount = Mathf.Max(1f, weapon.Data.damage),
            SourceFaction = faction,
            Source = gameObject,
            WeaponData = weapon.Data,
            HitPoint = target.position,
            Direction = dir
        };
        damageable.TakeDamage(info);

        if (showShotTracer)
        {
            SpawnTracer(origin, target.position);
        }
    }

    private void SpawnTracer(Vector2 origin, Vector3 targetPosition)
    {
        GameObject tracerObject = new GameObject("BaseTracer");
        tracerObject.transform.SetParent(transform, true);
        LineRenderer lr = tracerObject.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = tracerWidth;
        lr.endWidth = tracerWidth;
        lr.startColor = tracerColor;
        lr.endColor = tracerColor;
        lr.sortingOrder = tracerSortingOrder;
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(origin.x, origin.y, 0f));
        lr.SetPosition(1, new Vector3(targetPosition.x, targetPosition.y, 0f));
        Destroy(tracerObject, Mathf.Max(0.01f, tracerLifetime));
    }

    private void EnsureTeamMember()
    {
        teamMember = GetComponent<TeamMember>();
        if (teamMember == null)
        {
            teamMember = gameObject.AddComponent<TeamMember>();
        }
    }
}
