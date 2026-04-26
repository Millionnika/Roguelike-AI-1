using UnityEngine;

[DisallowMultipleComponent]
public sealed class ProjectileBehaviour : MonoBehaviour
{
    private Rigidbody2D body;
    private Collider2D projectileCollider;
    private IPoolService poolService;
    private GameObject prefabKey;
    private GameObject ownerObject;
    private CombatFaction ownerFaction;
    private WeaponDataSO weaponData;
    private LayerMask targetMask;
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
        float projectileLifetime,
        LayerMask collisionMask)
    {
        poolService = runtimePoolService;
        prefabKey = sourcePrefab;
        ownerObject = sourceOwner;
        ownerFaction = sourceFaction;
        weaponData = sourceWeaponData;
        targetMask = collisionMask;
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

        if (ownerObject != null && other.transform.root == ownerObject.transform.root)
        {
            return;
        }

        if (targetMask.value != 0 && ((1 << other.gameObject.layer) & targetMask.value) == 0)
        {
            return;
        }

        TeamMember targetTeamMember = other.GetComponentInParent<TeamMember>();
        if (targetTeamMember != null && ownerFaction != CombatFaction.Neutral && targetTeamMember.Faction == ownerFaction)
        {
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
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
