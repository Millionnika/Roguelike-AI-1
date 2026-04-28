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
    private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();
    private readonly List<Collider2D> projectileColliders = new List<Collider2D>();

    internal void Initialize(
        IPoolService runtimePoolService,
        GameObject sourcePrefab,
        GameObject sourceOwner,
        CombatFaction sourceFaction,
        WeaponDataSO sourceWeaponData,
        Vector2 startDirection,
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

        EnsurePhysicsComponents();
        ConfigureOwnerCollisionIgnores();

        float visualOffset = weaponData != null ? weaponData.projectileRotationOffset : 0f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, direction) * Quaternion.Euler(0f, 0f, visualOffset);
        body.linearVelocity = direction * speed;
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
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

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
