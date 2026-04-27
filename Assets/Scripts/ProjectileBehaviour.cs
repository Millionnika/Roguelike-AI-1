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

        transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
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

        if (ownerObject != null && other.transform.root == ownerObject.transform.root)
        {
            return;
        }

        TeamMember targetTeamMember = other.GetComponentInParent<TeamMember>();
        if (targetTeamMember == null)
        {
            Debug.Log($"{WeaponDebugPrefix} Projectile ignored {other.name}: no TeamMember.");
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
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
}
