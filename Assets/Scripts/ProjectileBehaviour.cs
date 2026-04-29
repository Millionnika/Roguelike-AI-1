using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProjectileBehaviour : MonoBehaviour
{
    private const string WeaponDebugPrefix = "[WeaponDebug]";
    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private IPoolService poolService;
    private GameObject prefabKey;
    private GameObject ownerObject;
    private CombatFaction ownerFaction;
    private WeaponDataSO weaponData;
    private Vector2 direction;
    private float damage;
    private float speed;
    private float maxDistance;
    private float maxLifetime;
    private float lifeTimer;
    private float traveledDistance;
    private Vector2 previousPosition;
    private bool activeProjectile;
    private bool isMissile;
    private float missileTurnSpeed;
    private float missileSeekRadius;
    private float missileWobbleAmplitude;
    private float missileWobbleFrequency;
    private float missileSpeedCurrent;
    private float missileAcceleration;
    private float missileTimer;
    private Vector2 preferredTargetPoint;
    private Transform missileTarget;
    private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();
    private readonly List<Collider2D> projectileColliders = new List<Collider2D>();

    internal void Initialize(
        IPoolService runtimePoolService,
        GameObject sourcePrefab,
        GameObject sourceOwner,
        CombatFaction sourceFaction,
        WeaponDataSO sourceWeaponData,
        Vector2 startDirection,
        Vector2 preferredTargetPoint,
        float projectileDamage,
        float projectileSpeed,
        float projectileMaxDistance,
        float projectileLifetime)
    {
        poolService = runtimePoolService;
        prefabKey = sourcePrefab;
        ownerObject = sourceOwner;
        ownerFaction = sourceFaction;
        weaponData = sourceWeaponData;
        direction = startDirection.sqrMagnitude > 0.0001f ? startDirection.normalized : Vector2.up;
        damage = Mathf.Max(0f, projectileDamage);
        speed = Mathf.Max(0.01f, projectileSpeed);
        maxDistance = Mathf.Max(0f, projectileMaxDistance);
        maxLifetime = Mathf.Max(0f, projectileLifetime);
        lifeTimer = 0f;
        traveledDistance = 0f;
        previousPosition = transform.position;
        activeProjectile = true;
        missileTimer = 0f;
        missileTarget = null;
        this.preferredTargetPoint = preferredTargetPoint;

        EnsurePhysicsComponents();
        ConfigureOwnerCollisionIgnores();

        isMissile = weaponData != null && weaponData.fireMode == FireMode.Missile;
        missileTurnSpeed = weaponData != null ? Mathf.Max(1f, weaponData.missileTurnSpeed) : 180f;
        missileSeekRadius = weaponData != null ? Mathf.Max(0.1f, weaponData.missileSeekRadius) : 12f;
        missileWobbleAmplitude = weaponData != null ? Mathf.Max(0f, weaponData.missileWobbleAmplitude) : 0f;
        missileWobbleFrequency = weaponData != null ? Mathf.Max(0f, weaponData.missileWobbleFrequency) : 0f;
        missileAcceleration = weaponData != null ? Mathf.Max(0f, weaponData.missileAcceleration) : 0f;
        missileSpeedCurrent = speed;
        if (isMissile)
        {
            AcquireMissileTarget();
        }

        float visualOffset = weaponData != null ? weaponData.projectileRotationOffset : 0f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0f, 0f, visualOffset);
        MoveProjectile(direction * speed, 0f);
        Debug.Log(
            $"{WeaponDebugPrefix} Projectile init: projectile={name} owner={(ownerObject != null ? ownerObject.name : "None")} team={ownerFaction} damage={damage:0.##}");
    }

    public void ForceDespawn()
    {
        if (!activeProjectile)
        {
            return;
        }

        Despawn();
    }

    private void Awake()
    {
        EnsurePhysicsComponents();
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
    }

    private void OnDisable()
    {
        RestoreOwnerCollisionIgnores();
    }

    private void FixedUpdate()
    {
        if (!activeProjectile)
        {
            return;
        }

        if (isMissile)
        {
            UpdateMissileMotion(Time.fixedDeltaTime);
        }
        else
        {
            MoveProjectile(direction * speed, Time.fixedDeltaTime);
        }

        lifeTimer += Time.fixedDeltaTime;
        Vector2 currentPosition = transform.position;
        traveledDistance += Vector2.Distance(previousPosition, currentPosition);
        previousPosition = currentPosition;

        if (maxLifetime > 0f && lifeTimer >= maxLifetime)
        {
            Despawn();
            return;
        }

        if (maxDistance > 0f && traveledDistance >= maxDistance)
        {
            Despawn();
        }
    }

    private void UpdateMissileMotion(float deltaTime)
    {
        missileTimer += Mathf.Max(0f, deltaTime);
        if (missileTarget == null || !missileTarget.gameObject.activeInHierarchy)
        {
            AcquireMissileTarget();
        }

        Vector2 steerDirection = direction;
        if (missileTarget != null)
        {
            Vector2 toTarget = ((Vector2)missileTarget.position - (Vector2)transform.position);
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector2 desired = toTarget.normalized;
                float maxRadians = missileTurnSpeed * Mathf.Deg2Rad * Mathf.Max(0f, deltaTime);
                Vector3 rotated = Vector3.RotateTowards(direction, desired, maxRadians, 0f);
                steerDirection = new Vector2(rotated.x, rotated.y);
            }
        }
        else if ((preferredTargetPoint - (Vector2)transform.position).sqrMagnitude > 0.04f)
        {
            Vector2 toPreferred = (preferredTargetPoint - (Vector2)transform.position).normalized;
            float maxRadians = missileTurnSpeed * Mathf.Deg2Rad * Mathf.Max(0f, deltaTime);
            Vector3 rotated = Vector3.RotateTowards(direction, toPreferred, maxRadians, 0f);
            steerDirection = new Vector2(rotated.x, rotated.y);
        }

        if (missileWobbleAmplitude > 0f && missileWobbleFrequency > 0f)
        {
            Vector2 perpendicular = new Vector2(-steerDirection.y, steerDirection.x);
            float wobble = Mathf.Sin(missileTimer * missileWobbleFrequency) * missileWobbleAmplitude;
            steerDirection = (steerDirection + perpendicular * wobble).normalized;
        }

        direction = steerDirection.sqrMagnitude > 0.0001f ? steerDirection.normalized : direction;
        missileSpeedCurrent = Mathf.Max(0.1f, missileSpeedCurrent + missileAcceleration * deltaTime);

        MoveProjectile(direction * missileSpeedCurrent, deltaTime);

        float visualOffset = weaponData != null ? weaponData.projectileRotationOffset : 0f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0f, 0f, visualOffset);
    }

    private void AcquireMissileTarget()
    {
        TeamMember[] teamMembers = FindObjectsByType<TeamMember>();
        Transform best = null;
        float bestSqr = float.MaxValue;
        Vector2 current = transform.position;
        float maxSqr = missileSeekRadius * missileSeekRadius;

        for (int i = 0; i < teamMembers.Length; i++)
        {
            TeamMember team = teamMembers[i];
            if (team == null || team.gameObject == ownerObject)
            {
                continue;
            }

            if (ownerFaction != CombatFaction.Neutral && team.Faction == ownerFaction)
            {
                continue;
            }

            float sqr = ((Vector2)team.transform.position - current).sqrMagnitude;
            if (sqr > maxSqr || sqr >= bestSqr)
            {
                continue;
            }

            bestSqr = sqr;
            best = team.transform;
        }

        missileTarget = best;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!activeProjectile)
        {
            return;
        }

        TryApplyHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!activeProjectile)
        {
            return;
        }

        TryApplyHit(collision.collider);
    }

    private void TryApplyHit(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        Debug.Log($"{WeaponDebugPrefix} Projectile hit candidate: {other.name}");

        if (IsOwnerCollider(other))
        {
            Debug.Log($"{WeaponDebugPrefix} Projectile ignored {other.name}: owner collider.");
            return;
        }

        TeamMember targetTeamMember = ResolveTeamMember(other);
        if (targetTeamMember == null)
        {
            Debug.Log($"{WeaponDebugPrefix} Projectile ignored {other.name}: no TeamMember.");
            return;
        }

        IDamageable damageable = ResolveDamageable(other);
        if (damageable == null)
        {
            Debug.Log($"{WeaponDebugPrefix} Projectile ignored {other.name}: no IDamageable.");
            return;
        }

        if (ownerFaction != CombatFaction.Neutral && targetTeamMember.Faction == ownerFaction)
        {
            Debug.Log($"{WeaponDebugPrefix} Projectile ignored {other.name}: same team {ownerFaction}.");
            return;
        }

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        DamageInfo info = new DamageInfo
        {
            Amount = damage,
            SourceFaction = ownerFaction,
            Source = ownerObject,
            WeaponData = weaponData,
            HitPoint = hitPoint,
            Direction = direction
        };

        damageable.TakeDamage(info);
        Debug.Log($"{WeaponDebugPrefix} Projectile damage applied to {other.name}: {damage:0.##}");
        Despawn();
    }

    private void Despawn()
    {
        activeProjectile = false;
        RestoreOwnerCollisionIgnores();
        MoveProjectile(Vector2.zero, 0f);

        if (poolService != null && prefabKey != null)
        {
            poolService.Return(prefabKey, gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void EnsurePhysicsComponents()
    {
        body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        projectileCollider = GetComponent<Collider2D>();
        if (projectileCollider == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.08f;
            projectileCollider = circle;
        }

        projectileCollider.isTrigger = true;
    }

    private void MoveProjectile(Vector2 velocity, float deltaTime)
    {
        if (body == null)
        {
            if (deltaTime > 0f)
            {
                transform.position += (Vector3)(velocity * deltaTime);
            }
            return;
        }

        if (deltaTime <= 0f)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        body.MovePosition(body.position + velocity * deltaTime);
    }

    private void ConfigureOwnerCollisionIgnores()
    {
        RestoreOwnerCollisionIgnores();

        if (ownerObject == null)
        {
            return;
        }

        projectileColliders.Clear();
        GetComponentsInChildren(true, projectileColliders);
        if (projectileColliders.Count == 0)
        {
            return;
        }

        Collider2D[] ownerColliders = ownerObject.GetComponentsInChildren<Collider2D>(true);
        for (int ownerIndex = 0; ownerIndex < ownerColliders.Length; ownerIndex++)
        {
            Collider2D ownerCollider = ownerColliders[ownerIndex];
            if (ownerCollider == null)
            {
                continue;
            }

            for (int projectileIndex = 0; projectileIndex < projectileColliders.Count; projectileIndex++)
            {
                Collider2D currentProjectileCollider = projectileColliders[projectileIndex];
                if (currentProjectileCollider == null)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(currentProjectileCollider, ownerCollider, true);
            }

            ignoredOwnerColliders.Add(ownerCollider);
        }
    }

    private void RestoreOwnerCollisionIgnores()
    {
        if (ignoredOwnerColliders.Count == 0 || projectileColliders.Count == 0)
        {
            ignoredOwnerColliders.Clear();
            return;
        }

        for (int ownerIndex = 0; ownerIndex < ignoredOwnerColliders.Count; ownerIndex++)
        {
            Collider2D ownerCollider = ignoredOwnerColliders[ownerIndex];
            if (ownerCollider == null)
            {
                continue;
            }

            for (int projectileIndex = 0; projectileIndex < projectileColliders.Count; projectileIndex++)
            {
                Collider2D currentProjectileCollider = projectileColliders[projectileIndex];
                if (currentProjectileCollider == null)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(currentProjectileCollider, ownerCollider, false);
            }
        }

        ignoredOwnerColliders.Clear();
    }

    private bool IsOwnerCollider(Collider2D other)
    {
        if (ownerObject == null || other == null)
        {
            return false;
        }

        Transform ownerTransform = ownerObject.transform;
        return other.transform == ownerTransform || other.transform.IsChildOf(ownerTransform);
    }

    private static TeamMember ResolveTeamMember(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        Transform hierarchyRoot = other.transform.root;
        TeamMember rootMember = hierarchyRoot != null ? hierarchyRoot.GetComponent<TeamMember>() : null;
        return rootMember != null ? rootMember : other.GetComponentInParent<TeamMember>();
    }

    private static IDamageable ResolveDamageable(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        Transform hierarchyRoot = other.transform.root;
        IDamageable rootDamageable = hierarchyRoot != null ? hierarchyRoot.GetComponent<IDamageable>() : null;
        return rootDamageable != null ? rootDamageable : other.GetComponentInParent<IDamageable>();
    }
}
